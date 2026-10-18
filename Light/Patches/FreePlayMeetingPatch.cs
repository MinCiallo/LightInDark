using HarmonyLib;
using LightInDark.Game;

namespace Light.Patches;

internal static class MeetingStartFlag
{
    public static bool Called;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
public static class FreePlayMeetingFallbackPatch
{
    public static void Prefix() => MeetingStartFlag.Called = false;

    public static void Postfix(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        try
        {
            if (MeetingStartFlag.Called) return;
            if (!GameModeManager.AllowRoleChanges) return;
            if (MeetingHud.Instance != null) return;

            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (__instance.Data == null || __instance.Data.IsDead || __instance.inVent) return;

            var room = MeetingRoomManager.Instance;
            var hud = HudManager.Instance;
            if (room == null || hud == null) return;

            room.AssignSelf(__instance, target);
            hud.OpenMeetingRoom(__instance);
            __instance.RpcStartMeeting(target);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcStartMeeting))]
public static class RpcStartMeetingFlagPatch
{
    public static void Prefix() => MeetingStartFlag.Called = true;
}
