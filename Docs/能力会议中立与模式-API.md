# 能力、会议按钮、中立胜利与游戏模式

本轮对应 `/www/LID/需要改动的内容` 的四项需求，并按补充要求提供公开 API。实现位于主插件与 `LightInDark.dll`，继续使用现有 AU 19.0 x64、net6.0 和 BepInEx 依赖。

## 实现与入口

| 需求 | 实现与使用入口 |
| --- | --- |
| Nebula 风格能力按钮 | `AbilityButtonFactory.Create/CreateKill/CreateEffect`；冷却和效果独立计时，当前计时器优先显示效果；支持次数、主副热键、快捷键提示、闪白、破损、图标和标签更新、会议及释放回调。 |
| 优化会议按钮创建 | `CreateMeetingTarget` 创建名牌目标按钮，`CreateMeetingAbility(..., MeetingCorner.BottomLeft/BottomRight)` 创建角落按钮；自动错开同侧／同名牌按钮；支持手动 Position 和目标筛选。 |
| 中立与胜利系统 | `RoleTemplate.VictoryTeamCode/NeutralVictory/BlocksParityVictory/CountsTasksTowardVictory`，`VictoryRules.Register`，`VictoryResult` 与房主 `EndGameManager.TryEndGame`；结果同步到结算、获胜名单和复盘。 |
| 标准／自由模式雏形 | MOD 设置中的“游戏模式”；标准模式按既有设置分配职业、自动判定结束；自由模式可单人开局，默认分配船员，局内“选择职业”允许切换，不自动结束。房主 Alt+F 可手动结束为无效局。 |

能力和会议按钮均由职业运行时实例持有。HUD 尚未就绪或会议尚未开始时，逻辑对象仍有效；角色更换会释放按钮和所属追踪器，会议结束只清理会议 UI，下一次会议会重新创建。`HudManager.Update` 每渲染帧驱动一次，避免 FixedUpdate 漏掉输入或重复计时。

普通冷却在通风管、会议、放逐、开场、换装界面和非任务小游戏中暂停；任务小游戏允许推进。会议按钮则在会议内计时，Results/Proceeding 阶段禁止使用。点击时重新检查显示、冷却、次数、破损、聊天焦点、目标死亡与断线，并按帧去重。

## 公开 API

所有接口均为进程内 C# API，可通过引用 `LightInDark.dll` 接入；本轮没有引入 HTTP 服务。完整、可编译的外部插件示例见 `Tests/ApiConsumer/ExampleRole.cs`，它独立引用发布 DLL，不依赖主插件内部类型。

```csharp
// 放在 RuntimeRoleTemplate.OnActivated；只有本地所属玩家创建 HUD 按钮。
if (!AmOwner) return;
var button = AbilityButtonFactory.CreateEffect(this,
    new RoleButtonConfig {
        Label = "能力", Hotkey = KeyCode.F, Cooldown = 10,
        MaxUses = 3, StartOnCooldown = false,
        ResetCooldownAfterMeeting = true
    }, () => ActivateAbility());
button.EffectDuration = 2;
button.OnEffectEnd = () => DeactivateAbility();
```

`IAbilityButton` 提供 `TryClick/StartCooldown/StopCooldown/SetUses/SetLabel/SetIcon/Release`。`CooldownTimer` 和效果按钮的 `EffectTimer` 是公开 `AbilityTimer`，支持 Start/Stop/Pause/Resume、时长、剩余时间、归一化 Progress、CanProgress 谓词和 Completed 事件。`CurrentTimer` 返回应显示的计时器。

`RoleButtonConfig` 保留原有链式配置入口，并增加 `StartOnCooldown`、`ResetCooldownAfterMeeting`、`CancelEffectOnMeeting`、`MeetingStarted/MeetingEnded`、`OnUpdate/OnBroken/OnReleased` 和 `ShowKeyHint`。持续效果默认可再次点击取消；同一帧第二次点击不会立即取消刚开启的效果。死亡、会议和释放都会按配置结束效果。

```csharp
AbilityButtonFactory.CreateMeetingTarget(this,
    new MeetingTargetButtonConfig {
        Icon = icon, HoverIcon = hoverIcon, MaxUses = 1,
        CanAddTarget = p => p.Data != null && !p.Data.IsDead,
        CanUseTarget = p => p.Data != null && !p.Data.Disconnected
    }, (meeting, target) => SelectTarget(target));

AbilityButtonFactory.CreateMeetingAbility(this,
    new RoleButtonConfig { Label = "会议能力", StartOnCooldown = false },
    () => UseMeetingAbility(), MeetingCorner.BottomLeft);
```

`MeetingTargetButtonConfig.Position/Scale/AutoArrange/IncludeSelf` 控制名牌位置与目标；所有目标共享冷却及次数。`TryClickTarget(playerId)` 可程序触发指定目标。旧的 `CreateMeetingTarget(role, callback, canAdd, icon, sfx)` 签名仍可用。角落按钮默认右下角；设置返回实例的 `Position` 可使用自定义坐标。

## 中立与胜利约定

`RoleCategory` 继续定义角色分类；`TeamCode` 负责队友识别、追踪排除及名字可见，`VictoryTeamCode` 独立定义胜利阵营。同一阵营的存活成员参与计数，未断线的死亡队友也可共享阵营胜利。切换为鬼魂职业时保留死亡前的胜利身份。

`NeutralWinCondition.Manual` 用于小丑等独立胜利，`Parity` 在该阵营达到存活玩家半数且无其他阻挡敌对阵营时获胜，`LastSurvivor` 要求场上没有其他阵营。恶中立默认参与 Parity 并阻止普通人数胜利；良中立默认 Manual，不自动加入船员获胜名单。任务与破坏胜利仍可触发。断线玩家不参与计数，也不会进入获胜名单。

```csharp
VictoryRules.Register("addon.customWin", context => {
    if (!CustomCondition(context)) return null;
    return VictoryRules.ForTeam(context, "addon.team");
}, priority: 200);
```

规则按优先级从高到低、ID 稳定排序；返回 null 表示继续检查。注册规则先于旧 `RuntimeRoleTemplate.CheckWin()`，最后是内置任务、破坏和人数规则。使用相同 ID 会报错；`Unregister` 可撤销注册。`VictoryResult(reason, teamCode, winnerIds)` 可表达按玩家名单获胜的独立角色。

房主执行胜利检查、附加胜利及阻止胜利事件，再将最终获胜名单通过可靠 RPC 发给所有客户端，随后发原版游戏结束消息。客户端按最终名单结算，不重复本地计算附加胜利。`EndGameManager.Result/ResultReceived` 可获取结果；每局只接受一次结束，`TryEndGame(result, force: true)` 可绕过自由模式／调试限制。非房主和非 Started 状态不能发起结束。

RPC 接收端验证房主来源，只绕过正在接收的方法的发送拦截，嵌套 RPC 正常广播。自由模式客户端通过 `RequestFreePlayRole` 请求切换自身职业，由房主校验真实发送者、模式和回合状态后使用稳定 CodeName 广播分配。

## 模式扩展

```csharp
GameModeRegistry.Register(new GameModeDefinition(
    "addon.mode", "自定义模式", () => new MyAllocator(),
    allowAutomaticEnd: false, allowRoleChanges: true, minimumPlayers: 1));
```

模式 ID 必须唯一；分配器实现公开 `IRoleAllocator`。注册后自动进入模式选择项。`GameModeManager.Select(id)` 只允许房主在局外操作；开局前房主同步模式并重置结果，回合开始后冻结模式。`OnStarted/OnEnded` 是模式生命周期回调，`GameModeManager.Current` 返回当前定义。标准与自由模式固定在列表首部，扩展模式按 ID 排序。

这是模式框架雏形：提供分配、人数门槛、自动结束、职业切换和生命周期策略。Nebula 的额外特殊玩法与成就／语音等系统不在这四项需求内。

## 参考与验证

对照了 Dolly1016/Nebula-Public 的 `NebulaPluginNova/Modules/ScriptComponents/ModAbilityButtonImpl.cs`、`Roles/Neutral/Gambler.cs`、`Game/GameMode.cs`、`Game/NebulaGameEnd.cs` 和 `NebulaAPI/Game/GameMode.cs`。适配的是其按钮、计时、会议布局、队伍和模式契约，运行时使用本仓库已有 API／原版资源，不引入 Nebula DLL 依赖。

重放全部构建与检查：

```bash
DOTNET=dotnet bash Tools/run-regressions.sh
```

此脚本构建 Debug/Release 插件、验证服务器，运行既有回归、直接链接生产按钮／胜利／模式代码的托管场景回归、生产 RPC 编解码／发送接收回归，并编译独立 API 消费者。通过检查的 DLL 最终保留为 Release。Unity 替身用于可控场景测试，不能证明真实 IL2CPP 环境的渲染、相机层、点击区域或联网时序。

2026-10-12 验证结果：Debug/Release 插件与 Release 验证服务器构建通过；既有 69 项、玩法 89 项、RPC 9 项，共 167 项检查通过；独立 net6.0 API 消费者编译通过。插件构建仍有仓库原有警告，不能据此宣称零警告。交付包内保存完整日志和最终 DLL 哈希。

实机验收入口：将 `Output/Light.dll` 和 `Output/LightInDark.dll` 一起放入测试游戏插件目录；双客户端依次验证标准模式、豺狼／跟班胜利、任务／破坏胜利、会议名牌按钮、左右下角、多次会议、自身职业切换、自由模式单人开局及 Alt+F。检查快捷键文字、分辨率变化和按钮点击区。当前 Linux 工作区没有 Among Us/Unity 实机，以上显示和实际联网项目尚未执行。
