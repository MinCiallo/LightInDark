using LightInDark.Configuration;
using LightInDark.Core;
using LightInDark.Events;
using LightInDark.Roles;
using LightInDark.RPCs;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LightInDark.Game
{
    public interface IPlayer
    {
        bool IsDead { get; }
        bool IsLocal { get; }
        string Name { get; }
        Vector2 Position { get; }
        RuntimeRoleTemplate Role { get; }
    }

    public interface IBindPlayer
    {
        Player MyPlayer { get; }
        bool AmOwner { get; }
    }

    /// <summary>
    /// 健全的 Player 系统，封装 PlayerControl 并联合 Role。
    /// </summary>
    public class Player : IPlayer, IBindPlayer, IGameOperator, ILifespan
    {
        public PlayerControl Control { get; private set; }

        // ---- IPlayer ----
        public bool IsDead => Control?.Data?.IsDead ?? true;
        public bool IsLocal => Control == PlayerControl.LocalPlayer;
        public string Name => Control?.Data?.PlayerName ?? "Unknown";
        /// <summary>
        /// 世界坐标。
        /// ⚠️ 原来写的是 `Control?.transform?.position ?? Vector2.zero` —— 但 `Control` 是 **Unity 对象**，
        ///   已销毁（假 null）时 `?.` 挡不住，访问 `.transform` 会抛 `MissingReferenceException` ✗
        ///   （AGENTS §4.6.1）。改成显式 `== null` 判断（能识别假 null）并兜住异常。
        /// </summary>
        public Vector2 Position
        {
            get
            {
                try
                {
                    var c = Control;
                    if (c == null) return Vector2.zero;
                    var t = c.transform;
                    if (t == null) return Vector2.zero;
                    return t.position;
                }
                catch { return Vector2.zero; }
            }
        }
        public RuntimeRoleTemplate Role { get; internal set; }
        /// <summary>Victory identity retained when a ghost role replaces abilities after death.</summary>
        public RoleTemplate VictoryRole { get; private set; }

        /// <summary>
        /// 职业数据位（对齐 Nebula 的 `roleData` 字典）。见 <see cref="Roles.RoleData"/>：
        /// 职业的可变状态放这里，框架负责同步，不用每个职业自己写 RPC。
        /// ⚠️ 它是 **每局对象上的字段**（Player 是每局新建的），所以换局自然清空 ✓
        /// </summary>
        private readonly Dictionary<int, int> _roleData = new();

        /// <summary>读一个职业数据位（无记录 = 0）。</summary>
        internal int GetRoleData(int dataId)
        {
            try { return _roleData.TryGetValue(dataId, out var v) ? v : 0; }
            catch { return 0; }
        }

        /// <summary>写一个职业数据位（同步由 <see cref="Roles.RoleData.Set"/> 负责，别直接调）。</summary>
        internal void SetRoleData(int dataId, int value)
        {
            try { _roleData[dataId] = value; } catch { }
        }

        public bool IsWinner { get; set; }

        public Player MyPlayer => this;
        public bool AmOwner => IsLocal;

        // =====================================================================
        //  ★ 静态便捷入口（2026-10-16 用户提出 ✓）
        //
        //  【为什么要有这一层】用户原话：
        //    "我外部如果没职业我只能通过 GameManager 来访问我的玩家实例吗？
        //      Nebula 有 Player.LocalPlayer …… 而不是用原版的 PlayerControl.LocalPlayer，
        //      那样我们封装的意义就没了" ✓
        //    → 完全同意 ✓：包装类如果每次都要外面自己 `GameManager.Instance.GetPlayer(...)` ✓，
        //      那外部代码就同时依赖"我们的包装"和"内部管理器"两套东西 ✗；
        //      而且一旦有人图省事直接 `PlayerControl.LocalPlayer` ✓，
        //      **职业/RoleData/事件订阅全都在我们这层** ✗ → 拿到的只是个壳 ✓（正是"封装白做"✓）
        //    → 所以这里给出一套**只依赖 `Player` 这一类**的入口 ✓（对齐 Nebula 的 `GamePlayer.LocalPlayer` ✓）
        //
        //  ⚠️ 一律返回**我们自己这层的 `Player`** ✓（不是 `PlayerControl` ✓）；
        //     真要底层对象时用 `player.Control` ✓
        //  ⚠️ 拿不到返回 `null` ✓（外层一律 `!= null` 判 ✓ —— 我们的 Player 是托管对象，
        //     不是 Unity 对象，所以这里 `!= null` 就是普通判空 ✓，不涉及 AGENTS §4.6.1 的假 null ✓）
        // =====================================================================

        /// <summary>本局**本地玩家**（拿不到返回 null ✓）。等价 Nebula 的 `GamePlayer.LocalPlayer` ✓</summary>
        public static Player? LocalPlayer
        {
            get
            {
                try { return GameManager.Instance.LocalPlayer; }
                catch { return null; }
            }
        }

        /// <summary>**房主玩家**（大厅/局内都可用 ✓；拿不到返回 null ✓）</summary>
        public static Player? HostPlayer
        {
            get
            {
                try
                {
                    var client = AmongUsClient.Instance;
                    if (client == null) return null;

                    // ⚠️ 用 `GetClient(HostId)` 取角色（工程里 `CustomRPC.ResolveSenderId` 就是同一套用法 ✓）
                    var hostChar = client.GetClient(client.HostId)?.Character;
                    if (hostChar == null) return null;

                    return Get(hostChar);
                }
                catch (Exception ex)
                {
                    LightLogger.LogWarning($"[Player.HostPlayer] {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>本局**全部玩家**（我们这层的包装 ✓；没就绪时是空序列 ✓）</summary>
        public static System.Collections.Generic.IEnumerable<Player> AllPlayers
        {
            get
            {
                try { return GameManager.Instance.AllPlayers; }
                catch { return System.Array.Empty<Player>(); }
            }
        }

        /// <summary>按 PlayerId 取（找不到返回 null ✓）</summary>
        public static Player? Get(byte playerId)
        {
            try { return GameManager.Instance.GetPlayer(playerId); }
            catch { return null; }
        }

        /// <summary>
        /// 由**原版对象**反查我们的包装 ✓（外部拿到 `PlayerControl` 时用这个转回来 ✓）
        /// —— 这是"别直接用 `PlayerControl`"那条规矩的配套入口 ✓
        /// </summary>
        public static Player? Get(PlayerControl? control)
        {
            try
            {
                if (control == null) return null;         // == 走 Unity 的判空重载（§4.6.1）✓
                return GameManager.Instance.GetPlayer(control.PlayerId);
            }
            catch { return null; }
        }

        /// <summary>按条件找**第一个**满足的玩家（找不到返回 null ✓）</summary>
        public static Player? Find(Func<Player, bool> predicate)
            => TryGetPlayer(predicate, out var p) ? p : null;

        /// <summary>按条件找**全部**满足的玩家 ✓</summary>
        public static System.Collections.Generic.List<Player> FindAll(Func<Player, bool> predicate)
        {
            var list = new System.Collections.Generic.List<Player>();
            try
            {
                if (predicate == null) return list;
                foreach (var p in AllPlayers)
                {
                    if (p == null) continue;
                    bool ok;
                    try { ok = predicate(p); } catch { continue; }   // 单个谓词抛异常 → 跳过它，不影响其它 ✓
                    if (ok) list.Add(p);
                }
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[Player.FindAll] {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// 按条件尝试取一个玩家 ✓（用户点名的那个用法 ✓）：
        /// <code>
        ///   if (Player.TryGetPlayer(p => !p.IsDead && p.Is&lt;Jackal&gt;(), out var jackal)) { … }
        /// </code>
        /// </summary>
        public static bool TryGetPlayer(Func<Player, bool> predicate, out Player player)
        {
            player = null!;
            try
            {
                if (predicate == null) return false;
                foreach (var p in AllPlayers)
                {
                    if (p == null) continue;
                    bool ok;
                    try { ok = predicate(p); } catch { continue; }
                    if (!ok) continue;
                    player = p;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[Player.TryGetPlayer] {ex.Message}");
            }
            return false;
        }

        /// <summary>当前玩家是不是**房主** ✓</summary>
        public bool IsHost
        {
            get
            {
                try
                {
                    var client = AmongUsClient.Instance;
                    if (client == null || Control == null) return false;
                    return client.GetClient(client.HostId)?.Character == Control;
                }
                catch { return false; }
            }
        }

        public bool IsDeadObject => Control == null || Control.Data == null
            || Control.Data.Disconnected || Control.Data.IsDead;

        public Color PlayerColor
        {
            get
            {
                try
                {
                    try
                    {
                        if (Control?.Data != null)
                        {
                            var colorId = Control.Data.DefaultOutfit.ColorId;
                            if (colorId >= 0 && colorId < Palette.PlayerColors.Length)
                                return ((UnityEngine.Color)Palette.PlayerColors[colorId]).ToLIDColor();
                        }
                    }
                    catch { }
                    return Color.White;
                }
                catch (Exception ex)
                {
                    LightLogger.LogError("Player.PlayerColor", ex);
                    return default;
                }
            }
        }

        // ---- 角色 ----
        public RoleCategory? RoleCategory => Role?.RoleCategory;

        public Player(PlayerControl control)
        {
            try
            {
                Control = control;
                EventSystem.RegisterInstance(this);
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.Player", ex);
            }
        }

        /// <summary>
        /// 切换角色（本地立即切换，并发送RPC同步）
        /// </summary>
        public void SetRole(RoleTemplate newRole, int[] arguments = null)
        {
            try
            {
                if (newRole == null) return;
                arguments ??= newRole.DefaultArguments;

                var ev = new PlayerTryToChangeRoleEvent(Control, Role, newRole);
                EventSystem.RunEvent(ev);
                if (ev.IsCanceled) return;

                Role?.Inactivate();

                // ★ 先赋值、再激活（审查 #12）：否则 OnActivated 里读到的 MyPlayer.Role 还是旧职业
                var runtime = newRole.CreateRuntimeFor(this);
                Role = runtime;
                if (!newRole.IsGhostRole) VictoryRole = newRole;
                runtime?.Activate();

                EventTriggers.OnRoleAssigned(Control, newRole, arguments);

                // ★ 审查 A3：**按 CodeName 下发**（不再用 `newRole.Id`）——
                //   Id 是按注册顺序发的号，两端顺序/数量有差别就会整体错位 → 静默发错职业 ✗
                //   CodeName 是职业自己声明的稳定字符串 ✓ 协议层从此不会再发错职业 ✓
                RpcDefinitions.SetRoleByCode(Control.PlayerId, newRole.CodeName, arguments);

                Core.LightLogger.Log($"[Player] {Name} → {newRole.Name}");
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.SetRole", ex);
            }
        }

        /// <summary>
        /// 仅本地设置角色（用于RPC接收）
        /// </summary>
        internal void SetRoleLocal(RoleTemplate newRole, int[] arguments = null)
        {
            try
            {
                if (newRole == null) return;

                Role?.Inactivate();

                // ★ 先赋值、再激活（审查 #12，与 SetRole 同理）
                var runtime = newRole.CreateRuntimeFor(this);
                Role = runtime;
                if (!newRole.IsGhostRole) VictoryRole = newRole;
                runtime?.Activate();

                Core.LightLogger.Log($"[Player] {Name} (本地) → {newRole.Name}");
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.SetRoleLocal", ex);
            }
        }

        // ---- 操作 ----
        public void Suicide(PlayerState state = PlayerState.Suicide)
        {
            try
            {
                RpcDefinitions.Suicide(Control, playerState: state);
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.Suicide", ex);
            }
        }

        public void MurderPlayer(PlayerControl victim, PlayerState state = PlayerState.BeKilled)
        {
            try
            {
                RpcDefinitions.MurderPlayer(Control, victim, state);
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.MurderPlayer", ex);
            }
        }

        // ---- 判断 ----
        public bool IsRole(string roleName)
        {
            try
            {
                return Role?.Name == roleName;
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.IsRole", ex);
                return default;
            }
        }

        public bool Is<T>() where T : RoleTemplate
        {
            try
            {
                return Role?.Role is T;
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.Is", ex);
                return default;
            }
        }

        public bool HasRole => Role != null;

        // ---- 清理 ----
        public void Release()
        {
            try
            {
                EventSystem.UnregisterInstance(this);
                Role?.Release();
                Role = null;
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.Release", ex);
            }
        }

        void IGameOperator.OnReleased()
        {
            try
            {
            }
            catch (Exception ex)
            {
                LightLogger.LogError("Player.OnReleased", ex);
            }
        }
    }
    public enum PlayerState : uint
    {
        /// <summary>
        /// 普通死亡，会在复盘时显示真正的凶手。
        /// </summary>
        Dead = 0,
        /// <summary>
        /// 自杀，凶手记录为本人。
        /// </summary>
        Suicide = 1,
        /// <summary>
        /// 被猜测，凶手记录为猜测者。非会议中触发本死因将会被替换为PlayerState.Dead。
        /// </summary>
        BeGuessed = 2,
        /// <summary>
        /// 更精确的指向"被击杀"的死因。事实上，更建议使用PlayerState.Dead。凶手被记录为击杀者。
        /// </summary>
        BeKilled = 3,
        /// <summary>
        /// 警长击杀时走火。凶手记录为尝试击杀的人。
        /// </summary>
        GoOff = 4,
        /// <summary>
        /// 被放逐，不记录凶手。
        /// </summary>
        Exile = 5,
    }
}
