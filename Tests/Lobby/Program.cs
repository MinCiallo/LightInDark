using System.Reflection;
using Light.Config;
using Light.Handshake;
using Light.Patches;
using LightInDark.Configuration;
using LightInDark.Game;
using Mono.Cecil;
using UnityEngine;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
GameStartManager Reset(int players = 1)
{
    GameModeManager.Reset();
    AmongUsClient.Instance = new(); GameOptionsManager.Instance = new(); GameData.Instance = new() { PlayerCount = players };
    DebugMode.Enabled = false;
    HandshakeManager.IsEnabled = HandshakeManager.Pending = HandshakeManager.Unverified = false;
    Light.LightPlugin.LightSettingsData.HandshakeMode = 0;
    Time.frameCount++;
    var gsm = new GameStartManager();
    if (ConfigRegistry.Get(GameModeConfigRegistrar.Key) is { } item) item.SetValueSilently(0);
    PresetStore.FlushNow();
    return gsm;
}
void Frame(GameStartManager gsm)
{
    Time.frameCount++;
    HandshakeLobbyGatePatch.UpdatePrefix(gsm);
    GameModeLobbyPatch.UpdatePrefix(gsm);
    gsm.Update();
    GameModeLobbyPatch.UpdatePostfix(gsm);
    HandshakeLobbyGatePatch.UpdatePostfix(gsm);
}
void Begin(GameStartManager gsm)
{
    Time.frameCount++;
    AutoLoadPresetPatch.Prefix();
    bool allowed = HandshakeLobbyGatePatch.BeginGamePrefix(gsm);
    allowed &= GameModeLobbyPatch.BeginGamePrefix(gsm);
    if (allowed) gsm.BeginGame();
}
string directory = Path.Combine(Path.GetTempPath(), "lid-lobby-" + Guid.NewGuid().ToString("N"));
Application.persistentDataPath = directory;
try
{
    Reset();
    Check(GameModeConfigRegistrar.SelectedMode == null, "unregistered modes are safe during early lobby initialization");
    GameModeConfigRegistrar.Register();
    var item = ConfigRegistry.Get(GameModeConfigRegistrar.Key);
    Check(GameModeRegistry.Get(GameModeRegistry.FreePlay).MinimumPlayers == 0, "registered freeplay disables native minimum warning");
    Check(new GameModeDefinition("zero", "zero", () => new Light.Roles.Assignment.FreePlayRoleAllocator(), minimumPlayers: 0).MinimumPlayers == 0,
        "public API preserves a zero-player threshold");

    var legacy = Reset(); legacy.MinPlayers = 1; legacy.BeginGame();
    Check(legacy.Warning && legacy.startState == GameStartManager.StartingStates.NotStarting,
        "fixture reproduces the old solo minimum warning");
    var gsm = Reset(); Frame(gsm);
#if DEBUG
    const int standardMinimum = 1;
#else
    const int standardMinimum = 4;
#endif
    Check(gsm.MinPlayers == standardMinimum, "standard lobby retains build-specific minimum");
    item.Increase(); // Same UI path as changing the mode; no player joins/leaves.
    GameModeLobbyPatch.UpdatePrefix(gsm);
    Check(gsm.MinPlayers == 0 && gsm.LastPlayerCount == -1, "mode switch invalidates native player-count cache");
    Frame(gsm);
    Check(gsm.StartButton.Enabled && gsm.StartButton.Text == "Start" && gsm.StartButtonGlyph.Color.a == 1,
        "solo freeplay immediately enables button, label and controller glyph");
    Check(PresetStore.HasPendingChanges, "UI mode selection has a pending debounce write");
    Begin(gsm);
    Check(item.GetInt() == 1 && gsm.MinPlayers == 0 && !gsm.Warning && gsm.startState == GameStartManager.StartingStates.Countdown,
        "immediate click keeps live freeplay selection and enters native countdown without warning");
    Check(gsm.NativeBegins == 1 && gsm.NativeFinals == 0, "start still uses the native BeginGame entry point");
    Begin(gsm);
    Check(gsm.countDownTimer == 5, "repeated click does not restart an active countdown");
    for (int i = 0; i < 5; i++) Frame(gsm);
    Check(gsm.NativeFinals == 1 && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
        "countdown reaches native FinallyBegin exactly once");
    GameModeConfigRegistrar.SelectForRound();
    Check(GameModeManager.CurrentId == GameModeRegistry.FreePlay && !GameModeManager.AllowAutomaticEnd && GameModeManager.AllowRoleChanges,
        "starting preserves the freeplay allocator and runtime policy");

    gsm = Reset(); item.Increase(); Begin(gsm);
    Check(GameModeLobbyPatch.TrySkipCountdown(gsm) && gsm.NativeFinals == 1 && gsm.NativeBegins == 1,
        "skip completes native countdown rather than reentering BeginGame");
    Check(!GameModeLobbyPatch.TrySkipCountdown(gsm) && gsm.NativeFinals == 1,
        "double-clicking skip cannot start a second game");
    gsm = Reset(); item.Increase();
    Check(!GameModeLobbyPatch.TrySkipCountdown(gsm) && gsm.NativeBegins == 0 && gsm.NativeFinals == 0,
        "skip cannot bypass initial BeginGame checks");
    gsm = Reset(); item.Increase(); Begin(gsm); HandshakeManager.IsEnabled = HandshakeManager.Pending = true;
    Check(!GameModeLobbyPatch.TrySkipCountdown(gsm) && gsm.NativeFinals == 0,
        "skip rechecks players still awaiting handshake");
    gsm = Reset(); item.Increase(); Begin(gsm); GameData.Instance.PlayerCount = 0;
    Check(!GameModeLobbyPatch.TrySkipCountdown(gsm) && gsm.NativeFinals == 0,
        "skip refuses an empty lobby after players leave");
    gsm = Reset(); item.Increase(); Begin(gsm); AmongUsClient.Instance.AmHost = false;
    Check(!GameModeLobbyPatch.TrySkipCountdown(gsm) && gsm.NativeFinals == 0,
        "client cannot invoke the host countdown skip");

    gsm = Reset(); item.Increase(); PresetStore.FlushNow(); item.SetValueSilently(0);
    Begin(gsm);
    Check(item.GetInt() == 1 && gsm.startState == GameStartManager.StartingStates.Countdown,
        "persisted freeplay loads before final start eligibility, even without a HUD update");
    gsm = Reset(); item.Increase(); Frame(gsm); item.Decrease(); Frame(gsm);
    Check(gsm.MinPlayers == standardMinimum && gsm.StartButton.Enabled == (standardMinimum <= 1),
        "switching back without changing player count restores standard eligibility");
    gsm = Reset(); item.Increase(); DebugMode.Enabled = true; Frame(gsm);
    Check(gsm.MinPlayers == 0, "debug mode cannot restore the solo minimum warning in freeplay");
    gsm = Reset(0); item.Increase(); Frame(gsm); Begin(gsm);
    Check(!gsm.StartButton.Enabled && gsm.NativeBegins == 0, "zero threshold still requires an actual player");
    gsm = Reset(); item.Increase(); GameData.Instance = null;
    Check(!GameModeLobbyPatch.BeginGamePrefix(gsm), "uninitialized game data cannot start a lobby");

    gsm = Reset(2); item.Increase(); HandshakeManager.IsEnabled = HandshakeManager.Pending = true;
    Frame(gsm); Begin(gsm);
    Check(!gsm.StartButton.Enabled && gsm.NativeBegins == 0, "freeplay retains handshake pending gate for UI and direct calls");
    HandshakeManager.Pending = false; Frame(gsm); Begin(gsm);
    Check(gsm.StartButton.Enabled && gsm.startState == GameStartManager.StartingStates.Countdown,
        "completed handshake restores freeplay start without another join");
    gsm = Reset(2); item.Increase(); HandshakeManager.IsEnabled = HandshakeManager.Unverified = true;
    Light.LightPlugin.LightSettingsData.HandshakeMode = (int)HandshakeModeOption.Kick;
    Frame(gsm); Begin(gsm);
    Check(!gsm.StartButton.Enabled && gsm.NativeBegins == 0, "kick-mode rejected player cannot start freeplay");
    Light.LightPlugin.LightSettingsData.HandshakeMode = (int)HandshakeModeOption.Warn;
    Frame(gsm); Begin(gsm);
    Check(gsm.startState == GameStartManager.StartingStates.Countdown, "warn-only handshake still permits freeplay");

    gsm = Reset(); item.Increase(); AmongUsClient.Instance.AmHost = false;
    GameModeLobbyPatch.UpdatePrefix(gsm); GameModeLobbyPatch.UpdatePostfix(gsm);
    Check(gsm.MinPlayers == 4 && !gsm.StartButton.Enabled, "client does not change host start policy");
    gsm = Reset(); item.Increase(); GameOptionsManager.Instance.CurrentGameOptions.GameMode = AmongUs.GameOptions.GameModes.HideNSeek;
    GameModeLobbyPatch.UpdatePrefix(gsm); GameModeLobbyPatch.UpdatePostfix(gsm);
    Check(gsm.MinPlayers == 4 && !gsm.StartButton.Enabled, "hide-and-seek retains its native lobby policy");
    gsm = Reset(); item.Increase(); GameOptionsManager.Instance.HasOptions = false;
    GameModeLobbyPatch.UpdatePrefix(gsm);
    Check(gsm.MinPlayers == 4, "options-not-ready path leaves native setup intact");
    gsm = Reset(); item.Increase(); GameModeManager.BeginRound(); GameModeLobbyPatch.UpdatePrefix(gsm);
    Check(gsm.MinPlayers == 4, "lobby settings cannot change a frozen round");
    gsm = Reset(); item.Increase(); gsm.StartButton.Destroyed = gsm.StartButtonGlyph.Destroyed = true;
    GameModeLobbyPatch.UpdatePostfix(gsm);
    Check(!gsm.StartButton.Enabled, "destroyed Unity buttons and glyphs are not accessed");

    // A background write may already have cleared _dirty but still be writing the old snapshot.
    gsm = Reset(); item.SetValueSilently(1);
    var writing = typeof(PresetStore).GetField("_writing", BindingFlags.NonPublic | BindingFlags.Static);
    writing.SetValue(null, 1);
    try
    {
        Check(PresetStore.HasPendingChanges, "in-flight background write counts as a pending change");
        Begin(gsm);
        Check(item.GetInt() == 1 && gsm.startState == GameStartManager.StartingStates.Countdown,
            "start cannot reload stale data while background persistence is in flight");
    }
    finally { writing.SetValue(null, 0); }
    PresetStore.FlushNow();
    Check(!PresetStore.HasPendingChanges, "completed synchronous flush clears pending state");

    if (args.Length > 0)
    {
        using var module = ModuleDefinition.ReadModule(args[0]);
        var patch = module.Types.Single(t => t.FullName == "Light.Patches.GameModeLobbyPatch");
        Check(patch.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch"), "compiled lobby driver has class-level Harmony registration");
        foreach (var name in new[] { "StartPostfix", "UpdatePrefix", "UpdatePostfix", "BeginGamePrefix" })
            Check(patch.Methods.Single(m => m.Name == name).CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch"),
                "compiled lobby entry point: " + name);
        int PriorityOf(string typeName, string methodName) => (int)module.Types.Single(t => t.FullName == typeName).Methods.Single(m => m.Name == methodName)
            .CustomAttributes.Single(a => a.AttributeType.Name == "HarmonyPriority").ConstructorArguments[0].Value;
        Check(PriorityOf(patch.FullName, "UpdatePostfix") > PriorityOf("Light.Handshake.HandshakeLobbyGatePatch", "UpdatePostfix"),
            "compiled availability refresh runs before handshake disabling");
        Check(PriorityOf(patch.FullName, "BeginGamePrefix") == 0, "compiled start check runs after preset loading");
        Check(!module.Types.Any(t => t.Name == "DebugMinPlayersPatch"), "no legacy debug patch overwrites freeplay minimum");
    }
    Console.WriteLine($"Lobby regression: {checks} checks passed.");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
