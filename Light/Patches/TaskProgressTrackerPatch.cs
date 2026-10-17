
using HarmonyLib;
using LightInDark.Game;
using TMPro;
using UnityEngine;

namespace Light.Patches;

/// <summary>
/// **任务进度条**：把标题改成 `标题( xx.x% )`，并给填充条上色 ✓
///
/// 参考：FinalSuspect `Patches\Game_Vanilla\TaskProgressTrackerPatch.cs`（68 行 ✓）
///
/// ═══════════════════════════════════════════════════════════════════════
/// 【原版是怎么画这根条的】19.0 `ProgressTracker.FixedUpdate`（L20-58 ✓）
/// <code>
///   if (PlayerTask.PlayerHasTaskOfType&lt;IHudOverrideTask&gt;(LocalPlayer)) { TileParent.enabled = false; return; }  // 通讯破坏 → 整条藏掉 ✓
///   int num = (Tutorial ? 1 : AllPlayers.Count - NumImpostors) - 断线人数;      // = "桶数"
///   float num2 = CompletedTasks / (float)TotalTasks * num;                     // ★ 注意乘的是**桶数**不是 100 ✗
///   curValue = Mathf.Lerp(curValue, num2, Time.fixedDeltaTime * 2f);           // 平滑
///   TileParent.material.SetFloat("_Buckets", num);
///   TileParent.material.SetFloat("_FullBuckets", curValue);                    // ← **填充长度在这**
/// </code>
///   → 所以**填充长度必须交给原版** ✓：我们自己写 `_Buckets/_FullBuckets` 会跟它的
///     `Lerp` 打架 ✗（表现就是"填充抖动/长度不对"✗）。我们只该动**颜色** ✓ 和**文字** ✓。
///
/// 【2026-10-16 修正（用户报"填充有问题、百分比有问题" ✓）】与 FS 逐行对照后补齐三处 ✓：
///   ① **通讯破坏**：原版这时把 `TileParent` 整个 `enabled=false` ✓，
///      但我们仍然把百分比写在标题上 ✗ → 等于**破坏期间照样泄露任务进度** ✗✗
///      → 照 FS：文字变灰 + 显示 `(??.?%)` ✓
///   ② **填充颜色必须保证可见**：原来无条件用**玩家自己的颜色** ✗ ——
///      玩家选白/浅色时，填充条和底槽几乎同色 → 看起来"没填充" ✗✗
///      → 照 FS 的思路按身份取色 ✓：**内鬼 → 按百分比红→黄→绿** ✓；**船员 → 自己职业的颜色** ✓
///        （职业色取自我们自己的 `RuntimeRoleTemplate.Color` ✓，比 FS 的 `GetRoleColor()` 更贴合本工程 ✓）
///   ③ **标题文字颜色**：完成任务→绿 / 未完成→黄 / 通讯破坏→灰 ✓（FS 同款 ✓，一眼看出"这局任务做完没" ✓）
///
/// ⚠️ 全部写入都在**原版跑完之后**（Postfix ✓），且只碰颜色与文字 ✓ —— 不碰填充长度 ✓
/// ⚠️ 拿不到本地玩家/职业时**一律退回安全值** ✓（绝不抛异常：这根条每 `FixedUpdate` 都在跑 ✓）
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
[HarmonyPatch(typeof(ProgressTracker))]
public static class TaskProgressTrackerPatch
{
    /// <summary>标题原文（`Start` 时取一次 ✓ —— 原版会带翻译，改完再用它拼百分比 ✓）</summary>
    private static string TitleText = "";

    /// <summary>`_FullColor` 的 shader 属性 id（每帧用字符串查太浪费 ✓）</summary>
    private static readonly int FullColor = Shader.PropertyToID("_FullColor");

    /// <summary>当前显示的百分比（通讯破坏时保留上次值 ✓，但**不显示** ✓）</summary>
    private static float lastPercentage;

    [HarmonyPatch(nameof(ProgressTracker.Start))]
    [HarmonyPostfix]
    public static void ProgressTracker_Start(ProgressTracker __instance)
    {
        try
        {
            var t = FindTitle(__instance);
            if (t != null) TitleText = t.text;
        }
        catch (System.Exception ex) { LightInDark.Core.LightLogger.LogWarning($"[TaskBar] Start: {ex.Message}"); }
    }

    [HarmonyPatch(nameof(ProgressTracker.FixedUpdate))]
    [HarmonyPostfix]
    public static void ProgressTracker_FixedUpdate(ProgressTracker __instance)
    {
        try
        {
            var tmp = FindTitle(__instance);
            if (tmp == null) return;

            var renderer = __instance.TileParent;
            if (renderer == null || renderer.material == null) return;

            // ── 不在局内：百分比归零，颜色走安全值 ✓（大厅/结算时原版本来就把这条藏起来 ✓）
            if (!GameState.IsInGame)
            {
                lastPercentage = 0f;
                Paint(renderer, SafeColor());
                SetTitle(tmp, lastPercentage, comms: false, completed: false);
                return;
            }

            var local = LightPlayer.LocalPlayer;
            bool isImpostor = false;
            try
            {
                // ⚠️ `NetworkedPlayerInfo` **没有** `IsImpostor` ✗（我上一版猜错了 ✓）——
                //    身份在 `Data.Role`（`RoleBehaviour.IsImpostor` ✓，19.0 的判定入口 ✓）
                isImpostor = local != null && local.Control != null
                             && local.Control.Data != null && local.Control.Data.Role != null
                             && local.Control.Data.Role.IsImpostor;
            }
            catch { }

            // ── 百分比：按原版同一套"可见性规则"决定要不要刷新 ✓
            var gd = GameData.Instance;
            if (gd != null && gd.TotalTasks > 0)
            {
                float pct = gd.CompletedTasks / (float)gd.TotalTasks * 100f;

                bool visible = true;
                try
                {
                    switch (GameManager.Instance.LogicOptions.GetTaskBarMode())
                    {
                        case TaskBarMode.Normal: break;
                        case TaskBarMode.MeetingOnly: visible = MeetingHud.Instance != null; break;
                        case TaskBarMode.Invisible:
                        default: visible = false; break;
                    }
                }
                catch { visible = false; }

                if (visible) lastPercentage = pct;      // 不可见时保留旧值（照 FS ✓）
            }

            // ── 通讯破坏：★ 用**原版自己的判据**，不自己猜 API ✓✓
            //    原版 `ProgressTracker.FixedUpdate` 开头就是：
            //      if (PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(LocalPlayer))
            //          { TileParent.enabled = false; return; }        ← 通讯破坏时**整条被禁用**
            //    → 所以 `!renderer.enabled` **就是**"现在该藏数字" ✓（比另找 Comms 系统更稳 ✓）
            bool comms = !renderer.enabled;

            // ── 填充颜色（★ 这是"看起来没填充"的根治点 ✓）
            //    内鬼 → 按百分比红→黄→绿（和"进度"语义一致 ✓）
            //    船员 → 自己的职业颜色 ✓（拿不到就退回玩家颜色、再退回青色 ✓）
            var fill = isImpostor ? ColorByPercentage(lastPercentage) : RoleOrPlayerColor(local);
            Paint(renderer, comms ? UnityEngine.Color.gray : fill);

            // ── 标题：文字 + 颜色 ✓
            //    ⚠️ "任务是否做完"不猜 API ✓：直接用百分比判定 ✓（FS 用的是它自己的扩展 `TaskCompleted` ✗，
            //       我们没有对应物 ✓；用 `>= 99.95%` 表达"做完了"在语义上完全一致 ✓）
            bool completed = lastPercentage >= 99.95f;

            SetTitle(tmp, lastPercentage, comms, completed);
        }
        catch (System.Exception ex)
        {
            LightInDark.Core.LightLogger.LogWarning($"[TaskBar] FixedUpdate: {ex.Message}");
        }
    }

    // =====================================================================
    //  工具
    // =====================================================================

    private static TextMeshPro? FindTitle(ProgressTracker t)
    {
        try
        {
            var tr = t.gameObject.transform.FindChild("TitleText_TMP");
            return tr != null ? tr.GetComponent<TextMeshPro>() : null;
        }
        catch { return null; }
    }

    private static void Paint(MeshRenderer r, UnityEngine.Color c)
    {
        try { r.material.SetColor(FullColor, c); } catch { }
    }

    private static void SetTitle(TextMeshPro tmp, float pct, bool comms, bool completed)
    {
        try
        {
            tmp.text = comms ? $"{TitleText}(??.?%)" : $"{TitleText}({pct:F1}%)";
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = comms ? UnityEngine.Color.gray
                              : (completed ? UnityEngine.Color.green : UnityEngine.Color.yellow);
        }
        catch { }
    }

    /// <summary>船员 → 职业色 → 玩家色 → 青色（逐级兜底 ✓ 保证**永远看得见** ✓）</summary>
    private static UnityEngine.Color RoleOrPlayerColor(Player? local)
    {
        try
        {
            if (local != null && local.Role != null)
                // ⚠️ `LightInDark.Color.ToUnityColor` 不是扩展方法 ✗（我上一版猜错了 ✓）
                //    工程里的统一转换入口是 `ColorHelper.ToUnityColor(...)` ✓（IntroPatch 等处同款 ✓）
                return LightInDark.ColorHelper.ToUnityColor(local.Role.Color);
        }
        catch { }
        return SafeColor();
    }

    private static UnityEngine.Color SafeColor()
    {
        try
        {
            var local = LightPlayer.LocalPlayer;
            if (local != null) return LightInDark.ColorHelper.ToUnityColor(local.PlayerColor);
        }
        catch { }
        return UnityEngine.Color.cyan;
    }

    /// <summary>按百分比取色：0% 红 → 50% 黄 → 100% 绿（FS 的 `GetColorByPercentage` 同款 ✓）</summary>
    private static UnityEngine.Color ColorByPercentage(float pct)
    {
        float t = Mathf.Clamp01(pct / 100f);
        return t < 0.5f
            ? UnityEngine.Color.Lerp(UnityEngine.Color.red, UnityEngine.Color.yellow, t * 2f)
            : UnityEngine.Color.Lerp(UnityEngine.Color.yellow, UnityEngine.Color.green, (t - 0.5f) * 2f);
    }
}
