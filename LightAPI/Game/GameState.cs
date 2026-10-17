using System;
using AmongUs.GameOptions;
using InnerNet;
using LightInDark.Core;

namespace LightInDark.Game;

/// <summary>
/// **全局游戏阶段查询**（2026-10-16 用户要求 ✓："我们有 GameState 这种东西吗，
/// 如果没有，你去参考 FinalSuspect 写一个" ✓）。
///
/// 参考：`FinalSuspect\DataHandling\FinalGameData\GameStates.cs`（107 行）✓
///
/// ═══════════════════════════════════════════════════════════════════════
/// 【与 FinalSuspect 的关键差异（也是本实现唯一的"改进点"）】
///
/// FinalSuspect 是**混合式**：一部分属性**实时算**（`IsLobby` / `IsCanMove` / `IsDead` … ✓），
/// 另一部分（`InGame` / `InMeeting` / `InitGame`）是**缓存字段** ✗，
/// 由 `UpdateGameState(state, type)` 从各个 patch 里手动喂 ✓ →
///   ① 任何一条喂它的 patch **漏挂/顺序不对/被异常吃掉** ✗ → 缓存就**永远停在错的值** ✓
///      而调用方看到的是"阶段判断错了" ✗ —— 这类"静默不一致"正是本工程最怕的（AGENTS §4.2.0/§11.6 ✓）
///   ② 多局之间忘记复位 ✗ → 上一局的状态漏进下一局 ✓
///
/// ★ 本实现**全部属性都是实时计算** ✓✓ —— 一个缓存字段都没有 ✓、不需要任何 patch 去喂 ✓、
///   因此**不可能出现"状态和现实不一致"** ✓，也就没有复位问题 ✓。
///   代价是每次读都碰一下原版单例 ✓（都是 `Instance != null` 级别的判断 ✓，可忽略 ✓）。
///
/// ⚠️ 全部属性**永不抛异常** ✓（内部各自 try/catch ✓ 失败返回false ✓）——
///    阶段判断是给外部/职业当"守卫条件"用的 ✓，它自己抛异常会把调用方整条逻辑带崩 ✗
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
public static class GameState
{
    // =====================================================================
    //  连接 / 房间层
    // =====================================================================

    /// <summary>已加入某个房间 ✓（`NotJoined` 的反面 ✓）</summary>
    public static bool IsJoined => Try(() => AmongUsClient.Instance != null
                                             && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined);

    /// <summary>还没进任何房间（主菜单/大厅浏览器 ✓）</summary>
    public static bool IsNotJoined => Try(() => AmongUsClient.Instance != null
                                                && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.NotJoined);

    /// <summary>自由练习模式（本地单机 ✓）</summary>
    public static bool IsFreePlay => Try(() => AmongUsClient.Instance?.NetworkMode == NetworkModes.FreePlay);

    /// <summary>联机（走网络 ✓）</summary>
    public static bool IsOnlineGame => Try(() => AmongUsClient.Instance?.NetworkMode == NetworkModes.OnlineGame);

    /// <summary>本地游戏（局域网/本机 ✓）</summary>
    public static bool IsLocalGame => Try(() => AmongUsClient.Instance?.NetworkMode == NetworkModes.LocalGame);

    /// <summary>**人在大厅**（已加入、但还没进对局 ✓）—— 等价 FinalSuspect 的 `IsLobby` ✓</summary>
    public static bool IsLobby => IsJoined && !IsFreePlay && !IsInGame && !IsInMeeting && Ship == null;

    /// <summary>我是不是**房主** ✓</summary>
    public static bool AmHost => Try(() => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost);

    // =====================================================================
    //  对局层
    // =====================================================================

    /// <summary>**对局进行中** ✓ —— 有飞船/地图就是局内（自由模式也算 ✓，和 FinalSuspect 一致 ✓）</summary>
    public static bool IsInGame => IsFreePlay || Try(() => ShipStatus.Instance != null);

    /// <summary>开局**初始化**还没完成（我们自己的 `GameManager` 还没建 Player 表 ✓）</summary>
    public static bool IsInitGame => Try(() => GameManager.Instance.LocalPlayer == null && IsInGame);

    /// <summary>**会议中**（含讨论与投票 ✓；用原版单例实时判断 ✓，不缓存 ✓）</summary>
    public static bool IsInMeeting => Try(() => MeetingHud.Instance != null);

    /// <summary>正在**投票**（会议已进入投票阶段 ✓）</summary>
    public static bool IsVoting => Try(() =>
    {
        var hud = MeetingHud.Instance;
        if (hud == null) return false;
        // ⚠️ 19.0 里枚举叫 **`MeetingStates`**（不是 FinalSuspect 那份的 `VoteStates` ✗ ——
        //    跨版本照抄会编译不过 ✓，以 19.0 源码为准 ✓）：
        //    Animating / Discussion / NotVoted / Voted / Results / Proceeding ✓
        var st = hud.CurrentState;
        return st is MeetingHud.MeetingStates.Voted or MeetingHud.MeetingStates.NotVoted;
    });

    /// <summary>会议正在**结果/放逐流程**中（不能再操作 ✓）</summary>
    public static bool IsMeetingResults => Try(() =>
    {
        var hud = MeetingHud.Instance;
        if (hud == null) return false;
        var st = hud.CurrentState;
        return st is MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding;
    });

    /// <summary>**任务阶段**（局内且不在会议 ✓）</summary>
    public static bool IsInTask => IsInGame && !IsInMeeting;

    /// <summary>开场动画中（`IntroCutscene` 或 `HudManager.IsIntroDisplayed` ✓）</summary>
    public static bool IsInIntro => Try(() =>
    {
        if (IntroCutscene.Instance != null) return true;
        var hud = HudManager.Instance;
        return hud != null && hud.IsIntroDisplayed;
    });

    /// <summary>放逐动画中 ✓</summary>
    public static bool IsInExile => Try(() => ExileController.Instance != null);

    /// <summary>正在玩小游戏（任务/门卡/生命体征… ✓）</summary>
    public static bool IsInMinigame => Try(() => Minigame.Instance != null);

    /// <summary>地图开着 ✓</summary>
    public static bool IsMapOpen => Try(() =>
    {
        var map = MapBehaviour.Instance;
        return map != null && map.IsOpen;
    });

    /// <summary>聊天框有焦点（输入中 ✓）</summary>
    public static bool IsChatFocused => Try(() =>
    {
        var hud = HudManager.Instance;
        if (hud == null || hud.Chat == null) return false;
        // ⚠️ 局部变量别叫 `field` ✗ —— C# 14 里它是属性访问器关键字（CS9273 ✓）
        var input = hud.Chat.freeChatField;
        return input != null && input.textArea != null && input.textArea.hasFocus;
    });

    /// <summary>倒计时中（房主点了开始 ✓）</summary>
    public static bool IsCountDown => Try(() =>
    {
        var gsm = GameStartManager.Instance;
        return gsm != null && gsm.startState == GameStartManager.StartingStates.Countdown;
    });

    // =====================================================================
    //  本地玩家层（用**我们这层**的包装 ✓ —— 见 `Player.LocalPlayer` ✓）
    // =====================================================================

    /// <summary>本地玩家（我们这层的包装 ✓；没有就 null ✓）</summary>
    public static Player? LocalPlayer => Player.LocalPlayer;

    /// <summary>我死了 ✓（大厅里不算死 ✓，与 FinalSuspect 的语义一致 ✓）</summary>
    public static bool IsDead
    {
        get
        {
            if (!IsInGame) return false;
            var p = Player.LocalPlayer;
            return p != null && p.IsDead;
        }
    }

    /// <summary>我现在**能动**（原版 `CanMove` ✓：不在管道/小游戏/会议/开场/地图… ✓）</summary>
    public static bool IsCanMove => Try(() =>
    {
        var pc = PlayerControl.LocalPlayer;
        return pc != null && pc.CanMove;
    });

    /// <summary>我在通风管里 ✓</summary>
    public static bool IsInVent => Try(() =>
    {
        var pc = PlayerControl.LocalPlayer;
        return pc != null && pc.inVent;
    });

    /// <summary>房主是我们这层的**模组房主**（房主 + 跑着本模组 ✓）</summary>
    public static bool IsModHost
    {
        get
        {
            try
            {
                if (!AmHost) return false;
                return Player.HostPlayer != null;    // 大厅表建好之后才为真 ✓
            }
            catch { return false; }
        }
    }

    // =====================================================================
    //  模式 / 地图
    // =====================================================================

    /// <summary>当前地图对象（局内才有 ✓）</summary>
    public static ShipStatus? Ship => Try<ShipStatus?>(() => ShipStatus.Instance, null);

    /// <summary>普通模式 ✓</summary>
    public static bool IsNormalGame => Try(() =>
    {
        var mode = GameOptionsManager.Instance?.CurrentGameOptions?.GameMode;
        return mode is GameModes.Normal;
    });

    /// <summary>隐藏者模式 ✓</summary>
    public static bool IsHideNSeek => Try(() =>
    {
        var mode = GameOptionsManager.Instance?.CurrentGameOptions?.GameMode;
        return mode is GameModes.HideNSeek;
    });

    /// <summary>当前地图是不是某一张 ✓</summary>
    public static bool IsMap(MapNames name) => Try(() =>
    {
        var opts = GameOptionsManager.Instance?.CurrentGameOptions;
        return opts != null && (MapNames)opts.MapId == name;
    });

    // =====================================================================
    //  内部：统一的"永不抛"包装 ✓
    // =====================================================================

    private static bool Try(Func<bool> f)
    {
        try { return f(); } catch { return false; }        // 阶段判断失败 = 当作"不是那个阶段" ✓
    }

    private static T Try<T>(Func<T> f, T fallback)
    {
        try { return f(); } catch { return fallback; }
    }
}
