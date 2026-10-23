using HarmonyLib;
using LightInDark.Core;
using LightInDark.Events;
using UnityEngine;

namespace Light.Patches;

/// <summary>
/// 通风管放行：职业模板覆写 CanUseVents 返回 true 即可钻通风管。
/// </summary>
internal static class RoleVent
{
    public static bool CanVent(byte playerId)
        => LightInDark.Game.GameManager.Instance?.GetPlayer(playerId)?.Role?.Role?.CanUseVents == true;
}

[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
public static class RoleVentCanUsePatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, ref bool canUse, ref bool couldUse)
    {
        if (canUse || pc?.Object == null) return;
        if (!RoleVent.CanVent(pc.PlayerId)) return;

        var player = pc.Object;
        couldUse = !pc.IsDead && (player.CanMove || player.inVent);
        if (!couldUse) return;

        Vector3 center = player.Collider.bounds.center;
        Vector3 position = __instance.transform.position;
        canUse = Vector2.Distance(center, position) <= __instance.UsableDistance
            && !PhysicsHelpers.AnythingBetween(player.Collider, center, position, Constants.ShipOnlyMask, false);
    }
}

[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.CanVent), MethodType.Getter)]
public static class RoleCanVentPatch
{
    public static void Postfix(RoleBehaviour __instance, ref bool __result)
    {
        if (__result || __instance?.Player == null) return;
        if (RoleVent.CanVent(__instance.Player.PlayerId)) __result = true;
    }
}

internal static class RoleVentButtonTicker
{
    public static void OnHudUpdate(GameHudUpdateEvent ev)
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || !RoleVent.CanVent(local.PlayerId)) return;

            var button = HudManager.Instance?.ImpostorVentButton;
            if (button == null) return;
            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);

            var vent = FindNearestVent(local, out float nearest, out Vent nearestVent);
            button.SetTarget(vent);

            if (vent != null) button.SetEnabled();
            else button.SetDisabled();

            // ⚠️ 2026-10-10 用户："游戏内不要打那么多日志，太刷屏了" ✓
            //    这里原来每条帧都打一条 `[Vent]`（实测一局 1 万多条 ✗）→ 已删除 ✓
            //    要查通风管逻辑时把下面打开（verbose 通道，默认静默 ✓）：
            // if (LightLogger.VerboseEnabled)
            //     LightLogger.LogVerbose($"[Vent] me={local.transform.position} nearest={nearest:0.00} target={(vent == null ? "null" : vent.Id.ToString())}");
        }
        catch { }
    }

    private static Vent FindNearestVent(PlayerControl local, out float nearest, out Vent nearestVent)
    {
        nearest = float.MaxValue;
        nearestVent = null;

        var ship = ShipStatus.Instance;
        if (ship?.AllVents == null || local == null) return null;

        var pos = (Vector2)local.transform.position;

        foreach (var vent in ship.AllVents)
        {
            if (vent == null) continue;

            float distance = Vector2.Distance(pos, (Vector2)vent.transform.position);
            if (distance >= nearest) continue;

            nearest = distance;
            nearestVent = vent;
        }

        return nearestVent != null && nearest <= nearestVent.UsableDistance ? nearestVent : null;
    }
}
