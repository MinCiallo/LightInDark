using System;
using HarmonyLib;
using LightInDark.Core;

namespace Light.Patches;

/// <summary>
/// **房主进房时自动加载"当前预设"（`Current.lidpreset`）** ✓
///
/// ═══════════════════════════════════════════════════════════════════════
/// 【2026-10-10 用户反馈改的时机 —— 这条很关键】
///   原实现挂在 **`GameStartManager.BeginGame`**（点开始游戏那一刻）✗
///   → 房主在大厅里刚调好的设置，按下"开始"时**被预设文件覆盖回去** ✗✗
///     表现就是："我改了半天，一开局又变回上次的" ✓（用户说"很影响体验" ✓）
///
///   用户要的语义 ✓：
///     · **房主改设置时 → 异步写进 Current.lidpreset**（这个已经由 `PresetStore` 的
///       防抖保存负责 ✓ 改完自动落盘 ✓）
///     · **刚开房时 → 加载这个预设** ✓（= 进大厅那一刻 ✓，而**不是**开局那一刻 ✗）
///
///   → 所以这里改成挂 **`LobbyBehaviour.Start`** ✓：
///     它是"进入大厅"的入口 ✓ —— 房主建房、玩家加入都会走 ✓（只房主执行 ✓）
///     此时加载一次 = "上次的改动"回来了 ✓；之后房主在大厅里改什么就**原样保留到开局** ✓✓
///
/// 【只房主做】
///   · 配置是房主权威的（同步走 `ConfigSync`）✓，客户端自己加载会**打架** ✗
///   · 加载失败**只警告、不阻止开局** ✓
///
/// ⚠️ 目标文件不存在时（第一次装的玩家）会自己说明 ✓，不是错误 ✓
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class AutoLoadPresetPatch
{
    /// <summary>本次进房是否已经加载过（Start 可能被调多次 ✓ 只做一次）</summary>
    private static int _lastLoadedFrame = -1;

    public static void Prefix()
    {
        try
        {
            // 只房主 ✓
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

            // 同一帧重复触发 → 跳过 ✓
            if (_lastLoadedFrame == UnityEngine.Time.frameCount) return;
            _lastLoadedFrame = UnityEngine.Time.frameCount;

            // 还有未落盘的改动 → 保留实时值，别用旧文件覆盖 ✓
            if (LightInDark.Configuration.PresetStore.HasPendingChanges)
            {
                LightLogger.LogVerbose("[AutoLoadPreset] 当前设置尚在保存，保留本次改动，不重读旧预设");
                return;
            }

            var path = LightInDark.Configuration.PresetStore.CurrentPath;
            if (!System.IO.File.Exists(path))
            {
                LightLogger.LogVerbose($"[AutoLoadPreset] 还没有当前预设（{path}）→ 跳过");
                return;
            }

            // 游戏模式是**这一局的选择**，不该被预设拽回去 ✓（与原实现一致）
            var modeItem = LightInDark.Configuration.ConfigRegistry.Get("lid.gameMode");
            int liveMode = modeItem?.GetInt() ?? 0;

            var (ok, msg) = LightInDark.Configuration.PresetStore.LoadFrom(path);
            modeItem?.SetValueSilently(liveMode);

            if (ok) LightLogger.Log($"[AutoLoadPreset] 进房时已加载上次的改动：{msg}");
            else LightLogger.LogWarning($"[AutoLoadPreset] 加载当前预设失败（不影响游戏）：{msg}");
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[AutoLoadPreset] {ex.Message}");   // ⚠️ catch 里别碰 ex.Message 之外的字段
        }
    }
}
