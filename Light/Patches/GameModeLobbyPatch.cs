using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using LightInDark.Core;
using LightInDark.Events;
using LightInDark.Game;

namespace Light.Patches;

/// <summary>Keep lobby availability and the native start checks on the same mode policy.</summary>
[HarmonyPatch(typeof(GameStartManager))]
public static class GameModeLobbyPatch
{
    private static GameModeDefinition LobbyMode()
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || client.GameState != InnerNetClient.GameStates.Joined
            || GameModeManager.IsRoundActive) return null;
        var options = GameOptionsManager.Instance;
        if (options == null || !options.HasOptions || options.CurrentGameOptions.GameMode != GameModes.Normal)
            return null;
        return Config.GameModeConfigRegistrar.SelectedMode;
    }

    private static int Minimum(GameModeDefinition mode)
    {
        int minimum = mode.MinimumPlayers;
        if (DebugMode.Enabled) minimum = System.Math.Min(minimum, 1);
#if DEBUG
        minimum = System.Math.Min(minimum, 1);
#endif
        return minimum;
    }

    private static void ApplyMinimum(GameStartManager instance, GameModeDefinition mode)
    {
        int minimum = Minimum(mode);
        if (instance.MinPlayers == minimum) return;
        instance.MinPlayers = minimum;
        // Vanilla can cache button/counter state by player count. A mode change must invalidate it too.
        instance.LastPlayerCount = -1;
    }

    [HarmonyPatch(nameof(GameStartManager.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(GameStartManager __instance)
    {
        var mode = LobbyMode();
        if (mode != null) ApplyMinimum(__instance, mode);
    }

    [HarmonyPatch(nameof(GameStartManager.Update))]
    [HarmonyPrefix]
    public static void UpdatePrefix(GameStartManager __instance)
    {
        var mode = LobbyMode();
        if (mode != null) ApplyMinimum(__instance, mode);
    }

    [HarmonyPatch(nameof(GameStartManager.Update))]
    [HarmonyPostfix]
    // Run before the handshake gate, which has Priority.First and may disable the button afterwards.
    [HarmonyPriority(Priority.First + 1)]
    public static void UpdatePostfix(GameStartManager __instance)
    {
        var mode = LobbyMode();
        var data = GameData.Instance;
        if (mode == null || data == null) return;
        ApplyMinimum(__instance, mode);
        if (__instance.startState != GameStartManager.StartingStates.NotStarting) return;
        bool canStart = data.PlayerCount > 0 && data.PlayerCount >= Minimum(mode);
        if (__instance.StartButton != null)
        {
            __instance.StartButton.SetButtonEnableState(canStart);
            var translation = TranslationController.Instance;
            if (translation != null)
                __instance.StartButton.ChangeButtonText(translation.GetString(
                    canStart ? StringNames.StartLabel : StringNames.WaitingForPlayers));
        }
        if (__instance.StartButtonGlyph != null)
            __instance.StartButtonGlyph.SetColor(canStart ? Palette.EnabledColor : Palette.DisabledClear);
    }

    [HarmonyPatch(nameof(GameStartManager.BeginGame))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static bool BeginGamePrefix(GameStartManager __instance)
    {
        // AutoLoadPreset runs first: the final selection must also govern a click before the next Update.
        var mode = LobbyMode();
        if (mode == null) return true;
        ApplyMinimum(__instance, mode);
        var data = GameData.Instance;
        int count = data != null ? data.PlayerCount : 0;
        bool canStart = count > 0 && count >= Minimum(mode);
        LightLogger.Log($"[GameMode] 开局检查：{mode.Id}，人数={count}，最低人数={__instance.MinPlayers}，状态={__instance.startState}，允许={canStart}");
        // Retain vanilla BeginGame -> ReallyBegin -> countdown -> FinallyBegin, including the handshake gate.
        return canStart;
    }

    internal static bool TrySkipCountdown(GameStartManager instance)
    {
        var client = AmongUsClient.Instance;
        var data = GameData.Instance;
        if (instance == null || client == null || !client.AmHost
            || client.GameState != InnerNetClient.GameStates.Joined || data == null
            || instance.startState != GameStartManager.StartingStates.Countdown) return false;
        var mode = LobbyMode();
        if (mode != null) ApplyMinimum(instance, mode);
        if (data.PlayerCount <= 0 || data.PlayerCount < instance.MinPlayers) return false;
        if (!Handshake.HandshakeLobbyGatePatch.BeginGamePrefix(instance)) return false;
        EventTriggers.OnLobbySkipCountdown(data.PlayerCount);
        // BeginGame only starts a countdown. Finish it through the same entry point as native Update/NoS.
        instance.FinallyBegin();
        return true;
    }
}
