using LightInDark.Configuration;
using LightInDark.Events;
using LightInDark.Game;
using LightInDark.Roles;
using LightInDark.UI.Ability;
using Mono.Cecil;
using UnityEngine;
using ModEnd = LightInDark.Game.EndGameManager;
using ModGame = LightInDark.Game.GameManager;
using Object = UnityEngine.Object;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
RuntimeRoleTemplate Player(byte id, RoleCategory category = RoleCategory.Crewmate, bool local = false)
{
    var pc = new GameObject("player"+id).AddComponent<PlayerControl>();
    pc.PlayerId = id; pc.AmOwner = local;
    pc.Data.Role.IsImpostor = category == RoleCategory.Impostor;
    PlayerControl.AllPlayerControls.Add(pc);
    if (local) PlayerControl.LocalPlayer = pc;
    var p = new LightInDark.Game.Player { Control = pc };
    var role = new RuntimeRoleTemplate { MyPlayer = p, Role = new RoleTemplate { RoleCategory = category } };
    p.Role = role; p.VictoryRole = role.Role;
    ModGame.Instance.AllPlayers.Add(p);
    if (local) ModGame.Instance.LocalPlayer = p;
    return role;
}
void Reset()
{
    RoleButtonManager.Clear();
    PlayerControl.AllPlayerControls.Clear();
    ModGame.Instance = new();
    PlayerControl.LocalPlayer = null;
    HudManager.Instance = null;
    MeetingHud.Instance = null;
    Minigame.Instance = null;
    MapBehaviour.Instance = null;
    IntroCutscene.Instance = null;
    ExileController.Instance = null;
    Input.Pressed.Clear();
    AmongUsClient.Instance = new();
    global::GameManager.Instance = new();
    GameModeManager.Reset();
    ModEnd.Reset();
    ModEnd.AutomaticEndBlocked = null;
    EventSystem.OnTryEnd = null;
    EventTriggers.OnCheck = EventTriggers.OnExtra = EventTriggers.OnBlock = null;
    Time.frameCount++;
}
GameObject Child(GameObject parent, string name)
{
    var child = new GameObject(name);
    child.transform.SetParent(parent.transform, false);
    return child;
}
ActionButton Template(GameObject parent)
{
    var root = Child(parent, "AbilityTemplate");
    root.AddComponent<PassiveButton>();
    var action = root.AddComponent<ActionButton>();
    Child(root,"graphic").AddComponent<SpriteRenderer>();
    Child(root,"label").AddComponent<TMPro.TextMeshPro>();
    Child(root,"timer").AddComponent<TMPro.TextMeshPro>();
    Child(root,"translate").AddComponent<TextTranslatorTMP>();
    root.AddComponent<AspectPosition>();
    action.BindChildren();
    return action;
}
HudManager Hud()
{
    var root = new GameObject("HUD");
    var hud = root.AddComponent<HudManager>();
    hud.AbilityButton = Template(root); hud.KillButton = Template(root);
    HudManager.Instance = hud;
    return hud;
}
MeetingHud Meeting(params RuntimeRoleTemplate[] targets)
{
    var meeting = new GameObject("Meeting").AddComponent<MeetingHud>();
    meeting.playerStates = targets.Select(r =>
    {
        var area = Child(meeting.gameObject, "area"+r.MyPlayer.Control.PlayerId).AddComponent<PlayerVoteArea>();
        area.PlayerId = r.MyPlayer.Control.PlayerId;
        area.Buttons = Child(area.gameObject, "Buttons");
        var cancel = Child(area.Buttons, "CancelButton");
        cancel.AddComponent<SpriteRenderer>(); cancel.AddComponent<PassiveButton>();
        return area;
    }).ToArray();
    MeetingHud.Instance = meeting;
    return meeting;
}
void Frame(float dt = 0.1f)
{
    Time.frameCount++; Time.deltaTime = dt; Time.unscaledTime += dt;
    RoleButtonManager.UpdateAll();
}

Reset();
var timer = new AbilityTimer(2f);
int timerEnds = 0;
timer.Completed += () => timerEnds++;
timer.Start(); timer.CanProgress = () => false; timer.Tick(1);
Check(timer.Remaining == 2 && timer.Progress == 1, "timer predicate pauses progress");
timer.CanProgress = () => true; timer.Tick(0.5f);
Check(timer.Remaining == 1.5f && timer.Progress == 0.75f, "public timer exposes normalized progress");
timer.Pause(); timer.Tick(10); timer.Resume(); timer.Tick(10); timer.Tick(10);
Check(timerEnds == 1 && timer.Remaining == 0 && !timer.IsProgressing, "timer resume and completion fire exactly once");
timer.SetDuration(0); timer.Start();
Check(!timer.IsProgressing && timer.Progress == 0, "zero-duration timer is immediately ready");
timer.SetDuration(1); timer.Start(); timer.Tick(float.NaN); timer.Tick(-1);
Check(timer.Remaining == 1, "timer rejects invalid elapsed time");
var me = Player(0, local: true);
int clicks = 0;
var button = AbilityButton.Create(me, new RoleButtonConfig { Cooldown = 2, MaxUses = 2, StartOnCooldown = false }, () => clicks++);
Check(RoleButtonManager.Count == 1 && !button.IsDeadObject && !button.HasUI, "queued button has a live logical lifetime");
Frame();
Check(RoleButtonManager.Count == 1, "HUD absence does not remove pending buttons");
var hud = Hud(); Frame();
Check(button.HasUI && button.IsVisible, "HUD readiness creates and displays queued button");
Check(button.TryClick() && clicks == 1 && button.UsesLeft == 1, "click executes once and consumes one use");
Check(!button.TryClick() && clicks == 1, "same-frame repeated click is rejected");
Frame(0.5f);
Check(Math.Abs(button.CooldownRemaining - 1.5f) < 0.001f, "cooldown advances with render delta");
RoleButtonManager.UpdateAll();
Check(Math.Abs(button.CooldownRemaining - 1.5f) < 0.001f, "multiple HUD callbacks cannot advance a timer twice per frame");
me.MyPlayer.Control.inVent = true; Frame(1);
Check(Math.Abs(button.CooldownRemaining - 1.5f) < 0.001f, "vent pauses cooldown");
me.MyPlayer.Control.inVent = false;
Minigame.Instance = new GameObject().AddComponent<Minigame>(); Frame(1);
Check(Math.Abs(button.CooldownRemaining - 1.5f) < 0.001f, "non-task minigame pauses cooldown");
Minigame.Instance.MyNormTask = new(); Frame(1.5f);
Check(!button.IsInCooldown, "task minigame allows cooldown to finish");
Time.frameCount++;
Check(!button.TryClick(), "minigame blocks direct click, not just hotkeys");
Minigame.Instance = null; Frame();
hud.Chat.freeChatField.textArea.hasFocus = true;
Check(!button.TryClick(), "chat focus blocks ability clicks");
hud.Chat.freeChatField.textArea.hasFocus = false;
Check(button.TryClick() && button.UsesLeft == 0, "second permitted click exhausts uses");
button.StopCooldown(); Frame();
Check(!button.TryClick(), "exhausted uses block a refreshed button");
button.SetUses(1); button.Break(); Frame();
Check(!button.TryClick(), "broken state cannot fire callbacks");
Object.Destroy(hud.gameObject); HudManager.Instance = null; Frame();
Check(!button.IsDeadObject && !button.HasUI, "destroyed Unity object does not release logical button");
Hud(); Frame(); Frame();
Check(button.HasUI && button.UsesLeft == 1, "replacement HUD recreates UI and preserves usage state");
RoleButtonManager.ReleaseButtonsOf(me);
Check(button.IsDeadObject && RoleButtonManager.Count == 0, "role release removes both active and queued controls");

Reset(); me = Player(0, local: true); Hud();
int starts = 0, ends = 0;
var effect = EffectButton.Create(me, new RoleButtonConfig { StartOnCooldown = false, UseByMouseClick = true }, () => starts++);
effect.OnEffectEnd = () => ends++;
Frame();
Check(effect.TryClick() && effect.IsInEffect && starts == 1, "effect begins through unified input path");
Check(!effect.TryClick() && effect.IsInEffect && ends == 0, "duplicate callback cannot immediately cancel effect");
Frame(); Check(effect.TryClick() && !effect.IsInEffect && ends == 1, "next-frame reclick cancels effect once");
Frame(); effect.TryClick();
var meeting = Meeting(); RoleButtonManager.OnMeetingStart(meeting);
Check(!effect.IsInEffect && ends == 2, "meeting cancels persistent effect");
RoleButtonManager.OnMeetingEnd(); MeetingHud.Instance = null;
Frame(); effect.TryClick(); effect.Release(); effect.Release();
Check(ends == 3, "release ends persistent effects exactly once");

Reset(); me = Player(0, local:true); Hud();
ends = 0;
effect = EffectButton.Create(me, new RoleButtonConfig { StartOnCooldown = false, Cooldown = 5, Hotkey = KeyCode.F }, () => { });
effect.EffectDuration = 1f; effect.AllowCancelByReclick = false;
effect.OnEffectEnd = () => ends++;
Frame(); effect.TryClick();
Check(ReferenceEquals(effect.CurrentTimer,effect.EffectTimer), "effect timer takes visual priority over cooldown");
Frame(1f);
Check(!effect.IsInEffect && ends == 1 && ReferenceEquals(effect.CurrentTimer,effect.CooldownTimer)
    && Math.Abs(effect.CooldownRemaining - 4) < 0.001f, "finite effect expires and reveals independently advancing cooldown");
Check(HudManager.Instance.gameObject.GetComponentsInChildren<TMPro.TextMeshPro>(true).Any(t => t.gameObject.name == "AbilityKeyHint" && t.text == "F"), "configured hotkey appears on ability button");

Reset(); me = Player(0, local: true); var target = Player(1); Hud();
var tracker = new PlayerTracker { Source = me.MyPlayer };
RoleButtonManager.RegisterTracker(tracker); RoleButtonManager.ReleaseButtonsOf(me);
Check(tracker.Stopped, "role change stops owned trackers");
clicks = 0;
var targetButton = MeetingTargetButton.Create(me, new MeetingTargetButtonConfig { MaxUses = 1 }, (_, _) => clicks++);
var right = MeetingAbilityButton.Create(me, new RoleButtonConfig { StartOnCooldown = false }, () => clicks++);
var right2 = MeetingAbilityButton.Create(me, new RoleButtonConfig { StartOnCooldown = false }, () => clicks++);
var left = MeetingAbilityButton.Create(me, new RoleButtonConfig { StartOnCooldown = false }, () => clicks++, MeetingCorner.BottomLeft);
Frame(); Check(RoleButtonManager.Count == 4, "meeting controls survive task-phase updates");
meeting = Meeting(me, target); RoleButtonManager.OnMeetingStart(meeting); Frame();
me.MyPlayer.Control.CanMove = false;
Check(targetButton.HasUI && right.HasUI && left.HasUI, "meeting start builds target and corner controls");
Check(left.Position.x < 0 && right.Position.x > 0 && right2.Position.x < right.Position.x, "left/right slots arrange without overlap");
Check(right.TryClick(), "meeting default usability permits clicks while CanMove is false");
var pva = meeting.playerStates[1];
var pb = pva.transform.Children.First(t => t.gameObject.name.StartsWith("MeetingTarget_")).GetComponent<PassiveButton>();
Time.frameCount++; target.MyPlayer.Control.Data.IsDead = true; pb.OnClick.Invoke();
Check(clicks == 1, "target death between creation and click prevents callback");
target.MyPlayer.Control.Data.IsDead = false; pb.OnClick.Invoke();
Check(clicks == 2 && targetButton.UsesLeft == 0, "target callback consumes shared use");
Time.frameCount++; pb.OnClick.Invoke(); Check(clicks == 2, "meeting target usage limit is enforced");
meeting.state = MeetingHud.MeetingStates.Results;
Time.frameCount++; Check(!left.TryClick(), "result/proceeding phase blocks meeting actions");
RoleButtonManager.OnMeetingEnd(); MeetingHud.Instance = null; Frame();
Check(!right.HasUI && !targetButton.HasUI && RoleButtonManager.Count == 4, "meeting end clears UI and preserves subscriptions");
meeting = Meeting(me, target); RoleButtonManager.OnMeetingStart(meeting); Frame();
Check(right.HasUI && targetButton.HasUI && targetButton.UsesLeft == 0, "second meeting rebuilds controls without restoring uses");
RoleButtonManager.Clear(); Check(RoleButtonManager.Count == 0, "round cleanup releases all controls");

VictoryPlayer Crew(byte id, bool alive = true, bool disconnected = false) => new(id, RoleCategory.Crewmate, "Crewmate", alive, disconnected);
VictoryPlayer Imp(byte id, bool alive = true) => new(id, RoleCategory.Impostor, "Impostor", alive, false);
VictoryPlayer Neutral(byte id, string team, bool alive = true, bool disconnected = false, bool evil = true,
    NeutralWinCondition condition = NeutralWinCondition.Parity) => new(id, RoleCategory.Neutral, team, alive, disconnected, evil, condition);
VictoryResult Eval(params VictoryPlayer[] players) => VictoryRules.Evaluate(new(players));
Check(Eval(Crew(0),Crew(1),Imp(2)) == null, "balanced standard game continues");
Check(Eval(Crew(0),Imp(2)).Reason == GameEndReason.ImpostorsByKill, "impostor parity ends game");
Check(Eval(Crew(0),Imp(2,false)).WinnerIds.SequenceEqual(new byte[] {0}), "eliminated impostors award crew only");
Check(Eval(Crew(0),Neutral(1,"wolf")).TeamCode == "wolf", "evil neutral parity awards its own faction");
Check(Eval(Crew(0),Neutral(1,"wolf"),Neutral(2,"fox")) == null, "independent evil factions block each other");
Check(Eval(Crew(0),Neutral(1,"wolf"),Imp(2)) == null, "living impostor blocks neutral parity");
Check(Eval(Neutral(0,"wolf",evil:false,condition:NeutralWinCondition.Manual),Neutral(1,"wolf"),Crew(2)).TeamCode == "wolf", "mixed-role faction shares its active parity policy");
Check(Eval(Crew(0),Crew(3),Neutral(1,"wolf",disconnected:true)).Reason == GameEndReason.CrewmatesByVote, "disconnected evil neutral cannot block crew");
Check(Eval(Neutral(0,"wolf"),Neutral(1,"wolf",false),Crew(2)).WinnerIds.SequenceEqual(new byte[] {0,1}), "dead faction members share team victory");
Check(!Eval(Neutral(0,"wolf"),Neutral(1,"wolf",disconnected:true),Crew(2)).IsWinner(1), "disconnected teammate never wins");
Check(!Eval(Crew(0),Neutral(1,"jester",evil:false,condition:NeutralWinCondition.Manual)).IsWinner(1), "benign neutrals do not inherit crew victory");
Check(VictoryRules.Evaluate(new([Crew(0),Neutral(1,"wolf")], tasksComplete:true)).TeamCode == "Crewmate", "task victory remains available with evil neutrals");
Check(VictoryRules.Evaluate(new([Crew(0),Crew(1),Imp(2)], GameEndReason.CrewmatesByTasks)) == null,
    "vanilla task-end request cannot bypass authoritative task completion");
Check(VictoryRules.Evaluate(new([Crew(0),Imp(1),Neutral(2,"wolf")], GameEndReason.ImpostorsBySabotage)).TeamCode == "Impostor", "sabotage victory remains available with evil neutrals");
Check(Eval(Crew(0,false),Imp(1,false)).Reason == GameEndReason.Invalid, "no survivors produces no winners");
VictoryRules.Register("test.extra", c => new(GameEndReason.CustomWin,"jester",[3,3,99]), 1000);
Check(Eval(Crew(0),Neutral(3,"jester",evil:false)).WinnerIds.SequenceEqual(new byte[] {3}), "custom rule priority and winner normalization");
VictoryRules.Unregister("test.extra");
Check(EndGameReasonHelper.IsCustomWin((GameEndReason)101), "extension reasons keep custom victory presentation");
Check(Eval(Crew(0),Neutral(3,"jester",evil:false,condition:NeutralWinCondition.Manual)).TeamCode == "Crewmate", "unregister restores built-in rule evaluation");

GameModeRegistry.Register(new(GameModeRegistry.Standard,"standard",() => new EmptyAllocator()));
GameModeRegistry.Register(new(GameModeRegistry.FreePlay,"free",() => new EmptyAllocator(),false,true));
Reset(); me = Player(0,local:true); target = Player(1,RoleCategory.Impostor);
Check(GameModeManager.Select(GameModeRegistry.FreePlay) && GameModeManager.AllowRoleChanges && !GameModeManager.AllowAutomaticEnd, "host selects freeplay policy");
GameModeManager.BeginRound();
Check(!GameModeManager.Select(GameModeRegistry.Standard), "mode cannot change during a round");
var victory = new VictoryResult(GameEndReason.CustomWin,"test",[0]);
Check(!ModEnd.TryEndGame(victory) && global::GameManager.Instance.Ends == 0, "freeplay blocks automatic endings");
Check(ModEnd.TryEndGame(new(GameEndReason.Invalid,"",Array.Empty<byte>()),true), "manual forced end works in freeplay");
Check(ModEnd.Result.WinnerIds.Count == 0 && global::GameManager.Instance.Ends == 1, "invalid game has no winners and ends once");
Check(!ModEnd.TryEndGame(victory,true), "duplicate end request is ignored");
GameModeManager.EndRound();
Check(GameModeManager.Select(GameModeRegistry.Standard) && ModEnd.Result == null, "next round mode synchronization resets authoritative result");
GameModeManager.BeginRound();
AmongUsClient.Instance.AmHost = false;
Check(!ModEnd.TryEndGame(victory) && !GameModeManager.Select(GameModeRegistry.FreePlay), "clients cannot choose outcomes or host modes");
AmongUsClient.Instance.AmHost = true;
AmongUsClient.Instance.GameState = InnerNet.InnerNetClient.GameStates.Joined;
Check(!ModEnd.TryEndGame(victory,true), "API cannot end a lobby before gameplay starts");
AmongUsClient.Instance.GameState = InnerNet.InnerNetClient.GameStates.Started;
EventSystem.OnTryEnd = ev => ev.IsCanceled = true;
Check(!ModEnd.TryEndGame(victory) && ModEnd.Result == null, "cancelled pre-end event preserves active game");
EventSystem.OnTryEnd = null;
EventTriggers.OnExtra = pc => new() { IsExtraWin = pc.PlayerId == 1 };
EventTriggers.OnBlock = pc => new() { IsBlocked = pc.PlayerId == 0 };
Check(ModEnd.TryEndGame(victory) && ModEnd.Result.WinnerIds.SequenceEqual(new byte[] {1}), "host finalizes extra and blocked winners before replication");
var hostResult = ModEnd.Result;
ModEnd.Reset(); AmongUsClient.Instance.AmHost = false;
ModEnd.ApplyResult(hostResult.Reason,hostResult.TeamCode,hostResult.WinnerIds.ToArray());
Check(ModEnd.Result.WinnerIds.SequenceEqual(hostResult.WinnerIds) && ModEnd.CurrentReason == hostResult.Reason, "client replay preserves exact host result");

if (args.Length > 0)
{
    using var api = ModuleDefinition.ReadModule(args[0]);
    IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> ts) => ts.SelectMany(t => new[] {t}.Concat(Types(t.NestedTypes)));
    var types = Types(api.Types).ToDictionary(t => t.FullName);
    foreach (var name in new[] {"IAbilityButton","IAbilityTimer","AbilityTimer","MeetingTargetButtonConfig","MeetingCorner"})
        Check(types.ContainsKey("LightInDark.UI.Ability."+name), "built public button API: "+name);
    foreach (var name in new[] {"VictoryContext","VictoryRules","VictoryResult","GameModeDefinition","GameModeRegistry"})
        Check(types.ContainsKey("LightInDark.Game."+name), "built public gameplay API: "+name);
    var rpc = types["LightInDark.RPCs.RpcDefinitions"];
    foreach (var name in new[] {"SyncVictory","SyncGameMode","ForceEndGameInvalid"})
        Check(rpc.Methods.Single(m => m.Name == name).CustomAttributes.Any(a => a.AttributeType.Name == "LidRPCAttribute"
            && a.Properties.Any(p => p.Name == "OnlyHost" && Equals(p.Argument.Value,true))), "built host-only RPC: "+name);
    foreach (var name in new[] {"RoleButtonsHudUpdatePatch","MeetingHudStartPatch","MeetingHudOnDestroyPatch"})
        Check(types["LightInDark.UI.Ability."+name].CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch"), "built Harmony driver: "+name);
}
Console.WriteLine($"Gameplay regression: {checks} checks passed.");

sealed class EmptyAllocator : IRoleAllocator { public void Assign(List<byte> impostors,List<byte> others) { } }
