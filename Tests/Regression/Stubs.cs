global using Light.Utilities;
global using UColor = UnityEngine.Color;
using System.Collections;
using System.Collections.Concurrent;

namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object obj) { if (obj is GameObject go) go.Destroyed = true; }
        public static void DestroyImmediate(Object obj) => Destroy(obj);
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class GameObject : Object
    {
        private static int nextId;
        private readonly int id = ++nextId;
        private readonly Dictionary<Type, Component> components = new();
        public bool activeSelf = true, Destroyed;
        public bool activeInHierarchy => !Destroyed && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public string name;
        public int layer;
        public Transform transform;
        public GameObject(string name = "") { this.name = name; transform = new Transform { gameObject = this }; }
        public int GetInstanceID() => id;
        public void SetActive(bool value) => activeSelf = value;
        public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; components[typeof(T)] = c; return c; }
        public T GetComponent<T>() where T : Component => components.TryGetValue(typeof(T), out var c) ? (T)c : null;
    }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 localPosition, localScale = Vector3.one;
        public string name { get => gameObject.name; set => gameObject.name = value; }
        public Vector3 position { get => parent == null ? localPosition : parent.position + localPosition; set => localPosition = parent == null ? value : value - parent.position; }
        public void SetParent(Transform value, bool worldPositionStays = true) { var p = position; parent = value; if (worldPositionStays) position = p; }
        public Vector3 InverseTransformPoint(Vector3 world)=>world-position;
    }
    public struct Vector2(float x, float y) { public float x = x, y = y; }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float f) => new(a.x*f,a.y*f,a.z*f);
    }
    public struct Color(float r, float g, float b, float a = 1)
    {
        public float r=r,g=g,b=b,a=a;
        public static Color red => new(1,0,0);
        public static Color white => new(1,1,1);
    }
    public static class Mathf
    {
        public static float Abs(float x) => Math.Abs(x);
        public static float Min(float a, float b) => Math.Min(a,b);
        public static float Max(float a, float b) => Math.Max(a,b);
        public static int Max(int a, int b) => Math.Max(a,b);
        public static float Clamp(float a, float min, float max) => Math.Clamp(a,min,max);
        public static float Clamp01(float a) => Math.Clamp(a,0,1);
    }
    public static class Screen { public static int width = 1920, height = 1080; }
    public static class Time { public static float deltaTime=1f/60f, time, realtimeSinceStartup; public static int frameCount; }
    public class WaitForSeconds(float seconds) { public float Seconds = seconds; }
    public enum KeyCode { Tab }
    public static class Input { public static Vector3 mousePosition; public static bool Tab; public static bool GetKeyDown(KeyCode key)=>Tab; }
    public enum SpriteDrawMode { Sliced }
    public class Sprite : Object {}
    public class SpriteRenderer : Behaviour { public Sprite sprite; public SpriteDrawMode drawMode; public Color color; }
    public class Camera : Behaviour
    {
        public static Camera[] allCameras=[];
        public static Camera main=>allCameras.FirstOrDefault();
        public float depth;
        public int cullingMask=-1;
        public Vector3 ScreenToWorldPoint(Vector3 p)=>ViewportToWorldPoint(new(p.x/Screen.width,p.y/Screen.height,p.z));
        public Vector3 ViewportToWorldPoint(Vector3 p)=>new(transform.position.x+(p.x-0.5f)*6*Screen.width/Screen.height,transform.position.y+(p.y-0.5f)*6,transform.position.z+p.z);
        public Vector3 WorldToScreenPoint(Vector3 p)=>new((p.x-transform.position.x)/(6*Screen.width/Screen.height)*Screen.width+Screen.width/2f,(p.y-transform.position.y)/6*Screen.height+Screen.height/2f,p.z-transform.position.z);
    }
}
namespace TMPro { public class TextMeshPro : UnityEngine.Behaviour { public string text; public void ForceMeshUpdate(bool ignoreActiveState,bool forceTextReparsing){} } }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple=true)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(){} public HarmonyPatch(Type t){} public HarmonyPatch(string m){} public HarmonyPatch(Type t,string m){} }
    public class HarmonyPostfix : Attribute {}
    public class HarmonyPrefix : Attribute {}
    public class HarmonyPriority(int n) : Attribute {}
    public static class Priority { public const int Last=0, First=800; }
}
public class AspectPosition : UnityEngine.Behaviour {}
public class GridArrange : UnityEngine.Behaviour {}
public class MeetingHud : UnityEngine.Behaviour { public static MeetingHud Instance; }
public class ExileController { public static ExileController Instance; }
public class ActionButton : UnityEngine.Behaviour {}
public class HudManager : UnityEngine.Behaviour
{
    public static HudManager Instance;
    public ActionButton UseButton, PetButton, SabotageButton, ReportButton, ImpostorVentButton, KillButton;
    public Notifier Notifier = new();
    public void Update() {}
}
public class Notifier { public readonly List<string> Messages = new(); public void AddDisconnectMessage(string m) => Messages.Add(m); }
public class GameOptionsMenu : UnityEngine.Behaviour {}
public class NameText { public UnityEngine.Color color; public string text; }
public class StartButton { public bool Enabled=true; public string Text="Start"; public void SetButtonEnableState(bool e)=>Enabled=e; public void ChangeButtonText(string t)=>Text=t; }
public class GameStartManager
{
    public enum StartingStates { NotStarting, Countdown }
    public StartingStates startState;
    public StartButton StartButton = new();
    public NameText GameStartText = new();
    public void Update() {} public void BeginGame() {}
}
public class PlayerData { public string PlayerName="Player"; public bool Disconnected; }
public class Cosmetics { public NameText nameText=new(); public void SetNameColor(UnityEngine.Color c)=>nameText.color=c; }
public class PlayerControl : UnityEngine.Behaviour
{
    public static PlayerControl LocalPlayer;
    public static readonly List<PlayerControl> AllPlayerControls=new();
    public byte PlayerId;
    public uint NetId;
    public PlayerData Data=new();
    public Cosmetics cosmetics=new();
    public void OnDestroy() {}
}
public class ClientData { public string ProductUserId="account"; public PlayerControl Character; }
public class AmongUsClient
{
    public static AmongUsClient Instance;
    public bool AmHost=true;
    public int GameId=1, HostId;
    public readonly Dictionary<byte,ClientData> Clients=new();
    public ClientData GetClientFromCharacter(PlayerControl pc) => pc != null && Clients.TryGetValue(pc.PlayerId,out var c) ? c : null;
    public ClientData GetClient(int id) => Clients.TryGetValue((byte)id,out var c) ? c : null;
}
namespace Hazel
{
    public class MessageReader(params object[] values)
    {
        private int i;
        public byte ReadByte()=>(byte)values[i++]; public int ReadInt32()=>(int)values[i++]; public string ReadString()=>(string)values[i++];
    }
    public class MessageWriter
    {
        public readonly List<object> Values=new(); public void Write(byte x)=>Values.Add(x); public void Write(int x)=>Values.Add(x); public void Write(string x)=>Values.Add(x);
    }
}
namespace LightInDark.RPCs
{
    public static class CustomRPC
    {
        public static byte CurrentSender;
        public static readonly Dictionary<string,Action<Hazel.MessageReader>> Handlers=new();
        public static readonly List<(string Hash, object[] Values)> Sent=new();
        public static void Register(string h,Action<Hazel.MessageReader> f)=>Handlers[h]=f;
        public static void SendOnly(string h,Action<Hazel.MessageWriter> f,bool reliable=true) { var w=new Hazel.MessageWriter(); f(w); Sent.Add((h,w.Values.ToArray())); }
    }
    public static class RpcDefinitions { public static readonly List<byte> Kicked=new(); public static void KickPlayerWithReason(byte p,string r)=>Kicked.Add(p); }
}
namespace Light.Utilities
{
    public class Dispatcher
    {
        public static Dispatcher Instance = new();
        public readonly ConcurrentQueue<Action> Queue=new();
        public readonly List<IEnumerator> Coroutines=new();
        public void Enqueue(Action a)=>Queue.Enqueue(a);
        public void Drain() { while(Queue.TryDequeue(out var a)) a(); }
        public void StartCoroutine(IEnumerator c)=>Coroutines.Add(c);
    }
}
namespace BepInEx.Unity.IL2CPP.Utils.Collections { public static class Extensions { public static IEnumerator WrapToIl2Cpp(this IEnumerator c)=>c; } }
namespace BepInEx { public static class Paths { public static string PluginPath="."; } }
namespace LightInDark { public class LIDPlugin {} }
namespace Light
{
    public class Settings { public bool EnableHandshake=true; public string VerifyServerUrl="http://127.0.0.1"; public float HandshakeTimeoutSeconds=10; public int HandshakeMode; }
    public static class LightPlugin { public const string Version="regression"; public static Settings LightSettingsData=new(); }
}
namespace LightInDark.Core
{
    public static class LightLogger
    {
        public static readonly List<string> Errors=new();
        public static void Log(string m){} public static void LogDebug(string m){} public static void LogWarning(string m){}
        public static void LogError(string m,Exception e) { Errors.Add(m+": "+e); }
    }
}
namespace LightInDark.Events { public class GameHudUpdateEvent {} }
namespace LightInDark.Configuration
{
    public enum ConfigCategory { Mod, Crewmate, Impostor }
    public class ConfigItem { public string Key; public ConfigBlock Block; public bool IsVisible=true; public bool GetBool()=>false; public int GetInt()=>0; }
    public class ConfigBlock { public string Key; public ConfigCategory Category; public List<ConfigItem> Items=new(); }
    public static class ConfigRegistry { public static readonly List<ConfigBlock> Blocks=new(); public static ConfigItem Get(string key)=>null; }
}
