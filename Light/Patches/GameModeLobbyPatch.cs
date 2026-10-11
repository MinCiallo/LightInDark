using HarmonyLib;

namespace Light.Patches;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
public static class GameModeLobbyPatch
{
    public static void Prefix(GameStartManager __instance)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost) return;
        var mode = Config.GameModeConfigRegistrar.SelectedMode;
        if (mode != null) __instance.MinPlayers = DebugMode.Enabled ? 1 : mode.MinimumPlayers;
    }
}
