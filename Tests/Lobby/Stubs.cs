// Only engine/network boundaries are simulated; configuration, presets, modes and lobby patches are production code.
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type) { }
        public HarmonyPatch(string name) { }
        public HarmonyPatch(Type type, string name) { }
    }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public class HarmonyPriority(int priority) : Attribute { public int Value = priority; }
    public static class Priority { public const int First = 800, Last = 0; }
}
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b)
            || (ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public struct Color(float r, float g, float b, float a = 1) { public float r = r, g = g, b = b, a = a; }
    public static class Time { public static int frameCount; }
    public static class Application { public static string persistentDataPath, version = "19.0"; }
}
namespace InnerNet
{
    public class InnerNetClient { public enum GameStates { NotJoined, Joined, Started, Ended } }
}
namespace AmongUs.GameOptions
{
    public enum GameModes { Normal, HideNSeek }
    public class Options { public GameModes GameMode; }
}
public class AmongUsClient : UnityEngine.Object
{
    public static AmongUsClient Instance;
    public bool AmHost = true;
    public InnerNet.InnerNetClient.GameStates GameState = InnerNet.InnerNetClient.GameStates.Joined;
}
public class GameOptionsManager : UnityEngine.Object
{
    public static GameOptionsManager Instance;
    public bool HasOptions = true;
    public AmongUs.GameOptions.Options CurrentGameOptions = new();
}
public class GameData : UnityEngine.Object { public static GameData Instance; public int PlayerCount = 1; }
public enum StringNames { StartLabel, WaitingForPlayers }
public class TranslationController : UnityEngine.Object
{
    public static TranslationController Instance = new();
    public string GetString(StringNames name) => name == StringNames.StartLabel ? "Start" : "Waiting";
}
public static class Palette
{
    public static UnityEngine.Color EnabledColor = new(1, 1, 1), DisabledClear = new(0, 0, 0, 0);
}
public class Glyph : UnityEngine.Object { public UnityEngine.Color Color; public void SetColor(UnityEngine.Color color) => Color = color; }
public class StartButton : UnityEngine.Object
{
    public bool Enabled;
    public string Text;
    public void SetButtonEnableState(bool enabled) => Enabled = enabled;
    public void ChangeButtonText(string text) => Text = text;
}
public class Text : UnityEngine.Object { public string text; }
public class GameStartManager : UnityEngine.Object
{
    public enum StartingStates { NotStarting, Countdown, Starting }
    public StartingStates startState;
    public int MinPlayers = 4, LastPlayerCount = -1, NativeBegins, NativeFinals;
    public bool Warning, ShowMinPlayerWarning = true;
    public float countDownTimer;
    public StartButton StartButton = new();
    public Glyph StartButtonGlyph = new();
    public Text GameStartText = new();
    public void Start() { }
    // Fixture models the native count cache, warning and countdown contract, not native IL2CPP execution.
    public void Update()
    {
        if (LastPlayerCount != GameData.Instance.PlayerCount)
        {
            LastPlayerCount = GameData.Instance.PlayerCount;
            StartButton.SetButtonEnableState(LastPlayerCount >= MinPlayers);
        }
        if (startState == StartingStates.Countdown && --countDownTimer <= 0) FinallyBegin();
    }
    public void BeginGame()
    {
        NativeBegins++;
        if (startState != StartingStates.NotStarting || GameData.Instance.PlayerCount < MinPlayers) return;
        if (ShowMinPlayerWarning && GameData.Instance.PlayerCount == MinPlayers) { Warning = true; return; }
        ReallyBegin();
    }
    public void ReallyBegin() { startState = StartingStates.Countdown; countDownTimer = 5; }
    public void FinallyBegin()
    {
        NativeFinals++;
        startState = StartingStates.Starting;
        AmongUsClient.Instance.GameState = InnerNet.InnerNetClient.GameStates.Started;
    }
}
public class PlayerControl : UnityEngine.Object
{
    public static readonly List<PlayerControl> AllPlayerControls = new();
    public byte PlayerId;
    public void OnDestroy() { }
}
namespace Light
{
    public class Settings { public int HandshakeMode; }
    public static class LightPlugin { public static Settings LightSettingsData = new(); }
}
namespace Light.Patches { public static class DebugMode { public static bool Enabled; } }
namespace Light.Handshake
{
    public static class HandshakeManager
    {
        public static bool IsEnabled, Pending, Unverified;
        public static bool HasPending() => Pending;
        public static bool HasUnverified() => Unverified;
        public static bool IsUnverified(byte id) => Unverified;
        public static void Tick() { }
        public static void MarkNameRed(byte id) { }
        public static void RestoreNameColor(byte id) { }
        public static void CleanupPlayer(byte id) { }
    }
}
namespace LightInDark.Core
{
    public static class LightLogger
    {
        public static void Log(string value) { }
        public static void LogWarning(string value) { }
        public static void LogError(string value, Exception error) => throw new Exception(value, error);
    }
}
namespace LightInDark.Roles { public interface IRoleAllocator { void Assign(List<byte> impostors, List<byte> others); } }
namespace Light.Roles.Assignment
{
    public class StandardRoleAllocator : LightInDark.Roles.IRoleAllocator { public void Assign(List<byte> impostors, List<byte> others) { } }
    public class FreePlayRoleAllocator : LightInDark.Roles.IRoleAllocator { public void Assign(List<byte> impostors, List<byte> others) { } }
}
namespace LightInDark.Game
{
    public static class EndGameManager
    {
        public static Func<bool> AutomaticEndBlocked;
        public static void OnNewGameStart() { }
    }
}
namespace LightInDark.RPCs
{
    public static class RpcDefinitions { public static void SyncGameMode(string id) => LightInDark.Game.GameModeManager.ApplyRemote(id); }
}
namespace LightInDark.Events
{
    public static class EventTriggers
    {
        public static int SkipEvents;
        public static void OnLobbySkipCountdown(int count) => SkipEvents++;
    }
}
