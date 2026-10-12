using AmongUs.GameOptions;
using HarmonyLib;
using LightInDark.Core;
using LightInDark.Game;
using ModEnd = LightInDark.Game.EndGameManager;

namespace Light.Patches;

[HarmonyPatch(typeof(global::GameManager), nameof(global::GameManager.RpcEndGame))]
public static class NeutralWinGatePatch
{
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(GameOverReason endReason)
    {
        if (ModEnd.IsEnding) return true;
        if (!UsesCustomVictory()) return true;
        if (!GameModeManager.AllowAutomaticEnd || DebugMode.Enabled) return false;
        if (ModEnd.Result != null) return false;
        var result = VictoryRuntime.Evaluate(ModEnd.FromVanillaReason(endReason));
        if (result != null) ModEnd.TryEndGame(result);
        return false;
    }

    internal static bool UsesCustomVictory()
    {
        var options = GameOptionsManager.Instance;
        return options != null && options.CurrentGameOptions != null
            && options.CurrentGameOptions.GameMode == GameModes.Normal;
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class VictoryUpdatePatch
{
    public static void Postfix()
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || client.GameState != InnerNet.InnerNetClient.GameStates.Started
            || !ModEnd.IsGameActive || ModEnd.Result != null || !GameModeManager.AllowAutomaticEnd
            || DebugMode.Enabled || !NeutralWinGatePatch.UsesCustomVictory()
            || IntroCutscene.Instance != null || MeetingHud.Instance != null || ExileController.Instance != null) return;
        try
        {
            var result = VictoryRuntime.Evaluate();
            if (result != null) ModEnd.TryEndGame(result);
        }
        catch (System.Exception ex) { LightLogger.LogError("[VictoryUpdate]", ex); }
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
public static class GameModeResetPatch
{
    public static void Postfix()
    {
        GameModeManager.Reset();
        ModEnd.Reset();
        LightInDark.UI.Ability.RoleButtonManager.Clear();
    }
}
