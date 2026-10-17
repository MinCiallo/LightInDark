using System;
using HarmonyLib;
using LightInDark.Core;
using UnityEngine;

namespace Light.Patches;

/// <summary>
/// **地图染色**（2026-10-16 用户要求 ✓："fs 改了 map 颜色，你也改一下，按职业颜色/玩家颜色来，
/// 让玩家在 option 改 职业/玩家/原版，默认职业" ✓）
///
/// 参考：FinalSuspect `DataHandling\FinalLocalHandling.cs` 的 `ShowMap`（L152-189 ✓），
///       由 `Patches\Game_Vanilla\MapRealTimeLocationPatch.cs:12` 挂在 `MapBehaviour.Show` 后面 ✓：
/// <code>
///   var color = RoleHelper.GetRoleColor(roleType);         // 职业色 ✓
///   switch (opts.Mode) {
///       case CountOverlay: color = Palette.AcceptedGreen; break;   // 管理员桌 → 接受绿 ✓
///       case Sabotage:     color = Palette.DisabledGrey;  break;   // 破坏视图 → 灰 ✓
///   }
///   map.ColorControl.SetColor(color);                      // ★ 真正染色的是 ColorControl ✓
/// </code>
///
/// ⚠️ **两个功能性配色永远照旧** ✓（本实现比 FS 更保守 ✓）：
///    · `CountOverlay`（管理员桌/人数视图 ✓）与 `Sabotage`（破坏视图 ✓）**一律不动** ✓ ——
///      这两个视图的颜色本身携带信息 ✓（绿色=人数、灰色=破坏 ✓），染成职业色会让玩家误读 ✗
///    · 其余情况（普通地图 ✓）才按配置染色 ✓
///
/// ⚠️ 配置项 `lid.hud.mapColor`：**0 = 职业色（默认 ✓）· 1 = 玩家色 · 2 = 原版（不染色 ✓）**
/// </summary>
[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
public static class MapColorPatch
{
    /// <summary>地图配色来源：0 = 职业色（默认）· 1 = 玩家色 · 2 = 原版不染</summary>
    public const string ConfigKey = "lid.hud.mapColor";

    public static void Postfix(MapBehaviour __instance, [HarmonyArgument(0)] MapOptions opts)
    {
        try
        {
            if (__instance == null) return;

            int mode = 0;
            // ★ 单一真相来源：**LightSettings.MapColor** ✓（设置菜单 → Light 页签里改 ✓）
            //   —— 和"鼠标样式 / 会议任务面板 / 握手"走同一条路 ✓，不再另立配置项 ✗
            try { mode = LightPlugin.LightSettingsData?.MapColor ?? 0; }
            catch { mode = 0; }

            if (mode == 2) return;                     // 原版 → 一个字都不写 ✓

            // 功能性视图不染（颜色本身是信息 ✓）
            try
            {
                if (opts.Mode == MapOptions.Modes.CountOverlay) return;
                if (opts.Mode == MapOptions.Modes.Sabotage) return;
            }
            catch { }

            var color = mode == 1 ? PlayerColor() : RoleColor();
            if (color == null) return;

            var control = __instance.ColorControl;
            if (control == null) return;

            control.SetColor(color.Value);             // ★ FS 同款入口 ✓
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[MapColorPatch] {ex.Message}");
        }
    }

    /// <summary>职业色（拿不到职业 → 退回玩家色 ✓，保证地图总有颜色 ✓）</summary>
    private static Color? RoleColor()
    {
        try
        {
            var local = LightInDark.Game.LightPlayer.LocalPlayer;
            if (local != null && local.Role != null)
                return LightInDark.ColorHelper.ToUnityColor(local.Role.Color);
        }
        catch { }
        return PlayerColor();
    }

    /// <summary>玩家色（拿不到 → null = 不染 ✓）</summary>
    private static Color? PlayerColor()
    {
        try
        {
            var local = LightInDark.Game.LightPlayer.LocalPlayer;
            if (local != null) return LightInDark.ColorHelper.ToUnityColor(local.PlayerColor);
        }
        catch { }
        return null;
    }
}
