using System;
using System.Collections.Generic;
using HarmonyLib;
using Light.UI.Window;
using LightInDark.Core;
using LightInDark.Game;
using LightInDark.Language;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Light.Patches;

/// <summary>
/// **会议中"死亡玩家名牌"悬停详情**（2026-10-24 用户需求）
///
/// ═══════════════════════════════════════════════════════════════════════
/// 【需求（用户原话）】
///   会议中鼠标放在**死亡玩家名牌**上时：
///     · local player 已死 → 显示 detail 框，写该玩家的死因
///       格式：<c>&lt;玩家颜色&gt;玩家名&lt;/color&gt;: 死因(第X轮)</c>
///     · local player 没死 → 显示一段文本"占位"
///   **均用翻译键** ✓
///
/// 【参考 NOS（一定要看 ✓）】
///   · 悬停挂法：`NebulaPluginNova\Behavior\MeetingPlayerButton.cs:99-123` ——
///     克隆 `playerVoteArea.Buttons.transform.Find("CancelButton")`，
///     `OnClick.RemoveAllListeners()` 后挂 `OnMouseOver/OnMouseOut` ✓
///   · 本工程已有同款：`LightAPI\UI\Ability\MeetingTargetButton.cs:55-80`（照抄它最稳 ✓）
///   · 悬浮框：NOS 用 `MouseOverPopup`（`NebulaManager.cs:54`，跟鼠标走的 `MetaScreen` 框）；
///     我们用它在本工程里的等价物 —— **`DetailPopup`**（`Light\UI\Window\DetailPopup.cs:417`）✓
///
/// 【原版机制（读 19.0 源码确认 ✓，不猜）】
///   · 悬停判定在 `PassiveButtonManager.Update()`（`PassiveButtonManager.cs:41-140`）：
///     遍历注册过的 `PassiveUiElement`，用各自的 `Colliders` 做 `OverlapPoint`，
///     再在 `HandleMouseOver()`（:230）里比 **world z**：
///     `if (currentOver && button.transform.position.z > currentOver.transform.position.z) return;`
///     → **同容器里 z 越小越靠前** ✓（与 AGENTS §4.3 一致 ✓）
///   · **点击不会被挡住**：同一个循环里对**每个** `PassiveUiElement` 的每个 collider 都跑
///     `controller.CheckDrag(col)` ✓ → 我们这块空按钮叠在名牌上，卡片本身的 `PlayerButton`
///     仍然收得到点击 ✓（投票不受影响 ✓）
///
/// 【自查】
///   ✓ §4.1 每个 patch 类都有**类级** `[HarmonyPatch]`
///   ✓ §4.5 克隆体的 `OnClick` 是**替换**（`new ButtonClickedEvent()`），不是追加
///   ✓ `OnMouseOver/Out` 用**新的 `UnityEvent`**（清掉模板自带监听）
///   ✓ §4.4 `AspectPosition` 全部禁用（否则位置会被算回去）
///   ✓ §4.6.1 判空一律 `!= null`，**没有**对 Unity 对象用 `??`
///   ✓ 名字牌位置/尺寸是**量出来的**（`NameText` 的 rect + lossyScale），不是拍脑袋 ✗
///   ✓ 一次性诊断日志（开局打一次位置/尺寸，方便调）
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>

// ── 会议开始：给每个**死亡玩家**的名牌建悬停区 ──
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class MeetingDeathInfoStartPatch
{
    // 排在 RoleButtonManager（Priority.High）之后：职业按钮先建，我们再装自己的
    [HarmonyPriority(Priority.Low)]
    public static void Postfix(MeetingHud __instance)
    {
        try { MeetingDeathInfo.Build(__instance); }
        catch (Exception ex) { LightLogger.LogError("[MeetingDeathInfo] Build", ex); }
    }
}

// ── 会议结束：销毁悬停区 + 收起 detail 框 ──
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
public static class MeetingDeathInfoEndPatch
{
    public static void Postfix()
    {
        try { MeetingDeathInfo.Clear(); }
        catch (Exception ex) { LightLogger.LogWarning($"[MeetingDeathInfo] Clear 失败：{ex.Message}"); }
    }
}

/// <summary>悬停区的建/拆 + 文本拼装。</summary>
internal static class MeetingDeathInfo
{
    // ── 翻译键（全部走 Language ✓ 用户要求"均用翻译键"）──
    // ★ 占位符照 **NOS 的写法**（`Behavior/MeetingMapLayer.cs:217`、`Modules/Achievement.cs:417`）：
    //   `%大写名%` 包裹，调用处直接链式 `.Replace("%X%", 值)` ✓ **不用** string.Format ✗ **也不封装 Format 帮助器** ✗
    private const string KeyFormat = "meeting.deathinfo.format";   // <color=#%COLOR%>%NAME%</color>: %CAUSE%(%ROUND%)
    private const string KeyRound = "meeting.deathinfo.round";    // 第%ROUND%轮
    private const string KeyAlive = "meeting.deathinfo.aliveInfo"; // 占位（local 未死时）
    private const string KeyUnknown = "meeting.deathinfo.unknown";  // 未知

    // 语言文件缺键时的兜底（与 SChinese 一致；英文文件会覆盖 ✓）
    private const string DefaultFormat = "<color=#%COLOR%>%NAME%</color>: %CAUSE%(%ROUND%)";
    private const string DefaultRound = "第%ROUND%轮";
    private const string DefaultAlive = "<color=#%COLOR%>%NAME%</color>:%REASON%";
    private const string DefaultUnknown = "未知";

    private static readonly List<GameObject> _hovers = new();
    private static bool _logged;

    // ═══════════════════════════════════════════════════════════════════
    //  建
    // ═══════════════════════════════════════════════════════════════════

    public static void Build(MeetingHud hud)
    {
        Clear();

        if (hud == null || hud.playerStates == null) return;

        // 顺手清掉设置菜单可能留下的"Tab 钉住"状态（否则 Show() 会直接 return ✗）
        DetailPopup.Reset();

        int total = hud.playerStates.Length;
        bool dumpAll = !_logged;                     // 第一次会议：把所有 area 的真值打一遍 ✓

        int built = 0, skipped = 0, deadCount = 0;
        foreach (var area in hud.playerStates)
        {
            if (area == null) { skipped++; continue; }
            bool dead = IsDeadPlayer(area);
            if (dead) deadCount++;

            // ★ 诊断：把"到底谁死了、名牌在哪个本地坐标、卡片多宽"一次性打出来（AGENTS §4.8）
            if (dumpAll)
            {
                var probe = MeasureNameplate(area);
                var data = LightPlayerDataManager.GetData(area.PlayerId);
                LightLogger.Log($"[MeetingDeathInfo] area id={(byte)area.PlayerId} " +
                                $"AmDead={area.AmDead} dataIsDead={(data != null && data.IsDead)} " +
                                $"nameLocal={probe.pos} hoverSize={probe.size} z={probe.pos.z:0.###}");
            }

            if (!dead) continue;                     // 只给死亡玩家建 ✓

            byte playerId = area.PlayerId;
            GameObject? go = null;
            try
            {
                // ★ NOS 的挂法（MeetingPlayerButton.cs:99-111 / 本工程 MeetingTargetButton.cs:55-66）
                var template = area.Buttons != null ? area.Buttons.transform.Find("CancelButton") : null;
                if (template == null) { skipped++; continue; }

                go = Object.Instantiate(template.gameObject, area.transform);
                go.name = "LidDeathHover_" + playerId;
                go.SetActive(true);                          // 模板在 Buttons 下是关着的 ✓
                go.transform.localScale = Vector3.one;       // 尺寸我们自己按名牌定 ✓

                // §4.4：不禁用 AspectPosition 的话，位置每帧被算回去 ✗
                foreach (var aspect in go.GetComponentsInChildren<AspectPosition>(true))
                    if (aspect != null) aspect.enabled = false;

                // 位置/尺寸：量原版名字牌（`NameText` 的 rect + lossyScale）✓
                var measured = MeasureNameplate(area);
                go.transform.localPosition = measured.pos;

                var col = go.GetComponent<BoxCollider2D>();
                if (col == null) col = go.AddComponent<BoxCollider2D>();
                col.size = measured.size;
                col.offset = Vector2.zero;
                col.isTrigger = true;

                // 不可见（只要 collider）✓
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = null;

                var passive = go.GetComponent<PassiveButton>();
                if (passive == null) { Object.Destroy(go); skipped++; continue; }

                // §4.5：**替换**而不是追加（模板自带 OnClick 会一起触发 ✗）
                //   NOS 原版写法是 RemoveAllListeners()（MeetingPlayerButton.cs:108）✓ 这里保持等价
                passive.OnClick.RemoveAllListeners();
                passive.OnClick = new Button.ButtonClickedEvent();
                // 模板自带的 hover 监听也清掉，换成我们自己的 ✓
                passive.OnMouseOver = new UnityEvent();
                passive.OnMouseOut = new UnityEvent();

                byte id = playerId;
                // ★ 2026-10-24 第 4 版：照 **NOS 的成熟实现** ✓
                //   · 悬停区/位置：`Behavior/MeetingPlayerButton.cs:100-111`（克隆 CancelButton + sprite=null ✓）
                //   · 提示框文字：NOS 给 overlay 用的是**固定文本属性** `AttributeAsset.OverlayContent`
                //     （`Modules/MetaWidget/GUIWidget.cs:355`：FontSize=1.5、min=1.1、max=1.5、Size=(5,6) ✓）
                //     → 所以这里**不传字号**，走 `DetailPopup` 的兜底 1.5（就是 NOS 那个数 ✓）
                //     ⚠️ 不能拿名牌 `NameText` 当锚点 ✗ —— 那是**原版名牌字号**，不是 NOS 提示框字号 ✓
                Transform anchor = go.transform;
                passive.OnMouseOver.AddListener((UnityAction)(() => OnHover(id, anchor)));
                passive.OnMouseOut.AddListener((UnityAction)(() => DetailPopup.Hide()));

                _hovers.Add(go);
                built++;

                LightLogger.Log($"[MeetingDeathInfo] 悬停区已建 ✓ 玩家 {playerId} " +
                                $"localPos={measured.pos} size={measured.size} areaScale={area.transform.lossyScale}");
            }
            catch (Exception ex)
            {
                LightLogger.LogWarning($"[MeetingDeathInfo] 建悬停区失败（玩家 {playerId}）：{ex.Message}");
                if (go != null) { try { Object.Destroy(go); } catch { } }
            }
        }

        LightLogger.Log($"[MeetingDeathInfo] 本次会议：playerStates={total}，判为死亡={deadCount}，" +
                        $"建了 {built} 个悬停区（找不到模板/组件跳过 {skipped}）；local 死亡={IsLocalDead()} ✓");
        _logged = true;
    }

    public static void Clear()
    {
        foreach (var go in _hovers)
        {
            try { if (go != null) Object.Destroy(go); } catch { }
        }
        _hovers.Clear();
        try { DetailPopup.Hide(); } catch { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  悬停 → 文本
    // ═══════════════════════════════════════════════════════════════════

    private static void OnHover(byte playerId, Transform anchor)
    {
        try
        {
            string text = BuildText(playerId);
            // 诊断：确认 hover 真的命中了（用户测的时候看这一行就能判断 ✓）
            LightLogger.Log($"[MeetingDeathInfo] hover 命中 玩家 {playerId} → \"{text}\"");
            // canPinByTab = false ✓（会议里不希望 Tab 钉住）
            DetailPopup.Show(text, false, anchor);   // 不传字号 → DetailPopup 兜底 1.5 = NOS OverlayContent 的值 ✓
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[MeetingDeathInfo] OnHover({playerId}) 失败：{ex.Message}");
        }
    }

    private static string BuildText(byte playerId)
    {


        var data = LightPlayerDataManager.GetData(playerId);

        string name = ResolveName(playerId, data);
        string hex = ResolveColorHex(playerId, data);
        string cause = ResolveCause(data);

        string round = Language.Translate(KeyRound, DefaultRound)
                               .Replace("%ROUND%", ResolveRound(data).ToString());
        if (!IsLocalDead())
            return Language.Translate(KeyAlive, DefaultAlive)
                       .Replace("%COLOR%", hex)
                       .Replace("%NAME%", name);
                       

        return Language.Translate(KeyFormat, DefaultFormat)
                       .Replace("%COLOR%", hex)
                       .Replace("%NAME%", name)
                       .Replace("%CAUSE%", cause)
                       .Replace("%ROUND%", round);
    }

    private static string ResolveName(byte playerId, LightPlayerData? data)
    {
        if (data != null && !string.IsNullOrEmpty(data.PlayerName)) return data.PlayerName;

        try
        {
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null) continue;
                if (pc.PlayerId != playerId) continue;
                if (pc.Data != null && !string.IsNullOrEmpty(pc.Data.PlayerName)) return pc.Data.PlayerName;
            }
        }
        catch { }

        return "?";
    }

    private static string ResolveColorHex(byte playerId, LightPlayerData? data)
    {
        int colorId = -1;
        try
        {
            if (data != null) colorId = data.ColorId;
            else
            {
                foreach (var pc in PlayerControl.AllPlayerControls)
                {
                    if (pc == null || pc.PlayerId != playerId) continue;
                    if (pc.Data != null) colorId = pc.Data.DefaultOutfit.ColorId;
                    break;
                }
            }
        }
        catch { }

        try
        {
            var colors = Palette.PlayerColors;
            if (colors != null && colors.Length > 0)
            {
                if (colorId < 0 || colorId >= colors.Length) colorId = 0;
                return ColorUtility.ToHtmlStringRGB(colors[colorId]);
            }
        }
        catch { }

        return "FFFFFF";   // 拿不到颜色就白字 ✓（不显示成一片黑 ✗）
    }

    private static string ResolveCause(LightPlayerData? data)
    {
        if (data == null) return Language.Translate(KeyUnknown, DefaultUnknown);

        // 凶手名拿不到时传 null（否则 {0} 会写成 "Unknown" ✗）
        string? killer = data.KillerName;
        if (string.IsNullOrEmpty(killer) || killer == "Unknown") killer = null;

        // ★ 死因走 DeathCause 注册表 → 内部就是翻译键（death.*）✓
        string? cause = DeathCause.Resolve(data.DeathCauseId, killer);
        if (!string.IsNullOrEmpty(cause)) return cause;

        // 兜底：老的枚举文案（保证"永远有字" ✓）
        cause = data.GetDeathCauseText();
        if (!string.IsNullOrEmpty(cause)) return cause;

        return Language.Translate(KeyUnknown, DefaultUnknown);
    }

    /// <summary>
    /// 死亡轮次（**1 基，给人看的**）。
    /// `DeathMeetingNumber` 是"死亡时已结束的会议数"（`ExileController.Begin` 里 `CurrentMeetingNumber++` ✓
    /// 见 `RoleInterceptPatch.cs:667`）→ 所以显示用 +1 ✓（开局第一轮死亡 = 第1轮 ✓）
    /// </summary>
    private static int ResolveRound(LightPlayerData? data)
    {
        int round;
        if (data != null && data.DeathMeetingNumber >= 0) round = data.DeathMeetingNumber + 1;
        else round = LightPlayerDataManager.CurrentMeetingNumber + 1;

        if (round < 1) round = 1;
        return round;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  工具
    // ═══════════════════════════════════════════════════════════════════

    private static bool IsLocalDead()
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;
            if (local.Data == null) return false;
            return local.Data.IsDead;
        }
        catch { return false; }
    }

    private static bool IsDeadPlayer(PlayerVoteArea area)
    {
        try
        {
            if (area.AmDead) return true;
            var data = LightPlayerDataManager.GetData(area.PlayerId);
            return data != null && data.IsDead;
        }
        catch { return false; }
    }

    /// <summary>
    /// **量**名牌位置/尺寸（不猜 ✗）。
    ///
    /// ⚠️ 2026-10-24 第 2 版（用户："老老实实复制粘贴 NOS 然后适配"）：
    ///   上一版用 `NameText.rectTransform.rect` 当尺寸 ✗ —— rect 可能被 TMP 的 autoSizing
    ///   改得很大/很小，且名字短的时候明显偏窄 ✓
    ///   现在照 NOS 的"固定几何"风格 ✓：
    ///     · **x = 0**（名牌在卡片里是居中的 ✓）
    ///     · **y = 量 `NameText` 的本地 y**（这是唯一必须量的东西 ✓）
    ///     · **宽 = 整张卡片宽**（量 `Background` 的 bounds ✓，NOS 也是拿卡片宽度当基准 ✓）
    ///     · **高 = 0.42 的横条**（名牌带 ✓，容错大，用户"放在名牌上"就能命中 ✓）
    ///     · **z = 整张卡片最前 - 1**（`PassiveButtonManager` 只认 z 更小者 ✓）
    /// </summary>
    private static (Vector3 pos, Vector2 size) MeasureNameplate(PlayerVoteArea area)
    {
        var fallback = (new Vector3(0f, -0.85f, -2.5f), new Vector2(1.45f, 0.42f));
        try
        {
            // ① y：量名字（`PlayerVoteArea.cs:468` 的 TMP 字段 ✓）
            float nameY = fallback.Item1.y;
            float nameX = 0f;
            var tmp = area.NameText;
            if (tmp != null && tmp.transform != null)
            {
                Vector3 nLocal = area.transform.InverseTransformPoint(tmp.transform.position);
                nameY = nLocal.y;
                nameX = nLocal.x;
            }

            // ② 宽：量卡片背景（world bounds → vote area 本地单位 ✓）
            float cardW = fallback.Item2.x;
            var bg = area.Background;
            if (bg != null)
            {
                float w = bg.bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(area.transform.lossyScale.x));
                if (w > 0.5f && w < 6f) cardW = w;      // 明显不合理的值不用 ✗
            }

            // ③ z：整张卡片最前 - 1（`PassiveButtonManager.HandleMouseOver` 只认 z 更小者 ✓）
            float z = MeasureFrontZ(area) - 1f;

            var pos = new Vector3(nameX, nameY, z);
            var size = new Vector2(cardW * 0.95f, 0.42f);
            return (pos, size);
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[MeetingDeathInfo] MeasureNameplate 失败：{ex.Message}");
            return fallback;
        }
    }

    private static float SafeRatio(float a, float b)
    {
        if (Mathf.Abs(b) < 0.0001f) return 1f;
        return a / b;
    }

    /// <summary>
    /// 量**这张投票卡片最靠前的 z**（同容器里 z 越小越靠前 ✓）。
    /// 用所有 SpriteRenderer 在 vote area 本地空间里的 z 取最小 ✓（不猜 ✗）。
    /// </summary>
    private static float MeasureFrontZ(PlayerVoteArea area)
    {
        float min = 0f;
        bool any = false;
        try
        {
            foreach (var sr in area.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                float z = area.transform.InverseTransformPoint(sr.transform.position).z;
                if (!any) { min = z; any = true; }
                else if (z < min) min = z;
            }
        }
        catch { }

        // 一点都量不到就退回 -3（原版会议 UI 常见深度区间 ✓）
        return any ? min : -3f;
    }

    
}
