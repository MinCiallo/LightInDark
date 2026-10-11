using System.Reflection;
using LightInDark.Configuration;
using UnityEngine;

// Managed simulation of the Unity/IL2CPP boundary. Production button/rule/mode code is linked unchanged.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public string name;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b) ||
            (ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object obj)
        {
            if (ReferenceEquals(obj, null)) return;
            obj.Destroyed = true;
            if (obj is GameObject go)
            {
                foreach (var c in go.Components) c.Destroyed = true;
                foreach (var child in go.transform.Children.ToArray()) Destroy(child.gameObject);
            }
        }
        public static T Instantiate<T>(T source, Transform parent) where T : Object
        {
            var go = source is GameObject g ? g : ((Component)(Object)source).gameObject;
            var clone = go.Clone();
            clone.transform.SetParent(parent, false);
            return source is GameObject ? (T)(Object)clone : clone.Components.OfType<T>().First();
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T GetComponentInChildren<T>(bool inactive = false) where T : Component => gameObject.GetComponentInChildren<T>(inactive);
        public T[] GetComponentsInChildren<T>(bool inactive = false) where T : Component => gameObject.GetComponentsInChildren<T>(inactive);
        public T TryCast<T>() where T : class => this as T;
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public Transform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy => !Destroyed && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public int layer;
        public GameObject(string value = "") { name = value; transform = new Transform { gameObject = this }; Components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() => (T)AddComponent(typeof(T));
        private Component AddComponent(Type type) { var c = (Component)Activator.CreateInstance(type); c.gameObject = this; Components.Add(c); return c; }
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault(c => c != null);
        public T GetComponentInChildren<T>(bool inactive = false) where T : Component => GetComponentsInChildren<T>(inactive).FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive = false) where T : Component => Components.OfType<T>()
            .Concat(transform.Children.SelectMany(t => t.gameObject.GetComponentsInChildren<T>(inactive))).Where(c => c != null).ToArray();
        public void SetActive(bool active) => activeSelf = active;
        internal GameObject Clone()
        {
            var copy = new GameObject(name) { layer = layer, activeSelf = activeSelf };
            copy.transform.localPosition = transform.localPosition;
            copy.transform.localScale = transform.localScale;
            foreach (var component in Components.Where(c => c is not Transform))
            {
                var c = copy.AddComponent(component.GetType());
                foreach (var f in component.GetType().GetFields())
                    if (f.Name != "gameObject") f.SetValue(c, f.GetValue(component));
                c.gameObject = copy;
            }
            foreach (var child in transform.Children) child.gameObject.Clone().transform.SetParent(copy.transform, false);
            var action = copy.GetComponent<ActionButton>();
            if (action != null) action.BindChildren();
            return copy;
        }
    }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> Children = new();
        public Vector3 localPosition, localScale = Vector3.one, position;
        public Quaternion localRotation;
        public int childCount => Children.Count;
        public void SetParent(Transform p, bool world = true) { parent?.Children.Remove(this); parent = p; p?.Children.Add(this); }
        public Transform GetChild(int index) => Children[index];
        public Transform Find(string name) => Children.FirstOrDefault(c => c.gameObject.name == name);
    }
    public struct Vector2(float x, float y)
    {
        public float x = x, y = y;
        public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
        public static implicit operator Vector2(Vector3 a) => new(a.x, a.y);
    }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => new(0,0,0);
        public static Vector3 one => new(1,1,1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b,a.y*b,a.z*b);
    }
    public struct Vector4 { public float x, y, z, w; }
    public struct Quaternion { public static Quaternion identity => new(); }
    public struct Color(float r, float g, float b, float a)
    {
        public float r = r, g = g, b = b, a = a;
        public static Color white => new(1,1,1,1);
    }
    public class Sprite : Object { public Vector2[] uv = [new(0,0),new(1,1)]; }
    public class Material : Object
    {
        public Material(Material source) { }
        public Material(Shader source) { }
        public Vector4 GetVector(string name) => new();
        public void SetColor(string name, Color value) { }
    }
    public class Shader : Object { public static Shader Find(string name) => new(); }
    public class SpriteRenderer : Behaviour
    {
        public Sprite sprite = new();
        public Material material = new(new Shader());
        public Color color = Color.white;
        public int sortingLayerID, sortingOrder;
    }
    public enum KeyCode { None, Q, F, G }
    public static class Time { public static int frameCount; public static float deltaTime = 0.1f, unscaledTime; }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static readonly HashSet<KeyCode> Pressed = new();
        public static bool GetKeyDown(KeyCode key) => Pressed.Contains(key);
        public static bool GetMouseButtonDown(int button) => false;
    }
    public static class Mathf { public static int CeilToInt(float value) => (int)Math.Ceiling(value); }
    public class Camera : Component { public static Camera main; public Vector3 WorldToScreenPoint(Vector3 p) => p; }
}
namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public class UnityEvent
    {
        public Action Callback;
        public void AddListener(UnityAction callback) => Callback += () => callback();
        public void Invoke() => Callback?.Invoke();
    }
}
namespace UnityEngine.UI { public class Button { public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { } } }
namespace TMPro
{
    public enum FontStyles { Normal, Bold }
    public class TextMeshPro : Behaviour
    {
        public string text;
        public Color color;
        public bool enableAutoSizing;
        public FontStyles fontStyle;
        public void SetText(string value) => text = value;
        public void SetSharedMaterial(Material material) { }
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyPatch(params object[] values) : Attribute { }
    public class HarmonyPriority(int priority) : Attribute { }
    public static class Priority { public const int High = 600; }
}
namespace AmongUs.GameOptions { public enum RoleTypes { Shapeshifter, Engineer } }
namespace LightInDark.Core
{
    public static class LightLogger
    {
        public static readonly List<string> Errors = new();
        public static void Log(string text) { }
        public static void LogWarning(string text) => Errors.Add(text);
        public static void LogError(string text, Exception error) => Errors.Add(text + ": " + error.Message);
    }
}
namespace LightInDark.Audio { public static class SfxManager { public static void Play(string path) { } public static void Warmup(string path) { } } }
namespace LightInDark.Language { public static class Language { public static string GetStringOrKey(string key, string fallback) => fallback; } }
public class PassiveButton : Behaviour
{
    public UnityEngine.UI.Button.ButtonClickedEvent OnClick = new();
    public UnityEngine.Events.UnityEvent OnMouseOver = new(), OnMouseOut = new();
}
public class ActionButton : Behaviour
{
    public SpriteRenderer graphic;
    public SpriteRenderer usesRemainingSprite;
    public TMPro.TextMeshPro usesRemainingText;
    public TMPro.TextMeshPro buttonLabelText, cooldownTimerText;
    public float LastCooldown, Fill;
    public int Uses;
    public bool CanInteract() => gameObject.activeInHierarchy;
    public void SetUsesRemaining(int count) => Uses = count;
    public void SetInfiniteUses() => Uses = -1;
    public void SetEnabled() { if (buttonLabelText != null) buttonLabelText.color = Color.white; }
    public void SetDisabled() { }
    public void SetCoolDown(float remaining, float max) { LastCooldown = remaining; Fill = remaining/max; }
    public void SetCooldownFill(float fill) => Fill = fill;
    public void OverrideText(string value) { if (buttonLabelText != null) buttonLabelText.text = value; }
    public void BindChildren()
    {
        graphic = gameObject.GetComponentInChildren<SpriteRenderer>(true);
        var labels = gameObject.GetComponentsInChildren<TMPro.TextMeshPro>(true);
        buttonLabelText = labels.FirstOrDefault(); cooldownTimerText = labels.Skip(1).FirstOrDefault();
    }
}
public static class CooldownHelpers { public static void SetCooldownNormalizedUvs(this SpriteRenderer renderer) { } }
public class AbilitySettings { public Material FontMaterial; }
public class RoleBehaviour : Behaviour { public AbilitySettings Ability = new(); public bool IsImpostor, TasksCountTowardProgress = true; }
public class RoleManager : Behaviour
{
    public static RoleManager Instance;
    public RoleBehaviour GetRole(AmongUs.GameOptions.RoleTypes type) => new();
}
public enum ImageNames { UseButton, PolusAdminButton }
public class UseButton : ActionButton { public Dictionary<ImageNames, AbilitySettings> fastUseSettings = new() { [ImageNames.UseButton] = new(), [ImageNames.PolusAdminButton] = new() }; }
public class AspectPosition : Behaviour { }
public class TextTranslatorTMP : Behaviour { }
public class Minigame : Behaviour
{
    public static Minigame Instance;
    public object MyNormTask;
}
public class SwitchMinigame : Minigame { }
public class IDoorMinigame : Minigame { }
public class VitalsMinigame : Minigame { }
public class IntroCutscene : Behaviour { public static IntroCutscene Instance; }
public class ExileController : Behaviour { public static ExileController Instance; }
public class PlayerCustomizationMenu : Behaviour { public static PlayerCustomizationMenu Instance; }
public class MapBehaviour : Behaviour { public static MapBehaviour Instance; public bool IsOpen; }
public class ChatText { public bool hasFocus; }
public class ChatField { public ChatText textArea = new(); }
public class ChatController { public ChatField freeChatField = new(); }
public class HudManager : Behaviour
{
    public static HudManager Instance;
    public ActionButton AbilityButton, SecondaryAbilityButton, KillButton;
    public UseButton UseButton;
    public bool IsIntroDisplayed;
    public ChatController Chat = new();
    public void Start() { }
    public void Update() { }
    public void SetHudActive(PlayerControl player, RoleBehaviour role, bool active) { }
}
public class PlayerVoteArea : Behaviour { public byte PlayerId; public bool AmDead; public GameObject Buttons; }
public class MeetingHud : Behaviour
{
    public static MeetingHud Instance;
    public PlayerVoteArea[] playerStates;
    public enum MeetingStates { Animating, Discussion, NotVoted, Voted, Results, Proceeding }
    public MeetingStates state = MeetingStates.Discussion;
    public void Start() { }
    public void OnDestroy() { }
}
public class PlayerTask { public bool Complete; }
public class NetworkedPlayerInfo { public bool IsDead, Disconnected; public RoleBehaviour Role = new(); public List<PlayerTask> Tasks = new(); }
public class PlayerControl : Behaviour
{
    public static PlayerControl LocalPlayer;
    public static List<PlayerControl> AllPlayerControls = new();
    public byte PlayerId;
    public NetworkedPlayerInfo Data = new();
    public bool CanMove = true, IsKillTimerEnabled = true, AmOwner, inVent, shapeshifting, moveable = true, isDummy;
    public void FixedUpdate() { }
}
namespace InnerNet { public class InnerNetClient { public enum GameStates { NotJoined, Joined, Started, Ended } } }
public class AmongUsClient
{
    public static AmongUsClient Instance = new();
    public bool AmHost = true;
    public InnerNet.InnerNetClient.GameStates GameState = InnerNet.InnerNetClient.GameStates.Started;
}
public enum GameOverReason { CrewmatesByTask, CrewmatesByVote, ImpostorsBySabotage, ImpostorsByVote, ImpostorsByKill, ImpostorDisconnect, CrewmateDisconnect }
public class GameManager
{
    public static GameManager Instance = new();
    public int Ends;
    public GameOverReason LastReason;
    public void RpcEndGame(GameOverReason reason, bool showAd) { Ends++; LastReason = reason; }
}
namespace LightInDark.Game
{
    public class Player
    {
        public PlayerControl Control;
        public LightInDark.Roles.RuntimeRoleTemplate Role;
        public LightInDark.Roles.RoleTemplate VictoryRole;
        public bool AmOwner => Control.AmOwner;
        public bool IsLocal => Control == PlayerControl.LocalPlayer;
        public bool IsDead => Control == null || Control.Data.IsDead;
    }
    public class GameManager
    {
        public static GameManager Instance = new();
        public Player LocalPlayer;
        public List<Player> AllPlayers = new();
        public Player GetPlayer(byte id) => AllPlayers.FirstOrDefault(p => p.Control.PlayerId == id);
        public void Update() { }
    }
}
namespace LightInDark.Roles
{
    public class RoleTemplate
    {
        public string CodeName = "test";
        public RoleCategory RoleCategory = RoleCategory.Crewmate;
        public string VictoryTeamCode => RoleCategory == RoleCategory.Neutral ? CodeName : RoleCategory.ToString();
        public bool CountsTasksTowardVictory => RoleCategory == RoleCategory.Crewmate;
        public bool BlocksParityVictory;
        public LightInDark.Game.NeutralWinCondition NeutralVictory;
        public bool IsGhostRole => RoleCategory == RoleCategory.Ghost;
    }
    public class RuntimeRoleTemplate
    {
        public LightInDark.Game.Player MyPlayer;
        public RoleTemplate Role = new();
        public string CodeName => Role.CodeName;
        public bool Win;
        public virtual bool CheckWin() => Win;
    }
    public class PlayerTracker
    {
        public LightInDark.Game.Player Source;
        public int Updates;
        public bool Stopped;
        public void Update() => Updates++;
        public void Stop() => Stopped = true;
    }
}
namespace LightInDark.UI.Ability
{
    public class HudContent(GameObject obj)
    {
        public bool IsStaticContent, OccupiesLine, ShouldBeInLastLine;
        public Func<bool> ActiveFunc;
        public void SetPriority(int value) { }
        public void MarkAsKillButtonContent(bool value) { }
    }
    public class HudGrid
    {
        public static HudGrid Ensure() => new();
        public void RegisterContent(HudContent content, bool left) { }
    }
}
namespace LightInDark.Events
{
    public class GameTryEndEvent { public bool CrewmatesWin, ImpostorsWin, IsCanceled; public string Reason; }
    public static class EventSystem { public static Action<GameTryEndEvent> OnTryEnd; public static void RunEvent(GameTryEndEvent ev) => OnTryEnd?.Invoke(ev); }
    public class Check { public bool IsWin, IsExtraWin, IsBlocked; }
    public static class EventTriggers
    {
        public static Func<PlayerControl, Check> OnCheck, OnExtra, OnBlock;
        public static Check OnPlayerCheckWin(PlayerControl pc, string reason) => OnCheck?.Invoke(pc);
        public static Check OnPlayerCheckExtraWin(PlayerControl pc, string reason) => OnExtra?.Invoke(pc);
        public static Check OnPlayerBlockWin(PlayerControl pc, bool win, string reason) => OnBlock?.Invoke(pc);
    }
}
namespace LightInDark.RPCs
{
    public static class RpcDefinitions
    {
        public static readonly List<string> Wire = new();
        public static void SyncGameMode(string id) { Wire.Add("mode:"+id); LightInDark.Game.GameModeManager.ApplyRemote(id); }
        public static void SyncVictory(LightInDark.Game.GameEndReason reason, string team, byte[] ids)
        { Wire.Add("victory:"+team); LightInDark.Game.EndGameManager.ApplyResult(reason, team, ids); }
        public static void TickPendingMessages() { }
    }
}
