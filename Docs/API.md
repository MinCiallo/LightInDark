# LightInDark（暗中辉）API 使用文档

> 本模块是 Light In The Dark 模组（BepInEx + Reactor + IL2CPP）的独立 API 库，供二次开发者在主插件（LightPluginMain）中实现自定义职业、事件、RPC、GUI 窗口等扩展功能。

## 文档约定

- **命名空间前缀**：所有 API 类型均以 `LightInDark` 开头，包括 `LightInDark.Color`、`LightInDark.Configuration`、`LightInDark.Events`、`LightInDark.Game`、`LightInDark.Language`、`LightInDark.RPCs`、`LightInDark.Roles`、`LightInDark.UI.Window`、`LightInDark.Utilities`、`LightInDark.Core`（`LightLogger`）。唯一例外：`LightAPI/Core/ILifespan.cs` 中的生命周期类型（`ILifespan`、`IReleasable`、`SimpleLifespan`、`DependentLifespan`、`FlexibleLifespan`、`IGameComponent`、`IGameOperator`）位于**全局命名空间**（无命名空间声明），直接可用。
- **引用方式**：主插件项目引用 `LightAPI` 编译产物（LightInDark.dll），在代码顶部 `using LightInDark.Game;` 等。由于 API 的 `LightInDark.Color` 与 `UnityEngine.Color` 同名，主插件统一使用别名：

  ```csharp
  using Color = LightInDark.Color;
  using Font = LightInDark.UI.Window.Font;
  using FontStyle = LightInDark.UI.Window.FontStyle;
  using TextAlignment = LightInDark.UI.Window.TextAlignment;
  ```

- **初始化顺序**（主插件 `LightPlugin.Load()` 中依次执行，二次开发插件需参考）：

  ```csharp
  RoleRegistry.RegisterAssembly(typeof(MyPlugin).Assembly); // 1. 自动扫描注册自定义职业
  Language.Load();                           // 2. 加载语言文件
  LidRpcRegistry.ScanAndPatch(Harmony);      // 3. 扫描 [LidRPC] 方法并打补丁
  RoleConfigRegistrar.Register();            // 4. 生成职业配置块（CanBeAssigned 职业自动跳过）
  // 5. 可选：订阅 API 提供的事件（如 RpcDefinitions.OnFreeChatStateChanged）
  ```

- **示例来源**：本文档所有示例均摘自主插件真实代码（`Roles/Crewmates/Caller.cs`、`Patches/RoleInterceptPatch.cs`、`UI/Help/HelpScreen.cs`、`Patches/PatchManager.cs`、`Roles/Assignment/StandardRoleAllocator.cs` 等）。

---

## 目录

1. [LightInDark.Color — 颜色](#1-lightindarkcolor--颜色)
2. [LightInDark.Configuration — 游戏配置](#2-lightindarkconfiguration--游戏配置)
3. [LightInDark.Core — 生命周期与日志](#3-lightindarkcore--生命周期与日志)
4. [LightInDark.Events — 事件系统](#4-lightindarkevents--事件系统)
5. [LightInDark.Game — 游戏实体](#5-lightindarkgame--游戏实体)
6. [LightInDark.Language — 多语言](#6-lightindarklanguage--多语言)
7. [LightInDark.RPCs — 网络通信](#7-lightindarkrpcs--网络通信)
8. [LightInDark.Roles — 职业系统](#8-lightindarkroles--职业系统)
9. [LightInDark.UI.Window — 窗口 GUI](#9-lightindarkuiwindow--窗口-gui)
10. [LightInDark.Utilities — 工具](#10-lightindarkutilities--工具)

---

## 1. LightInDark.Color — 颜色

**模块用途**：提供模组专用的颜色类型，与 Unity 颜色互转，并预置常用颜色与阵营颜色。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `Color`（struct） | 模组自带的 RGBA 颜色结构（0~1 浮点），与 UnityEngine.Color 互不依赖 |
| `ColorHelper`（static） | 颜色双向转换扩展方法 |

### 关键成员

**`Color` 结构体**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 字段 | `float R, G, B, A` | 颜色分量，取值 0~1 |
| 构造函数 | `Color(float r, float g, float b, float a = 1f)` | 浮点构造 |
| 构造函数 | `Color(int r, int g, int b, float a = 1f)` | 整数 0~255 构造，内部自动 Clamp 并除以 255 |
| 静态属性 | `ImpostorColor` / `CrewmateColor` | 阵营色，默认取原版 `Palette.ImpostorRed` / `Palette.CrewmateBlue` |
| 静态属性 | `Red` `Yellow` `Green` `Cyan` `Blue` `Magenta` `White` `Black` `Gray` `Clear` | 预置常用色 |

**`ColorHelper`（扩展方法）**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 扩展方法 | `UnityEngine.Color ToUnityColor(this Color)` | 转 Unity 颜色（用于材质/TextMeshPro 等） |
| 扩展方法 | `Color ToLIDColor(this UnityEngine.Color)` | Unity 颜色转模组颜色 |

### 使用示例

```csharp
using Color = LightInDark.Color;

// 构造与预设色
Color c1 = new(1f, 0.7f, 0f);              // 橙色
Color c2 = new(255, 128, 0);                // 整数构造
Color c3 = Color.ImpostorColor;             // 阵营色

// 与 UnityEngine.Color 互转（写入材质/渲染器）
renderer.color = c1.ToUnityColor();
Color back = renderer.color.ToLIDColor();
```

---

## 2. LightInDark.Configuration — 游戏配置

**模块用途**：定义职业阵营枚举与模组硬编码配置常量，供职业系统与分配器读取。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `RoleCategory`（enum） | 职业阵营：船员 / 内鬼 / 中立 / **鬼魂** |
| `NeutralType`（enum） | 中立细分：`Benign`（善意）/ `Evil`（恶意）等 |

> **配置系统已迁移**：本模块现在**只放枚举**。运行时配置项（职业数量/概率/专属项、分配上限）由 `LightAPI/Configuration/ConfigItem.cs` 提供：
> `ConfigBlock`（`AddConfiguration` / `AddFilter` / `SetHeaderColor`）、`ConfigItem`（`GetBool/GetInt/GetFloat`、`SetDependsOn` / `SetVisibleWhen` / `WithSuffix` / `WithColor`）、`ConfigRegistry`（`Get/GetBool/GetInt/GetFloat/ByCategory/All/Blocks`）。
> 旧的 `RoleConfig` 静态类与 `GameConfig` 硬编码常量表**均已删除**，不要再引用。

### 关键成员

**`RoleCategory` 枚举**

```csharp
public enum RoleCategory { Crewmate, Impostor, Neutral, Ghost }
```

> `Ghost`（2026-10-06 新增）：**鬼魂职业**，不会被开局分配，只能由 `GhostRoleAllocator` 在玩家死亡时授予；对应 `ConfigCategory.Ghost` 分类与 `RoleTemplate.IsGhostRole`。

**分配上限现在可配置**（不再硬编码）

| 配置键 | 默认 | 含义 |
| --- | --- | --- |
| `lid.assignment.crewmateMax` | 2 | 船员职业数上限（0~15） |
| `lid.assignment.impostorMax` | 2 | 内鬼职业数上限（0~15） |
| `lid.assignment.neutralMax` | 1 | 中立职业数上限（0~15） |

> 由 `Light/Config/AssignmentConfigRegistrar.cs` 注册；`StandardRoleAllocator` 内部读取（私有 `MaxImpostorRoles` / `MaxNeutralRoles` / `MaxCrewmateRoles`，缺失/异常回退默认并夹到 0~15）。

**HUD 布局现在可配置（2026-10-07 新增）**

| 配置键 | 默认 | 含义 |
| --- | --- | --- |
| `lid.hud.smallGrid` | false | 小 HUD：技能按钮阵缩到 0.72 倍并换边距公式（小分辨率/小 HUD 时不重叠） |
| `lid.hud.buttonArrangement` | 0 | 排列档位：0=默认；1=只抬高左侧一列；2=全部抬高（按钮多时不与原版按钮挤在一起） |

> 由 `Light/Config/HudLayoutConfigRegistrar.cs` 注册（`ConfigBlock id=lid.hud`）；由 `LightAPI/UI/Ability/HudGrid.cs` 的 `HudContent.SmallHud` / `Arrangement` 读取。**默认值 = 改动前行为**（纯增量）。

**地图颜色（2026-10-16 新增，走设置菜单 Light 页签，不入 `lid.hud` 块）**

存 `LightSettings.MapColor`（`Light/Config/LightSettingsJson.cs`），设置入口在 `Light/Config/LightOptionsRegistry.cs` 的选择器「地图颜色」，生效在 `Light/Patches/MapColorPatch.cs`（挂 `MapBehaviour.Show` 后置）：

| 值 | 含义 |
| --- | --- |
| 0（默认） | 职业颜色（拿不到职业退回玩家颜色） |
| 1 | 玩家颜色 |
| 2 | 原版（不染色） |

> 管理员桌（`CountOverlay`）与破坏视图（`Sabotage`）**一律不染** —— 它们的颜色本身携带信息。

**配置读写（现行 API：`ConfigRegistry` + `ConfigItem`）**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 取项 | `static ConfigItem? Get(string key)` | 按 key 取配置项（不存在返回 null） |
| 取值 | `static bool GetBool(string)` / `static int GetInt(string)` / `static float GetFloat(string)` | 便捷取值（找不到返回 `false/0/0f`） |
| 分组 | `static List<ConfigItem> ByCategory(ConfigCategory)` | 取某分类下全部配置项 |
| 全量 | `IReadOnlyList<ConfigItem> All` / `Blocks` | 全部配置项 / 配置块（顺序稳定） |
| 写入 | `ConfigItem.SetBool/SetInt/SetFloat(...)` | 写回并落盘（`ConfigItem` 上） |

职业配置项键约定：`role.<CodeName>.count`（数量 0~15）、`role.<CodeName>.chance`（概率 0~100）、专属项 `role.<CodeName>.<Key>`。

### 使用示例

分配器按类别抽选职业时读取**可配置**的上限（不再是硬编码常量）：

```csharp
using LightInDark.Configuration;

// 阵营上限：分配器内部从 lid.assignment.* 读取（MaxImpostorRoles / MaxNeutralRoles / MaxCrewmateRoles）
Roll(table, impostors, BuildPool(RoleCategory.Impostor),  2);   // ← lid.assignment.impostorMax
Roll(table, others,    BuildPool(RoleCategory.Neutral),   1);   // ← lid.assignment.neutralMax
Roll(table, crew,      BuildPool(RoleCategory.Crewmate),  2);   // ← lid.assignment.crewmateMax

// 单个职业"生效的数量 / 概率"（已夹紧，与配置一致；帮助页就是用它来显示规则的）
int maxCount = StandardRoleAllocator.GetMaxCount(role);
int chance   = StandardRoleAllocator.GetChance(role);
```

职业声明自己的配置项（`RoleConfiguration` 轻量项，注册器自动加 `role.<CodeName>.` 前缀）：

```csharp
public override RoleConfigItem[] RoleConfiguration =>
[
    new RoleConfigItem { Key = "Cooldown", Type = ConfigType.Float, Default = 20f, Min = 5f, Max = 60f, Suffix = ConfigSuffix.Seconds },
];
```

---

## 3. LightInDark.Core — 生命周期与日志

**模块用途**：提供对象生命周期（存活/释放）基础类型与全局日志器。生命周期类型位于**全局命名空间**，日志器位于 `LightInDark.Core`。

### 类型列表

| 类型 | 命名空间 | 职责 |
| --- | --- | --- |
| `ILifespan`（interface） | 全局 | 存活标记：`IsDeadObject` / `IsAliveObject` |
| `IReleasable`（interface） | 全局 | 可释放标记：`void Release()` |
| `SimpleLifespan` | 全局 | 简单生命周期：`Release()` 后即死亡 |
| `DependentLifespan` | 全局 | 依赖父级生命周期：父级死亡则自身死亡 |
| `FlexibleLifespan` | 全局 | 灵活生命周期：自身释放或父级死亡均视为死亡 |
| `IGameComponent`（interface） | 全局 | 游戏组件空标记接口 |
| `IGameOperator`（interface） | 全局 | 游戏操作实体：`IGameComponent + IReleasable + ILifespan`，含默认方法 `OnReleased()` |
| `LightLogger`（static） | `LightInDark.Core` | 文件日志器（LightLog.log） |

### 关键成员

**`ILifespan` / `IReleasable`**

```csharp
public interface ILifespan
{
    bool IsDeadObject { get; }
    bool IsAliveObject => !IsDeadObject;
}
public interface IReleasable { void Release(); }
```

**`IGameOperator`**：所有可注册进 GameManager 的实体（Player、RuntimeRoleTemplate、按钮等）都实现它。

```csharp
public interface IGameOperator : IGameComponent, IReleasable, ILifespan
{
    void OnReleased() { }   // 默认空实现，可覆写
}
```

**`LightLogger`（LightInDark.Core）**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 日志 | `static void Log(string message = "undefined")` | 普通日志 |
| 日志 | `static void LogWarning(string message = "undefined")` | 警告日志（不抛异常） |
| 日志 | `static void LogError(string message = "undefined", string reason = "undefined")` | 错误日志（写入后**抛异常**） |
| 日志 | `static void ClearLog()` | 清空日志文件 |

日志默认写入游戏根目录 `LightLog.log`；若无权写入则回退到 `LocalLow/Innersloth/Among Us/GiftLog.log`。

### 使用示例

自定义职业的运行时类通常实现 `IGameOperator`，并配合 `GameEntityExtension.Register` 注册到游戏（详见第 5 章）：

```csharp
public class MyOperator : IGameOperator
{
    public bool IsDeadObject { get; private set; }
    public void Release() => IsDeadObject = true;
    public void OnReleased() { /* 释放时的清理 */ }
}

// 记录日志
LightLogger.Log("游戏开始");
LightLogger.LogWarning("冷却未就绪");
```

---

## 4. LightInDark.Events — 事件系统

**模块用途**：模组事件总线。通过实例方法 + 特性声明监听器，支持优先级、宿主/本地过滤；框架在关键时刻 `RunEvent` 触发事件。事件类型分为**只读事件**（通知类）与**可取消事件**（合作式：监听者设置 `IsCanceled` 阻止后续逻辑）。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `IEvent`（interface） | 事件标记接口 |
| `EventPriority`（Attribute） | 方法级特性：监听优先级，数值大者先执行（默认 0） |
| `OnlyMyPlayer`（Attribute） | 方法级特性：仅当事件 `Player` 为本地玩家时触发 |
| `OnlyHost`（Attribute） | 方法级特性：仅房主触发 |
| `Local`（Attribute） | 方法级特性：仅本客户端触发 |
| `EventSystem`（static） | 事件注册/触发核心 |
| `BasePlayerEvent` | 玩家事件基类（含 `Player`） |
| `BaseCancelableEvent` | 可取消事件基类（含 `IsCanceled`） |
| `BaseCancelablePlayerEvent` | 可取消玩家事件基类 |
| 各具体事件类 | 见下表 |

### 事件类型一览

| 事件类 | 关键成员 | 可否取消 | 说明 |
| --- | --- | --- | --- |
| `GameStartEvent` | `PlayerCount` | 否 | 游戏开始 |
| `GameEndEvent` | `CrewmatesWin` `ImpostorsWin` `WinReason` | 否 | 游戏结束 |
| `GameTryEndEvent` | `CrewmatesWin` `ImpostorsWin` `Reason` | **是** | 游戏即将结束（可阻止） |
| `RoleAssignedEvent` | `Role`(RoleTemplate) `Arguments`(int[]) | 否 | 职业被设置到玩家 |
| `PlayerTryToChangeRoleEvent` | `OldRole`(RuntimeRoleTemplate) `NewRole`(RoleTemplate) | **是** | 换职前（可阻止） |
| `PreFixAssignmentEvent` | `Table`(IRoleTable) | 否 | 分配确定前（供修正分配表） |
| `PlayerDisconnectEvent` | — | 否 | 玩家断开连接 |
| `PlayerSuicideEvent` | `Reason` `NeedLog` | 否 | 玩家自杀 |
| `PlayerMurderEvent` | `Killer`(=Player) `Victim` | 否 | 玩家击杀完成 |
| `PlayerTryMurderEvent` | `Killer`(=Player) `Victim` | **是** | 尝试击杀（可阻止） |
| `PlayerDeathEvent` | `Killer` `Reason`(DeathReason) | 否 | 玩家死亡 |
| `PlayerReviveEvent` | — | 否 | 玩家复活 |
| `PlayerTaskCompleteEvent` | `CompletedTasks` `TotalTasks` | 否 | 玩家完成任务 |
| `AllTasksCompleteEvent` | `Player` | **是** | 所有任务完成（可阻止游戏结束） |
| `MeetingTryStartEvent` | `Reporter` `ReportedBody` `IsEmergencyMeeting` | **是** | 尝试召开会议 |
| `MeetingStartEvent` | `Reporter` `ReportedBody` `IsEmergencyMeeting` | 否 | 会议开始 |
| `MeetingDiscussionStartEvent` | — | 否 | 讨论阶段开始 |
| `MeetingVotingStartEvent` | — | 否 | 投票阶段开始 |
| `PlayerVoteEvent` | `VotedForPlayerId`(可改) `VoteWeight` | 否 | 玩家投票（可改票与权重） |
| `MeetingTryEndVotingEvent` | `ExiledPlayerId`(可改) `IsTie` | **是** | 结束投票（可改放逐对象） |
| `MeetingTryEndEvent` | — | **是** | 会议即将结束 |
| `MeetingEndEvent` | `ExiledPlayerId` `WasTie` | 否 | 会议结束 |
| `PlayerTryExileEvent` | `ExilePlayerId`(可改) `IsTie` | **是** | 放逐即将执行（可改对象） |
| `PlayerExileEvent` | `Exiled` | 否 | 放逐动画执行中（只读） |
| `EmergencyButtonBrokenEvent` | — | 否 | 紧急按钮被破坏 |
| `HudActiveChangeEvent` | `IsActive` | 否 | HUD 激活状态变更 |
| `SabotageStartEvent` | `SystemType` | 否 | 破坏系统激活 |
| `SabotageEndEvent` | `SystemType` | 否 | 破坏系统修复 |
| `VentUsedEvent` | `VentId` `Player` | 否 | 管道被使用 |
| `PlayerKickEvent` | `Reason` `Kicker` | 否 | 玩家被踢出 |
| `ChatMessageEvent` | `Message` | 否 | 聊天消息 |
| `PlayerMoveEvent` | `Position` | 否 | 玩家移动（每帧，高频） |

> 注：`DeathReason`、`SystemTypes` 为 Among Us 原版枚举。可取消事件均为合作式——调度器不会自动中断，由触发方检查 `IsCanceled` 决定是否继续。

### 关键成员（EventSystem）

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 扫描 | `static void RegisterAssembly(Assembly asm)` | 扫描程序集中所有参数为 `IEvent` 子类的方法（静态直接缓存，实例方法待绑定） |
| 注册实例 | `static void RegisterInstance(object instance)` | 将实例对象上匹配的方法绑定到已缓存监听器（幂等） |
| 注销实例 | `static void UnregisterInstance(object instance)` | 解绑实例的监听 |
| **新增** 批量解绑 | `static void DetachAllInstances()` / `static void Detach(object instance)` | 清空全部实例监听 / 解绑单个（换局清理由 `GameEndPatch.CleanupRolesAtGameEnd()` 调用） |
| 触发 | `static T RunEvent<T>(T ev) where T : IEvent` | 按优先级降序执行所有监听，应用 `[OnlyHost]`/`[Local]`/`[OnlyMyPlayer]` 过滤，返回事件对象 |
| 诊断 | — | `Attach` 命中 0 个监听时打 warning（实例方法签名不匹配会静默失效，这条日志就是用来抓它的） |

### 使用示例

**① 声明并注册监听器**（典型写法：监听方法为实例方法，构造时 `RegisterInstance`）：

```csharp
using LightInDark.Events;
using LightInDark.Core;

public class MyListener
{
    public MyListener() => EventSystem.RegisterInstance(this);

    [EventPriority(10)]            // 高优先级，先执行
    [OnlyHost]                     // 仅房主执行
    void OnPlayerDeath(PlayerDeathEvent ev)
    {
        LightLogger.Log($"{ev.Player.Data.PlayerName} 死亡，凶手: {ev.Killer?.Data?.PlayerName}");
    }

    [OnlyMyPlayer]                 // 仅当事件 Player 是本地玩家
    void OnMyTask(PlayerTaskCompleteEvent ev)
        => LightLogger.Log($"我的任务进度 {ev.CompletedTasks}/{ev.TotalTasks}");

    void OnTryEnd(MeetingTryEndEvent ev)
    {
        if (SomethingBad) ev.IsCanceled = true;   // 阻止会议结束
    }
}
```

**② 触发事件**（框架/补丁处，主插件 `RoleInterceptPatch.cs` 真实用法）：

```csharp
using LightInDark.Events;

// 游戏开始（GameManager.StartGame Postfix）
EventSystem.RunEvent(new GameStartEvent
{
    PlayerCount = PlayerControl.AllPlayerControls.Count
});

// 玩家死亡（PlayerControl.Die Postfix）
EventSystem.RunEvent(new PlayerDeathEvent
{
    Player = __instance,
    Reason = reason
});

// 可取消事件：所有任务完成时询问监听者是否阻止游戏结束
var ev = new GameTryEndEvent { CrewmatesWin = true, Reason = "task" };
EventSystem.RunEvent(ev);
if (ev.IsCanceled) { __result = false; return false; }
```

**③ 生命周期自动监听**：`RuntimeRoleTemplate` 在 `Activate` 时自动 `RegisterInstance(this)`（框架在 `Player.SetRoleLocal` 内调用），因此其内部的事件方法（如 `RuntimeRoleTemplate.OnTaskComplete`）无需手动注册。

---

## 5. LightInDark.Game — 游戏实体

**模块用途**：封装原版 `PlayerControl` 的玩家实体系统，提供 `GameManager` 单例统一管理所有玩家/实体，并支持实体注册与生命周期绑定。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `IGame`（interface） | 游戏接口：玩家枚举、注册/注销实体 |
| `GameManager` | 游戏管理器单例，玩家实体容器 |
| `IPlayer`（interface） | 玩家只读接口 |
| `IBindPlayer`（interface） | 绑定玩家的接口（`MyPlayer` / `AmOwner`） |
| `Player` | 玩家实体，封装 PlayerControl 与 RuntimeRoleTemplate |
| `LightPlayer`（static） | **`Player` 的别名入口**（纯转发）：`LocalPlayer` / `HostPlayer` / `AllPlayers` / `Get` / `Find` / `FindAll` / `TryGetPlayer`，两种写法都能用 |
| `GameState`（static） | **全局游戏状态判定**（全部实时读取原版单例，不缓存）：`IsJoined` / `IsNotJoined` / `IsLobby` / `IsInGame` / `IsInitGame` / `IsInMeeting` / `IsVoting` / `IsMeetingResults` / `IsInTask` / `IsInIntro` / `IsInExile` / `IsInMinigame` / `IsMapOpen` / `IsChatFocused` / `IsCountDown` / `IsFreePlay` / `IsOnlineGame` / `IsLocalGame` / `AmHost` / `LocalPlayer` / `IsDead` / `CanMove` |
| `GameEntityExtension`（static） | 实体注册扩展：`Register` / `RegisterSelf` |
| `GameEndReason`（enum） | 模组自定义游戏结束原因（原版 `GameOverReason` 已弃用） |
| `EndGameReasonHelper`（static） | 结束原因判断：`IsImpostorWin` / `IsCustom` |

### 关键成员

**`GameManager`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 单例 | `static GameManager Instance` | 全局单例（惰性创建） |
| 初始化 | `void Initialize()` | 按当前玩家列表重建（**幂等**：同一 `PlayerControl` 复用已有 `Player` 以保住职业）；末尾调用 `RpcDefinitions.FlushPendingRoles()` 重放早到的职业 RPC |
| 查询 | `Player GetPlayer(byte playerId)` | 按 PlayerId 获取玩家 |
| 枚举 | `IEnumerable<Player> AllPlayers` | 所有玩家 |
| 属性 | `Player LocalPlayer { get; }` | 本地玩家 |
| 注册 | `void RegisterEntity(IGameOperator entity, ILifespan lifespan)` | 注册实体 |
| 注销 | `void UnregisterEntity(IGameOperator entity)` | 注销实体 |
| 更新 | `void Update()` | 每帧清理已释放实体、驱动挂起消息补发（按钮/能力更新由补丁驱动） |

**`Player`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 属性 | `PlayerControl Control` | 原版玩家控件 |
| 属性 | `bool IsDead` / `bool IsLocal` / `string Name` / `Vector2 Position` | 玩家状态 |
| 属性 | `RuntimeRoleTemplate Role { get; internal set; }` | 当前运行时职业 |
| 属性 | `Player MyPlayer` / `bool AmOwner` | IBindPlayer 实现（AmOwner == IsLocal） |
| 属性 | `bool IsDeadObject` | 断开/死亡即视为死亡对象 |
| 属性 | `Color PlayerColor` | 玩家外观颜色（模组 Color） |
| 属性 | `RoleCategory? RoleCategory` | 职业阵营（无职业为 null） |
| 方法 | `void SetRole(RoleTemplate newRole, int[] arguments = null)` | 本地切换职业：触发 `PlayerTryToChangeRoleEvent`（可取消）→ 失活旧职业 → 实例化新职业并激活 → 触发 `RoleAssignedEvent` → 发送 **`SetRoleByCode`** RPC（按稳定内部名 `CodeName` 同步，不再用注册序号 Id） |
| 方法 | `void Suicide()` | 自杀（RPC 同步） |
| 方法 | `void MurderPlayer(PlayerControl victim)` | 击杀（RPC 同步） |
| 判断 | `bool HasRole` | 有无自定义职业 |
| **新增** 数据位 | `internal int GetRoleData(int dataId)` / `internal void SetRoleData(int dataId, int value)` | 职业数据插槽的**每玩家存储**（由 `RoleData` 读写；随换局重建 `Player` 自动清空） |
| 判断 | `bool Is<T>() where T : RoleTemplate` | 按模板类型判断职业 |
| 判断 | `bool IsHost` | 当前玩家是不是房主 |
| 清理 | `void Release()` | 注销事件监听、释放职业 |

**`Player` 静态查询 API**（2026-10-16 新增；`LightPlayer` 为同签名别名）

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 本地玩家 | `static Player LocalPlayer` | 拿不到返回 null |
| 房主 | `static Player HostPlayer` | 大厅/局内都可用 |
| 全部玩家 | `static IEnumerable<Player> AllPlayers` | 未就绪时为空序列 |
| 按 Id | `static Player Get(byte playerId)` | 找不到返回 null |
| 反查 | `static Player Get(PlayerControl control)` | 由原版控件反查包装 |
| 查找 | `static Player Find(Func<Player,bool>)` / `static List<Player> FindAll(Func<Player,bool>)` | 按条件找玩家 |
| 尝试取 | `static bool TryGetPlayer(Func<Player,bool>, out Player)` | 命中返回 true |

**`GameEntityExtension`（LightInDark.Game）**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 注册 | `static T Register<T>(this T entity, ILifespan lifespan) where T : IGameOperator` | 注册实体到 GameManager（可链式返回自身） |
| 注册 | `static T RegisterSelf<T>(this T entity) where T : IGameOperator, ILifespan` | 以自身为寿命注册 |

**`GameEndReason` 枚举**：原版 `GameOverReason` 已弃用，统一使用本枚举驱动结算/复盘/事件。内置值 `CrewmatesByTasks`、`ImpostorsByKill` 等与原版规则对应；`CustomFirst = 100` 起供模组自定义。已内置 `CustomJesterWin = 101`（Jester 被放逐即获胜）。判断用 `EndGameReasonHelper.IsImpostorWin(reason)` / `IsCustom(reason)`，通过 `EndGameManager.TryEndGame(reason)` 以自定义原因结束一局。

### 使用示例

**① 初始化与查询**（主插件 `RoleInterceptPatch.cs`）：

```csharp
using LightInDark.Game;

// 拦截原版选职时初始化玩家系统
GameManager.Instance.Initialize();

// 按 ID 查玩家
Player p = GameManager.Instance.GetPlayer(playerId);
if (p != null) { /* ... */ }

// 遍历所有玩家
foreach (var player in GameManager.Instance.AllPlayers)
{
    if (player.IsDead) continue;
    // ...
}
```

**② 切换职业**（主插件 `PatchManager.cs` 的 `/sr` 指令）：

```csharp
using LightInDark.Game;
using LightInDark.Roles;

var me = GameManager.Instance.LocalPlayer;
if (me == null) return;
me.SetRole(RoleRegistry.Get<Caller>());   // 模板单例，本地切换 + RPC 同步
```

**③ 玩家判断与操作**：

```csharp
using LightInDark.Game;
using LightInDark.Roles;

if (player.Is<Caller>()) { }               // 按模板类型判断
if (player.IsCrewmate()) { }               // 按阵营判断（RoleTypeChecker 扩展）
player.Suicide();                          // 自杀（同步）
player.MurderPlayer(target.Control);       // 击杀（同步）
```

**④ 实体注册**：

```csharp
public class MyEntity : IGameOperator
{
    public bool IsDeadObject { get; private set; }
    public void Release() => IsDeadObject = true;
    public void OnReleased() { }

    public void Setup() => this.Register(GameManager.Instance); // 注册进游戏
}
```

---

## 6. LightInDark.Language — 多语言

**模块用途**：JSON 键值对多语言系统。语言文件位于游戏根目录 `Language/*.json`（文件名即语言名，如 `English.json`、`ChineseSimplified.json`）。**无需显式注册翻译键**——首次 `Translate` 一个不存在的键时，会自动以"占位"写入当前语言文件，之后再人工编辑该 JSON 即可。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `Language`（static） | 语言加载与翻译 |

### 关键成员

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 加载 | `static void Load()` | 加载 `Language/*.json` 全部语言包，并同步当前语言 |
| 更新 | `static void UpdateCurrentLanguage()` | 按游戏设置语言切换（缺失时回退 English） |
| 翻译 | `static string Translate(string key, string fallback = null)` | 查当前语言；缺失回退 English；均无则写"占位"并返回 `fallback ?? key` |
| 翻译 | `static string GetString(string key)` | `Translate(key, "*" + key)`，无词条时返回 `*键名` 便于排查 |
| 尝试 | `static bool TryGetString(string key, ref string output)` | 尝试获取，成功返回 true 并写出 |

### 使用示例

主插件 `HelpScreen.cs` 真实用法（所有 UI 文案走翻译，缺词条显示 fallback 中文）：

```csharp
using LightInDark.Language;

// 页签名
string roles = Language.Translate("help.tabs.roles", "职业");
string myInfo = Language.Translate("help.tabs.myInfo", "我的职业");

// 分类名
string crewmate = Language.Translate("role.category.crewmate", "船员");

// 拼接动态文案
string line = Language.Translate("help.overview.players", "当前玩家数") + ": " + playerCount;
```

语言文件（`Language/ChineseSimplified.json`）示例：

```json
{
  "help.tabs.roles": "职业",
  "role.category.crewmate": "船员",
  "help.overview.players": "当前玩家数"
}
```

---

## 7. LightInDark.RPCs — 网络通信

**模块用途**：基于 Reactor 模式的自定义 RPC 系统。**定义即用**——给静态方法打上 `[LidRPC]` 特性，调用该方法即自动：网络广播 + 本地执行（`LocalExecute` 模式，参考 Reactor `LocalHandling.Before`），并在所有客户端上执行方法体。另提供内置常用 RPC 定义与 `GameActions` 快捷操作。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `LidRPCAttribute`（Attribute） | 标记静态方法为 RPC |
| `LidRpcRegistry`（static） | RPC 自动注册器：扫描 `[LidRPC]` 方法并打 Harmony Prefix |
| `CustomRPC`（static） | 底层自定义 RPC 管理器（callId = 200），可手动注册/发送；`CurrentSender` 暴露本次分发的**真实发送者 PlayerId**（无法确定时为 255） |
| `CustomRpcHandlePatch` | 拦截 `InnerNetObject.HandleRpc` / `PlayerControl.HandleRpc` 的补丁，并把真实发送者透传给 `CustomRPC`（框架内部） |
| `MessageWriterExtensions` / `MessageReaderExtensions` | Hazel 读写扩展（Player/Vector2/Vector3） |
| `RpcDefinitions`（static） | 内置 `[LidRPC]` 方法与自由聊天事件 |
| `GameActions`（static） | 游戏操作快捷方法 |

### 关键成员

**`LidRPCAttribute`**

| 属性 | 默认值 | 含义 |
| --- | --- | --- |
| `OnlyHost` | false | 仅房主可发送（非房主调用被拦截） |
| `Reliable` | true | 可靠传输 |
| `LocalExecute` | true | 发送端是否本地执行方法体 |

> 序列化支持类型：`byte` `int` `float` `bool` `string` `PlayerControl` `Vector2` `Vector3`，其余类型不支持。

**`LidRpcRegistry`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 注册 | `static void ScanAndPatch(Harmony harmony)` | 扫描**调用方程序集**静态方法中的 `[LidRPC]`，注册接收器 + 打 Prefix 补丁（幂等） |

**`RpcDefinitions`（内置 RPC）**

| 方法 | 说明 |
| --- | --- |
| `SetRoleByCode(byte playerId, string codeName, int[] arguments)` [OnlyHost] | 同步职业（**推荐**）：按稳定内部名 `CodeName` 查 `RoleRegistry`，避免"按注册序号 Id 解析到另一个职业"；**早到的会被挂起**，等 `GameManager.Initialize()` 建表后由 `FlushPendingRoles()` 重放 |
| `SetRole(byte playerId, int roleId, int[] arguments)` [OnlyHost] | 旧"按 Id"版本，保留兼容；**内部转成 CodeName 路径**（挂起表也按 CodeName 存） |
| `SetRoleData(byte playerId, int dataId, int value)` [OnlyHost] | 写职业数据位（`RoleData` 专用；接收端落地并回调职业的 `OnRoleData`） |
| `Suicide(PlayerControl player, bool needLog = true, string state = "suicide")` | 自杀并触发 `PlayerSuicideEvent` |
| `MurderPlayer(PlayerControl killer, PlayerControl victim)` | 击杀并触发 `PlayerMurderEvent` |
| `RevivePlayer(byte playerId)` [OnlyHost] | 复活玩家并触发 `PlayerReviveEvent` |
| `SetPlayerInvisible(byte playerId, bool invisible)` | 隐身/显形 |
| `TeleportPlayer(byte playerId, Vector2 position)` [OnlyHost] | 传送玩家 |
| `StartMeeting(byte reporterId, byte reportedId)` | 指定报告人开会 |
| `RpcStartMeeting()` | 本地玩家召开紧急会议 |
| `ForceEndMeeting()` [OnlyHost] | 强制关闭会议 |
| `BreakEmergencyButton()` [OnlyHost] | 破坏紧急按钮并触发 `EmergencyButtonBrokenEvent` |
| `ShowChat()` / `HideChat()` | 显示/隐藏聊天，触发 `OnFreeChatStateChanged` |
| `SendChatMessage(byte playerId, string message)` | 以指定玩家身份发聊天并触发 `ChatMessageEvent` |
| `KickPlayer(byte playerId)` [OnlyHost] | 踢出玩家 |
| `SetKickReason(byte targetPlayerId, string reason)` | 设置被踢玩家的显示原因 |
| `KickPlayerWithReason(byte playerId, string reason)` [OnlyHost] | 带原因踢出 |
| `RequestPinnedRole(byte senderPlayerId, string roleName)` | 请求预定下一局职业（发送者 → 房主） |
| `CancelPinnedRole(byte senderPlayerId)` | 取消职业预定 |
| `ShowSystemMessage(byte targetPlayerId, string message)` | 定向系统消息（仅目标玩家本地显示；未就绪时挂起、逐帧补发） |

**`FlushPendingRoles()`**：由 `GameManager.Initialize()` 调用，把"表还没建好时收到的职业 RPC（按 CodeName 挂起）"重放一遍（幂等）。

> 落地 `ApplyRole` 时：① 持久数据存 **CodeName**（非本地化显示名，显示用 `LightPlayerData.DisplayRole` 反查）；② **中立职业**的 `Data.Role.TasksCountTowardProgress` 置 false（任务不计入团队进度）。

> ⚠️ **跨版本不兼容**：`SetRoleByCode` 是新 RPC，老客户端不识别 → 新旧版本混服时职业同步会**静默失败**（表现为"没职业"）。请确保全员同版本。

> **`OnlyHost` 集合**（非房主调用会被拦截）：`SetRoleByCode` / `SetRole` / `SetRoleData` / `RevivePlayer` / `TeleportPlayer` / `ForceEndMeeting` / `BreakEmergencyButton` / `KickPlayer` / `KickPlayerWithReason`。

**事件**：`public static event Action<bool> OnFreeChatStateChanged` —— 聊天显示状态变更（主插件订阅后刷新聊天框）。

**`GameActions`（静态快捷方法）**

| 成员 | 含义 |
| --- | --- |
| `AssignRole(Game.Player player, RoleTemplate role)` | 分配职业（同步） |
| `KillSelf(PlayerControl player, string reason = "suicide")` | 自杀 |
| `Murder(PlayerControl killer, PlayerControl victim)` | 击杀 |
| `Revive(PlayerControl player)` | 复活（仅房主） |
| `Teleport(PlayerControl player, Vector2 position)` | 传送 |
| `SetInvisible(PlayerControl player, bool invisible)` | 隐身/显形 |
| `StartMeeting()` / `ForceEndMeeting()` / `BreakEmergencyButton()` | 会议与紧急按钮操作 |
| `ShowChat()` / `HideChat()` / `SendMessage(PlayerControl, string)` | 聊天操作 |
| `Kick(PlayerControl player, string reason = "")` | 带原因踢出 |
| `AlivePlayers()` / `Impostors()` | 枚举存活玩家 / 内鬼 |
| `GetClosestPlayer(PlayerControl source, float maxDistance = 2f)` | 最近玩家 |
| `LocalPlayer` / `IsHost` / `InGame` | 便捷状态 |

### 使用示例

**① 调用内置 RPC**（主插件 `PatchManager.cs` / `CallerAbility`）：

```csharp
using LightInDark.RPCs;

// 自杀指令
RpcDefinitions.Suicide(PlayerControl.LocalPlayer);

// 显示/隐藏聊天
RpcDefinitions.ShowChat();
RpcDefinitions.HideChat();

// Caller 技能：召开紧急会议
RpcDefinitions.RpcStartMeeting();
```

**② 自定义 RPC（定义即用）**：

```csharp
using LightInDark.RPCs;

public static class MyRpcs
{
    [LidRPC]
    public static void SyncMyValue(byte playerId, int value, Vector2 pos)
    {
        // 所有客户端都会执行这里
        var pc = AmongUsClient.Instance.GetPlayerById(playerId); // 自行取玩家
        LightLogger.Log($"玩家 {playerId} 同步 value={value} pos={pos}");
    }
}
// 发送端直接调用即可（自动广播 + 本地执行）：
// MyRpcs.SyncMyValue(PlayerControl.LocalPlayer.PlayerId, 42, pos);

// 仅房主可发送：
[LidRPC(OnlyHost = true)]
public static void HostOnly(byte playerId) { /* ... */ }
```

**③ 订阅 API 事件**（主插件 `LightPlugin.Load()`）：

```csharp
using LightInDark.RPCs;

RpcDefinitions.OnFreeChatStateChanged += show => ShowChatPatch.NeedShowFreeChat = show;
```

**④ 踢出玩家**（主插件 `PatchManager.cs` 的 `/kick` 指令）：

```csharp
using LightInDark.Utilities;

AmongUsEdited.KickPlayer(target.ToLIDPlayer(), PlayerControl.LocalPlayer.name, reason);
```

---

## 8. LightInDark.Roles — 职业系统

**模块用途**：模组职业框架。**静态定义（`RoleTemplate`）+ 运行时实例（`RuntimeRoleTemplate`）** 两层结构；约定每个职业类持有 `public static readonly Xxx MyRole = new()` 单例。注册中心启动时自动扫描程序集发现全部子类，配置项走轻量 `RoleConfigItem` 声明，技能按钮统一经 `AbilityButtonFactory` 创建。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `RoleTemplate`（abstract） | 职业静态定义：CodeName、颜色、阵营、翻译键、分配参数、轻量配置项 |
| `RuntimeRoleTemplate`（abstract） | 职业运行时实例，绑定 `PlayerControl`，事件自动注册、按钮自动回收、名字显示 |
| `RoleRegistry`（static） | 职业注册中心：程序集自动扫描 + CodeName/类型/序号三索引 |
| `RoleData`（static） | **职业数据插槽**：每玩家可变状态（票数/剩余次数…）的注册与房主权威同步 |
| `RoleConfigItem`（LightInDark.Configuration） | 职业轻量配置项声明（注册器自动加 `role.<CodeName>.` 前缀） |
| `RoleDocumentType`（enum） | 职业文档类型：Normal / Html / MarkDown |
| `RoleDocument`（static，LightInDark.Documents） | 文档加载/渲染：读嵌入资源并转 TMP 富文本 |
| `AllocationParameters`（struct） | 职业分配参数（数量/必出/概率） |
| `IRoleTable`（interface） | 分配表：记录分配结果，`Determine` 时下发 |
| `IRoleAllocator`（interface） | 分配器：输入内鬼/其余玩家 ID，产出分配表 |
| `RoleUtils`（static） | 玩家查询工具：存活/阵营枚举、最近目标、随机玩家 |
| `PlayerTracker` | 玩家追踪器：每帧找最近合法目标并高亮 |
| `TrackerPredicates`（static） | 标准追踪过滤谓词工厂 |
| `RoleTypeChecker`（static） | 玩家职业类型检查扩展 |
| `AbilityButtonFactory`（static，LightInDark.UI.Ability） | 按钮创建统一入口：技能/效果/会议目标/会议按钮 |

### 关键成员

**`RoleTemplate`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 抽象属性 | `string CodeName { get; }` | 职业内部名（必填且全局唯一，翻译键与配置前缀都用它） |
| 虚拟属性 | `Color Color` / `RoleCategory RoleCategory` | 颜色（默认白）、阵营（默认船员） |
| 翻译键 | `NameKey` / `ShortDescribe` / `Describe` / `Intro` | 默认 `role.{CodeName}.name/shortdes/des/intro`，支持 `{CodeName}` 占位符 |
| 虚拟属性 | `RoleDocumentType DocumentType` | 文档类型：Normal / Html / MarkDown |
| 虚拟属性 | `string IntroSFX` / `string From` | 开场白音效资源路径、职业来源标注 |
| 解析属性 | `string Name` / `ShortDescribeText` / `IntroText` | 按语言键解析后的文案（Name 缺省回退 CodeName） |
| 解析属性 | `string DescribeText` | 按语言键解析的描述（仅 Normal 格式） |
| 文档方法 | `string GetDocumentText()` | 按 DocumentType 分发描述全文：Normal 走 DescribeText；Html/MarkDown 时 Describe 存嵌入资源路径（如 `"./Resources/Docs/x.html"`），经 `RoleDocument` 渲染成 TMP 富文本（结果缓存） |
| 虚拟属性 | `AllocationParameters Allocation` | 分配参数（默认不参与分配） |
| 虚拟属性 | `int[] DefaultArguments` | 实例化默认参数 |
| 虚拟属性 | `bool CanBeAssigned` | false = 不进抽选池（兜底/手动指定职业），也不出配置 |
| 虚拟属性 | `int TaskCount` / `bool CanReport` / `bool CanCallEmergencyMeeting` | 能否报告、能否按紧急按钮；**`TaskCount` = 可覆写的任务数，默认 `1` 表示"不干预"**（>1 从地图任务池补发未用过的任务、低于原版则截断；每次开局由 `RoleTaskCountPatch` 落地） |
| 虚拟属性 | `RoleConfigItem[] RoleConfiguration` | 职业轻量配置项声明（默认空数组） |
| 虚拟属性 | `UnityEngine.Sprite IconImage` | 帮助菜单立绘（直接给 Sprite）；**新代码建议改用 `RoleImagePath` + `RoleImage`**（路径加载 + 缓存） |
| **新增** 阵营细分 | `bool IsGhostRole`（=`RoleCategory == Ghost`）、`NeutralType NeutralType`、`string TeamCode` | 鬼魂判定、中立细分、同队标识 |
| **新增** 击杀能力 | `bool CanKill` / `float KillCooldown`（20f）/ `bool CanUseVents` | 声明是否为"击杀类"职业及其冷却、能否钻管道 |
| **新增** 块配色 | `Color? BlockColor` / `Color? BlockHighlightColor` + `ResolveBlockColor()` / `ResolveBlockHighlight()` | 职业块底色/高光（返回 null 时按阵营自动取色） |
| **新增** 立绘资源 | `string? RoleImagePath` / `static Func<string, Sprite?>? RoleImageLoader` / `Sprite? RoleImage` / `const float RoleImagePPU = 115f` | 立绘路径 + 加载器 + 懒加载结果（失败不再重试） |
| **新增** 可分配判定 | `bool IsSpawnable()` | "这个职业本局能不能出"的**唯一入口**（组合 CanBeAssigned / 配置 count / 阵营上限），异常返回 false |
| 键解析 | `string ResolveKey(string key)` | 替换键中的 `{CodeName}` 占位符 |
| 抽象方法 | `RuntimeRoleTemplate CreateRuntime(PlayerControl owner)` | 创建运行时实例（返回本职业的 Runtime 嵌套类） |
| 内部创建 | `internal RuntimeRoleTemplate CreateRuntimeFor(Game.Player player)` | 框架入口（取代旧 `CreateRuntimeInternal`） |

**`AllocationParameters`**：`MaxCount`（最大数量，0 不参与分配）、`GuaranteedCount`（必出数量）、`Chance`（出现概率 0-100）。

**`RuntimeRoleTemplate`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 属性 | `RoleTemplate Role { get; }` | **基类只读属性**（构造时由框架传入）；子类**不再 override**，也不必写 `=> MyRole` |
| 属性 | `PlayerControl Owner` / `Player MyPlayer` / `bool AmOwner` | 绑定原版控件、玩家包装、是否本人（`MyPlayer` 改为**懒加载 + 自愈**，避免建表时序差异导致永久为 null） |
| 属性 | `bool IsActive` / `bool IsDeadObject` | 激活状态、存活判定 |
| 便捷属性 | `CodeName` / `Name` / `Color` / `IntroText` / `IntroSFX` / `RoleCategory` | 转发模板 |
| 生命周期 | `internal void Activate()` / `public void Inactivate()` | 框架内部驱动：激活时自动 `EventSystem.RegisterInstance(this)`，失活时注销并释放 |
| 覆写点 | `protected virtual void OnActivated()` / `OnInactivated()` | 激活后挂按钮/执行技能；失活时清理 |
| **新增** 数据钩子 | `protected internal virtual void OnRoleData(int dataId, int value)` | `RoleData.Set` 同步落地时回调（房主权威值到达本地后） |
| **新增** 具名钩子 | `protected virtual void OnDied / OnRevived / OnExiled / OnDisconnected / OnMeetingStarted / OnMeetingEnded(对应事件 e)` | 常用事件封装成具名虚方法，激活时自动绑定、释放时自动注销 |
| 方法 | `void Release()` | 注销事件监听、回收本职业全部按钮、恢复名字颜色、销毁 Info 文本 |
| 方法 | `void UpdateNameDisplay()` | 名字颜色 = 角色色（仅自己），名字上方显示 角色名 (已完成/总任务) |
| 工具 | `AlivePlayers()` / `AllPlayers()` / `ClosestPlayer(...)` / `RandomAlivePlayer()` / `DistanceTo(...)` / `IsInRange(...)` | RoleUtils 便捷转发 |
| **新增** 底色规整 | `protected void NormalizeVanillaRole(bool asImpostor = false)` | 把底层**原版职业**改写为 `Impostor`/`Crewmate` 底色，避免原版技能按钮/计时继续显示（中立职业传 `false`，`IsImpostor` 保持 false） |

> `Activate`/`Release` 由框架在 `Player.SetRoleLocal` / 换职时调用；本实例全部事件方法（任意私有方法、单参数 IEvent 子类）随激活自动注册、随释放自动注销。

**`RoleRegistry`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 枚举 | `IReadOnlyCollection<RoleTemplate> AllRoles` | 全部已注册职业模板 |
| 自动注册 | `static void RegisterAssembly(Assembly asm)` | 扫描程序集中全部 RoleTemplate 非抽象子类并注册（加类文件即生效） |
| 注册 | `static RoleTemplate Register(RoleTemplate role)` | 手动注册（CodeName 空/重复时拒绝，返回 null） |
| **新增** 事件 | `static event Action<RoleTemplate>? RoleRegistered` | 注册成功时触发；`RoleConfigRegistrar` 据此**增量补注册**配置块（新职业一到就出配置，无需重启） |
| 查询 | `GetByName(string)` / `Get<T>()` / `GetById(int)` | 按 CodeName/类型/序号查询 |
| 查询 | `IsRegistered(string name)` | 是否已注册 |
| 清空 | `Clear()` | 清空注册 |

> `Id`（注册序号）由注册器自动分配；**职业同步 RPC 现已改用 `CodeName`**（`SetRoleByCode`），`Id` 仅旧 `SetRole` 路径使用。
> 注册时**优先反射取职业类里的 `public static readonly Xxx MyRole` 单例**（取不到才无参构造），并把分配的 `Id` **同步回该单例** —— 否则运行时侧与兜底分配都走 `MyRole`、其 `Id` 恒为 0，`RoleTable` 发出去的 roleId 会错。

**`RoleData`（职业数据插槽，2026-10-06 新增）**

把"每玩家可变状态"（市长票数、吸血鬼剩余次数…）交给框架同步，不用自己写 RPC。

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 注册 | `static int RegisterId(string key)` | 注册一个数据位（幂等），返回稳定 id；key 空返回 -1 |
| 查键 | `static string KeyOf(int dataId)` | 数据位的 key（日志/诊断用） |
| 读 | `static int Get(byte playerId, int dataId)` | 读（无记录为 0） |
| 写 | `static void Set(byte playerId, int dataId, int value)` | 写（**房主权威**：房主写→广播；客户端写→本地先生效，随后被房主值覆盖） |
| 增减 | `static void Add(byte playerId, int dataId, int delta)` | 读改写组合 |

> 落地时回调职业的 `RuntimeRoleTemplate.OnRoleData(dataId, value)`；状态挂在**每局**的 `Player` 包装上，换局自动清空。

**分配与自检（Light 侧，2026-10-06 新增）**

| 类型 | 职责 |
| --- | --- |
| `StandardRoleAllocator` | 分配器；新增公开 `static int GetMaxCount(RoleTemplate)` / `static int GetChance(RoleTemplate)`（读 `role.X.count/chance` 配置并夹紧；帮助页改用它，避免"说明与实际分配不一致"）；阵营上限改为**配置驱动**（私有 `MaxImpostorRoles/MaxNeutralRoles/MaxCrewmateRoles` 读 `lid.assignment.*`）；`PickByChance` 已删除 |
| `GhostRoleAllocator`（internal） | 玩家死亡时由房主授予鬼魂职业；无 Ghost 职业则直接返回 |
| `RoleHealthCheck`（internal） | 启动自检：Id 唯一/连续、翻译缺失、count 配置、职业表 SHA256 指纹 |
| `AssignmentConfigRegistrar`（internal） | 注册 `lid.assignment.crewmateMax / impostorMax / neutralMax`（默认 2/2/1） |

**`IRoleTable` / `IRoleAllocator`**

```csharp
public interface IRoleTable
{
    void SetRole(byte playerId, RoleTemplate role, int[] arguments = null);
    IEnumerable<(byte PlayerId, RoleTemplate Role)> GetPlayers(RoleCategory category);
    bool HasRole(byte playerId);
    void Determine();
}
public interface IRoleAllocator
{
    void Assign(List<byte> impostors, List<byte> others);
}
```

**`PlayerTracker`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 属性 | `Player CurrentTarget` | 当前追踪目标 |
| 属性 | `bool IsLocked` | 锁定目标（锁定后不切换） |
| 属性 | `Color HighlightColor` | 高亮颜色（默认黄） |
| 构造 | `PlayerTracker(Player source, float? maxDistance = null, Func<Player, bool>? predicate = null)` | 指定发起者/最大距离/过滤条件 |
| 方法 | `void Update()` | 每帧更新（找目标 + 高亮描边） |
| 方法 | `void Stop()` | 停止追踪并清除高亮 |

**`TrackerPredicates`**：`Standard(source)`（非自己、非死亡）、`Killable(source)`（标准 + 非内鬼）、`ImpostorTarget(source)`（同 Killable）。

**`RoleTypeChecker`（Player 扩展）**：`HasRole<T>()`（T 为 RoleTemplate 模板类型）、`GetRoleRuntime<T>()`（返回运行时实例）、`IsCategory(RoleCategory)`、`IsCrewmate()`、`IsImpostor()`、`IsNeutral()`、`IsAliveWithRole()`。

### 使用示例

**① 自定义职业（模板 + 运行时嵌套类）**——主插件 `Roles/Crewmates/Caller.cs`（按钮经 `AbilityButtonFactory` 创建）：

```csharp
using AmongUs.GameOptions;
using LightInDark.Configuration;
using LightInDark.Core;
using LightInDark.Roles;
using LightInDark.UI.Ability;
using LightInDark.RPCs;
using UnityEngine;

public class Caller : RoleTemplate
{
    public static readonly Caller MyRole = new();   // 约定单例

    public override string CodeName => "caller";
    public override Color Color => Color.Yellow;
    public override RoleCategory RoleCategory => RoleCategory.Crewmate;
    public override string IntroSFX => "./Resources/SFX/CallerIntro.mp3";
    public override RoleDocumentType DocumentType => RoleDocumentType.MarkDown;
    // MarkDown/Html 时 Describe 存嵌入资源路径而非语言键
    public override string Describe => "./Resources/Docs/Caller.md";

    /// <summary>分配参数：每局必出 1 名 Caller</summary>
    public override AllocationParameters Allocation => new() { MaxCount = 1, GuaranteedCount = 1, Chance = 100 };

    /// <summary>轻量配置项（注册器自动展开为 role.caller.* 配置）</summary>
    public override RoleConfigItem[] RoleConfiguration =>
    [
        new RoleConfigItem { Key = "Cooldown", Type = ConfigType.Float, Default = 20f, Min = 5f, Max = 60f, Suffix = ConfigSuffix.Seconds },
    ];

    public override RuntimeRoleTemplate CreateRuntime(PlayerControl owner)
        => new RuntimeInstance(owner, this);

    public class RuntimeInstance : RuntimeRoleTemplate
    {
        // ⚠️ 不再需要 `public override RoleTemplate Role => MyRole;`
        //    基类已从构造参数持有模板，`Role` 是基类只读属性（2026-10-06 契约变更）
        public RuntimeInstance(PlayerControl owner, RoleTemplate template) : base(owner, template) { }

        protected override void OnActivated()
        {
            if (!AmOwner) return;   // 只有本人挂按钮
            AbilityButtonFactory.Create(this, new RoleButtonConfig
            {
                LabelKey = "Button.Caller.label",
                KeyCode = KeyCode.E,
            }, () => RpcDefinitions.RpcStartMeeting());   // 技能：召开紧急会议
        }
    }
}
```

翻译键（`role.<CodeName小写>.name/shortdes/des/intro`，支持 {CodeName} 占位符）：

```json
{
  "role.caller.name": "传唤师",
  "role.caller.shortdes": "可以随时召开紧急会议",
  "role.caller.des": "……",
  "role.caller.intro": "你是传唤师！"
}
```

**② 注册职业**（主插件 `LightPlugin.LoadRole()`，自动扫描，加职业类文件即生效）：

```csharp
using LightInDark.Roles;

RoleRegistry.RegisterAssembly(typeof(LightPlugin).Assembly);
```

**③ 分配器 + 分配表**（主插件 `StandardRoleAllocator.cs` + `RoleTable.cs`，实现 `IRoleAllocator` / `IRoleTable`）：

```csharp
using LightInDark.Configuration;
using LightInDark.Events;
using LightInDark.Roles;

// 分配表：记录结果，Determine 时逐玩家下发 RPC
public class RoleTable : IRoleTable
{
    private readonly Dictionary<byte, (RoleTemplate Role, int[] Arguments)> _assignments = new();

    public void SetRole(byte playerId, RoleTemplate role, int[] arguments = null)
        => _assignments[playerId] = (role, arguments ?? role.DefaultArguments);

    public IEnumerable<(byte PlayerId, RoleTemplate Role)> GetPlayers(RoleCategory category)
        => _assignments.Where(kv => kv.Value.Role.RoleCategory == category).Select(kv => (kv.Key, kv.Value.Role));

    public bool HasRole(byte playerId) => _assignments.ContainsKey(playerId);

    public void Determine()
    {
        foreach (var kv in _assignments)
            RpcDefinitions.SetRole(kv.Key, kv.Value.Role.Id, kv.Value.Arguments);
    }
}

// 分配器：内鬼 → 中立 → 船员抽选，未命中的玩家兜底为普通职业（Impostor/Crewmate 模板，CanBeAssigned=false）
public class StandardRoleAllocator : IRoleAllocator
{
    public void Assign(List<byte> impostors, List<byte> others)
    {
        var table = new RoleTable();
        Roll(table, impostors, BuildPool(RoleCategory.Impostor), 2);   // 阵营上限：分配器内部读 lid.assignment.impostorMax
        // ... 抽选逻辑（参考主插件实现）

        EventSystem.RunEvent(new PreFixAssignmentEvent(table)); // 允许他人修正分配表
        table.Determine();                                      // 逐玩家下发职业 RPC
    }
}

// 在拦截 RoleManager.SelectRoles 时调用（主插件 RoleInterceptPatch.cs）：
// GameManager.Instance.Initialize();
// ... 洗牌选内鬼 ...
// new StandardRoleAllocator().Assign(impostors, others);
```

> **原版分配已被接管（2026-10-07）**：`RoleInterceptPatch.VanillaSelectRolesBlockPatch` 对 `RoleManager.SelectRoles` 打 **Prefix 返回 false**（仅主机 + `GameModes.Normal`），在其中复刻原版"收集名单 → 按 `GetAdjustedNumImpostors` 定内鬼数 → 随机挑人"，但**只发 `Impostor`/`Crewmate` 底色职业**（`AssignBaseRoles()`），绝不让原版发出技能职业；随后我们的 `RoleSelectPatch.Postfix` 照常叠加自定义职业。非普通模式或任何异常一律 `return true` 退回原版。

**④ 玩家追踪**（技能索敌 + 高亮）：

```csharp
using LightInDark.Roles;

// 追踪附近可击杀目标（最大距离 2.5）
var tracker = new PlayerTracker(MyPlayer, 2.5f, TrackerPredicates.Killable(MyPlayer));

// 每帧调用（AbilityButton.Update 驱动）
tracker.Update();

if (tracker.CurrentTarget != null)
{
    // 对目标执行操作
}

// 锁定当前目标 / 停止追踪
tracker.IsLocked = true;
tracker.Stop();
```

**⑤ 玩家职业检查**：

```csharp
using LightInDark.Roles;

if (player.HasRole<Caller>()) { }                   // 按模板类型判断（T 为职业定义类）
var runtime = player.GetRoleRuntime<Caller>();      // 获取运行时实例
if (player.IsImpostor()) { }                        // 阵营判断
```

**能力按钮系统（`LightInDark.UI.Ability`，2026-10-06 增强）**

| 类型 | 关键新增成员 |
| --- | --- |
| `RoleButtonBase` | 缓存 `protected ActionButton Button`；`Predicate<float> CanRunCooldown`；公开 `UsesLeft/HasLimitedUses/IsVisible/IsBroken/StartCooldown/StopCooldown/SetHudActive/SetVisible/PlayFlash/Break/ShowUsesIcon/UpdateUsesIcon/HideUsesIcon`；覆写点 `OnCooldownFinished/UpdateCooldownDisplay/ShouldShow/ShouldBeUsable/HandleClick/HandleSubClick`；**新增** `protected virtual bool ShouldTickCooldown()`（通风管/变形/会议/放逐/开场/角色菜单/非白名单小游戏中**不推进冷却**）；`InputBlocked()`（小游戏/地图/会议/聊天聚焦时屏蔽热键与鼠标） |
| `RoleButtonManager` | `Register` / `CreateAllHudButtons` / `RegisterTracker` / `UpdateAll` / `SetAllVisible` / `OnMeetingStart` / `OnMeetingEnd` / **`ReleaseButtonsOf(RuntimeRoleTemplate)`** / **`SuppressVanillaAbilityButtons()`**（本地玩家有自定义职业时隐藏原版 `AbilityButton`/`SecondaryAbilityButton`）/ `Clear` / `Count` |
| `EffectButton` | `AllowCancelByReclick` / `EffectDuration` / `IsInEffect` / `EffectTimeRemaining` / `OnEffectStart` / `OnEffectEnd` / `ShowEffectCountdown` / `StartEffect` / `StopEffect`；**新增覆写** `UpdateCooldownDisplay()`（效果进行中刷进度环） |
| `AbilityButton` / `MeetingAbilityButton` | 换图标走 `protected void ApplyIcon(Sprite)`，内部 `SetCooldownNormalizedUvs()` 重算冷却遮罩 UV（修复换图标后遮罩错位） |
| `MeetingAbilityButton` / `MeetingTargetButton` | 会议期按钮：`Position` / `Rotation` / `Create(...)` |
| `HudGrid` | `Instance` / `Ensure` / `RegisterContent(HudContent, bool)` / `Tick`；`HudContent` 公开 `CurrentPos/OccupiesLine/IsStaticContent/ActiveFunc/Priority` |
| `RoleButtonConfig` | 新增 `LabelType`（`ButtonLabelType`）/`FlashWhile`/`BrokenIcon`/`SubAction`/`SubHotkey`/`UseByMouseClick`/`IsLeftSide`/`Priority`/`ArrangedAsKillButton`/`OccupiesLine`/`ShouldBeInLastLine`/`CanRunCooldown` 及对应 `Set*`（含 `SetCanRunCooldownPredicate`） |
| `AbilityButtonFactory` | `Create` / `CreateKill`（改用 `RoleTeam.IsTeammate` 排队友）/`CreateEffect` / `CreateMeetingTarget` / `CreateMeetingAbility` |

> **`RoleButtonConfig` 默认条件（2026-10-07 起，PR #7）**：`CanUse = () => PlayerControl.LocalPlayer.CanMove`、`CanShow = () => !PlayerControl.LocalPlayer.Data.IsDead`、`CanRunCooldown = t => PlayerControl.LocalPlayer.IsKillTimerEnabled`。
> 语义：**默认就"能移动时可用、死亡时隐藏、按击杀计时器跑冷却"**。需要别的语义请显式 `SetCanUse` / `SetCanShow` / `SetCanRunCooldownPredicate` 覆盖（`Jackal`、`CreateKill` 就是把条件直接并进 `CanUse`）。

> 按钮可用性与 `InputBlocked()` 现在由框架每帧统一驱动，技能实现不必自己处理"小游戏/会议/聊天聚焦时不可点"。

---

## 9. LightInDark.UI.Window — 窗口 GUI

**模块用途**：声明式 GUI 系统。二次开发者通过 `IGUI` 工厂构建 **Widget 树**（文本/按钮/图片/滚动视图/容器），再交给 `IGUIScreen`（主插件实现为 `MetaScreen`）实例化显示。API 只提供接口与数据结构，主插件的 `LIDGUI : IGUI`、`MetaScreen : IGUIScreen`、`VanillaAsset` 等提供实现与资源。

### 类型列表（GUI.cs）

| 类型 | 职责 |
| --- | --- |
| `GUIAlignment`（enum, Flags） | Widget 对齐方式：Center/Left/Right/Bottom/Top 及四角组合 |
| `Size`（struct） | 尺寸（Width/Height），与 Vector2 互转 |
| `FuzzySize`（struct） | 模糊尺寸（只指定宽或高，另一维自适应） |
| `Anchor`（record） | 锚点：屏幕基准点 + 空间偏移 |
| `GUIWidget`（abstract） | Widget 基类：`Instantiate(Size, out Size)` 实例化 |
| `IGUIScreen`（interface） | GUI 屏幕：`SetWidget(GUIWidget?, out Size)` |
| `GUIWidgetSupplier`（delegate） | Widget 供应器（`GUIWidget Supplier()`） |
| `GUIClickAction`（delegate） | 点击回调（`void(GUIClickable)`） |
| `GUIClickable` | 点击元素封装（含 `PassiveButton? Button`） |
| `IGUI`（interface） | GUI 工厂：全部 Widget 构建方法 |

### 类型列表（Text.cs）

| 类型 | 职责 |
| --- | --- |
| `TextAlignment`（enum） | 对齐：Left/Right/Center |
| `FontStyle`（enum, Flags） | 字体样式：Normal/Bold/Italic |
| `FontSize` | 字号：默认/最小/最大 + 自适应开关 |
| `FontAsset`（enum） | 字体资源标识：Prespawn/Barlow/Gothic/GothicMasked/Oblong/OblongMasked |
| `Font` | 字体信息（TMP_FontAsset + Material + IsMasked） |
| `TextAttribute` | 文本属性集合（对齐/字体/样式/字号/尺寸/颜色/弹性/换行/描边） |
| `AttributeParams`（enum, Flags） | 属性位标志与预设（Standard、StandardBold、StandardLeft 等） |
| `AttributeTemplateFlag`（enum, Flags） | 模板标志（同 AttributeParams 位） |
| `AttributeAsset`（enum） | 预定义文本属性资源：`DocumentStandard`、`DocumentTitle`、`CenteredBold`、`OverlayTitle` 等 |
| `TextComponent`（interface） | 文本组件接口：`GetString()` |
| `RawTextComponent` | 原始文本 |
| `TranslateTextComponent` | 翻译文本（按键翻译） |
| `ColorTextComponent` | 着色文本（包裹内层组件生成富文本） |
| `LazyTextComponent` | 懒加载文本（Func 延迟求值） |

### 类型列表（UnityHelper.cs）

| 类型 | 职责 |
| --- | --- |
| `UnityHelper`（static） | 创建带组件/不带组件的 GameObject（自动设 UI 层、父级、缩放） |
| `LayerExpansion`（static） | 获取 UI 层 / Default 层 |

### 关键成员

**`IGUI` 工厂方法**（主插件通过 `LIDGUI.Instance` 获取实现）：

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 文本 | `GUIWidget RawText(GUIAlignment, TextAttribute, string)` | 原始文本 |
| 文本 | `GUIWidget LocalizedText(GUIAlignment, TextAttribute, string translationKey)` | 翻译文本 |
| 文本 | `GUIWidget RealtimeText(GUIAlignment, TextAttribute, Func<string>, float width)` | 实时刷新文本 |
| 按钮 | `RawButton(...)` / `LocalizedButton(...)` / `Button(...)` | 三种按钮（原始/翻译/自定义组件），均支持 onMouseOver/onMouseOut/onRightClick、color、selectedColor、margin |
| 图片 | `GUIWidget Image(GUIAlignment, Sprite, FuzzySize, onClick = null, overlay = null, isMasked = true)` | 图片（可点击） |
| 滚动 | `GUIWidget ScrollView(GUIAlignment, Size, string? scrollerTag, GUIWidget? inner, out object artifact)` | 滚动视图 |
| 容器 | `VerticalHolder(...)` / `HorizontalHolder(...)` / `Arrange(alignment, widgets, perLine)` | 纵/横/网格布局 |
| 间距 | `Margin(FuzzySize)` / `VerticalMargin(float)` / `HorizontalMargin(float)` | 空白占位 |
| 属性 | `TextAttribute GetAttribute(AttributeAsset)` / `GenerateAttribute(AttributeParams, Color, FontSize, Size)` | 取预定义/生成文本属性 |
| 组件 | `RawTextComponent(string)` / `TranslateTextComponent(string)` / `ColorTextComponent(Color, TextComponent)` | 文本组件工厂 |

**`GUIWidget` 基类**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| 属性 | `abstract GUIAlignment Alignment` | 对齐方式 |
| 方法 | `abstract GameObject? Instantiate(Size size, out Size actualSize)` | 实例化为 GameObject |
| 方法 | `virtual GameObject? Instantiate(Anchor anchor, Size size, out Size actualSize)` | 按锚点实例化 |
| 属性 | `bool PostponesConsideringSize` | 延迟尺寸计算（同一位置可叠放） |

**`TextAttribute` 构造**：`TextAttribute(TextAlignment alignment, Font font, FontStyle style, FontSize fontSize, Size size, Color color, bool isFlexible, float? outlineWidth = null)`；另有拷贝构造。

### 使用示例

**① 构建窗口内容**（主插件 `MetaUI.cs` / `HelpScreen.cs` 真实用法，`LIDGUI` 与 `MetaScreen` 为主插件侧实现）：

```csharp
using LightInDark.UI.Window;
using Color = LightInDark.Color;

var gui = LIDGUI.Instance;   // IGUI 实现（主插件 Light.UI.Window.LIDGUI）

// 用 IGUI 工厂构建 Widget 树
var content = gui.VerticalHolder(GUIAlignment.Center,
    gui.RawText(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.DocumentTitle), "标题"),
    gui.VerticalMargin(0.3f),
    gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.CenteredBold), "按钮",
        clickable => { /* 点击回调 */ },
        color: Color.Green),
    gui.VerticalMargin(0.2f),
    gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.DocumentStandard), "关闭",
        clickable => { }, color: Color.Gray)
);

// 生成窗口（主插件 MetaScreen : IGUIScreen）
var screen = MetaScreen.GenerateWindow(
    new Vector2(4.5f, 3f),
    HudManager.Instance != null ? HudManager.Instance.transform : null,
    new Vector3(0f, 0f, -50f),
    withCloseButton: true,
    withBlackScreen: true
);
screen.SetWidget(content, out _);   // 实例化 Widget 并挂到屏幕
```

**② 页签栏 + 按钮列表 + 滚动视图**（主插件 `HelpScreen.cs`）：

```csharp
var gui = LIDGUI.Instance;

// 按钮属性：居中、Gothic 粗体、字号 1.6、尺寸 0.82×0.21、白色
var tabButtonAttr = new TextAttribute(
    TextAlignment.Center, gui.GetFont(FontAsset.Gothic), FontStyle.Bold,
    new FontSize(1.6f, false), new Size(0.82f, 0.21f), Color.White, false);

// 横向排列的按钮组
var buttons = new List<GUIWidget?>
{
    gui.RawButton(GUIAlignment.Center, tabButtonAttr, Language.Translate("help.tabs.roles", "职业"),
        _ => OnTabClicked(), color: Color.White, selectedColor: Color.White),
    gui.HorizontalMargin(0.1f),
    gui.RawButton(GUIAlignment.Center, tabButtonAttr, Language.Translate("help.tabs.overview", "概览"),
        _ => OnTabClicked(), color: Color.Gray, selectedColor: Color.Gray),
};
var tabBar = gui.HorizontalHolder(GUIAlignment.Center, buttons.ToArray());

// 滚动视图（内容超长可滚动）
var inner = gui.VerticalHolder(GUIAlignment.Left,
    gui.RawText(GUIAlignment.Left, gui.GetAttribute(AttributeAsset.DocumentTitle), "职业列表"),
    gui.VerticalMargin(0.1f),
    /* ...更多行... */
);
var scroll = gui.ScrollView(GUIAlignment.Center, new Size(6.9f, 2.65f), null, inner, out _);
```

**③ 着色文本 / 翻译文本组件**：

```csharp
// 着色 + 翻译组合（ColorTextComponent 包裹 TranslateTextComponent）
var colored = gui.ColorTextComponent(role.Color, new TranslateTextComponent("role.name"));
// 或着色原始文本
string coloredName = gui.ColorTextComponent(role.Color, new RawTextComponent(role.Name)).GetString();
```

**④ UnityHelper 创建对象**：

```csharp
using LightInDark.UI.Window;

// 创建带 SpriteRenderer 的 GameObject（自动 UI 层）
var renderer = UnityHelper.CreateObject<SpriteRenderer>("Icon", parent, Vector3.zero);
// 创建普通 GameObject
var obj = UnityHelper.CreateObject("MyObject", parent, new Vector3(0, 1, 0), LayerExpansion.GetUILayer());
// 查找渲染 UI 层的相机（UI 相机最后渲染，把窗口挂其下可确保盖住场景内所有 UI）
var uiCam = UnityHelper.FindCamera(LayerExpansion.GetUILayer());
```

### 版本记录

| 版本 | 更新内容 |
| --- | --- |
| 2026-08-07 | `UnityHelper` 新增 `FindCamera(int layer)`：按图层查找相机，回退主相机 |

---

## 10. LightInDark.Utilities — 工具

**模块用途**：杂项工具集合，提供屏幕闪红、踢人、断线弹窗、服务器/大厅判断等操作。

### 类型列表

| 类型 | 职责 |
| --- | --- |
| `LightUtils`（static） | 任务数查询、屏幕闪烁、客户端查询、踢人、断线弹窗、状态判断 |
| `LightUtils.KickManager`（嵌套 static） | 被踢原因显示管理 |

### 关键成员

**`LightUtils`**

| 成员 | 签名 | 含义 |
| --- | --- | --- |
| **新增** 任务数 | `static int GetCurrentTaskCount(PlayerControl player = null, bool countOnlyProgress = false)` | 取玩家当前任务数（null = 本地玩家）；`countOnlyProgress=true` 时仅"计入进度"者才算（中立职业被置为不计进度）；任务表在 `ShipStatus.Begin` 才发，开局前为 0 |
| 闪烁 | `static void PlayFlash(Color color, float fadeIn, float hold, float fadeOut)` | 全屏颜色闪烁（淡入/保持/淡出） |
| 闪烁 | `static void PlayFlash()` / `PlayFlash(Color color)` | 默认参数闪烁（红、0/0.15/0.3） |
| 查询 | `static ClientData? GetClient(PlayerControl player)` | 获取玩家对应客户端数据 |
| 踢人 | `static void KickPlayer(Player p, string kickerName, string reason = null)` | 房间内广播原因并带原因踢出（`p` 为 `LightInDark.Game.Player`） |
| 转换 | `static Player ToLIDPlayer(this PlayerControl pc)` | PlayerControl 转模组 Player |
| 弹窗 | `static void ShowCustomDisconnectWindow(string text)` | 显示自定义文本的断线风格弹窗 |
| 判断 | `static bool IsCustomServer()` | 是否自定义服务器 |
| 判断 | `static bool IsInLobby()` | 是否在大厅 |

**`KickManager`**：`kickReason`（原因文本）、`kickReasonWaitUntil` / `kickReasonConsumeUntil`（时间戳）、`Clear()`。

### 使用示例

主插件 `PatchManager.cs` 的 `/kick` 指令：

```csharp
using LightInDark.Game;
using LightInDark.Utilities;

// 找到目标玩家
PlayerControl target = null;
foreach (var p in PlayerControl.AllPlayerControls)
    if (p.Data.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase)) { target = p; break; }

string reason = parts.Length >= 3 ? string.Join(' ', parts.Skip(2)) : "无";
AmongUsEdited.KickPlayer(target.ToLIDPlayer(), PlayerControl.LocalPlayer.name, reason);
```

其他常用操作：

```csharp
// 全屏红闪提示（如被袭击）
AmongUsEdited.PlayFlash(Color.Red, 0.1f, 0.2f, 0.4f);
// 或使用默认参数
AmongUsEdited.PlayFlash();

// 自定义断线弹窗
AmongUsEdited.ShowCustomDisconnectWindow("你被移出了房间");

// 状态判断
if (AmongUsEdited.IsInLobby()) { /* 大厅内 */ }
if (AmongUsEdited.IsCustomServer()) { /* 自定义服务器 */ }
```

---

## 版本记录

| 日期 | 说明 |
| --- | --- |
| 2026-08-08 | 初版：覆盖全部 10 个 API 模块（Color、Configuration、Core、Events、Game、Language、RPCs、Roles、UI.Window、Utilities），示例基于主插件真实用法 |
| 2026-08-08 | 新增 IRoleDocument 职业文档接口（IconImage/IntroBlurb/SkillDescription），DefinedRole 提供空默认实现 |
| 2026-08-25 | `RoleConfig` 新增 Set 写回方法（`SetRoleCount`/`SetRoleChance`/`SetInt`/`SetBool`/`SetFloat`），Get/Set 共享缓存 `ConfigEntry`；新增 `GameEndReason.CustomJesterWin = 101` 与 `EndGameReasonHelper` |
| 2026-08-25 | 主菜单 `OptionsMenuBehaviour` 注入独立"模组设置"标签页（`Light.Patches.SettingsTabPatch`，克隆 `SettingsTab` 为模板插至 `Done` 之前，`Content` 为透明 `MetaScreen.GenerateBlankWindow`，用 `LIDGUI` 声明式构建），并删除原版"职业设置"页签（`FindRoleTabIndex`：先按名称含 `role` 匹配，兜底索引 0 当 `Tabs.Count==4`）；房间内 `GameSettingMenuPatch`（`GameSettingMenu.Start` Postfix）删除原版角色设置页签（`GetChild(4).GetChild(2)`，整体 `GameObject.Destroy`）；新增语言资源键 `settings.title`（模组设置 / Mod Settings） |
| 2026-10-01 | `Role` 新增 `BuildConfigurations(ConfigBlock)` 虚方法：职业声明专属配置项（通用数量/概率由 RoleConfigRegistrar 自动生成） |
| 2026-10-04 | 按钮系统（`LightInDark.UI.Ability`）：`RoleButtonManager` 新增 `ReleaseButtonsOf(Role)`（按职业释放按钮）；`Role.Release()` 现自动回收本职业全部按钮（换职业不再残留重复按钮）；冷却显示由框架统一驱动——`RoleButtonBase` 新增 `UpdateCooldownDisplay()` 虚方法与 `Button` 属性（子类可覆写自定义 CD 表现），`AbilityButton` 创建时初始为满冷却遮罩、冷却期间每帧写回进度遮罩与倒计时数字，`OnCooldownFinished` 自动清除残留遮罩 |
| 2026-10-04 | 职业预定（`/up` 指令）：新增 `LightInDark.Roles.Assignment.RolePinManager`（房主侧预定表：`HandleRequest`/`HandleCancel`/`TryGetPin`/`Consume`/`Clear`，按 PlayerId 记录、分配时消耗、未上局保留）；`RpcDefinitions` 新增 `RequestPinnedRole(byte,string)`、`CancelPinnedRole(byte)`（发送者→房主）与 `ShowSystemMessage(byte,string)`（定向系统消息，仅目标玩家本地显示）；`StandardRoleAllocator` 分配时预定优先消耗类别名额（阵营不符/名额满→预定保留，职业已关闭→消耗并回执），其余玩家按剩余名额正常抽选 |
| 2026-10-04 | **职业框架重构**：`DefinedRole`/`RuntimeRole` 更名为 `RoleTemplate`/`RuntimeRoleTemplate`（旧基类删除）。新约定：职业类持有 `MyRole` 单例、运行时类为嵌套类 `RuntimeInstance`；`CanSpawnIn()` 改为 `CanBeAssigned`（false = 兜底/手动指定职业，不出配置不进池）；文案全部走翻译键 `role.{CodeName}.name/shortdes/des/intro`（`Name/ShortDescribeText/DescribeText/IntroText` 取代 Name/Description/IntroBlurb/SkillDescription）；配置项走 `RoleConfigItem` 轻量声明（自动 `role.<CodeName>.` 前缀）；`RoleRegistry.RegisterAssembly` / `EventSystem.RegisterAssembly` 程序集自动扫描；新增 `AbilityButtonFactory` 统一按钮创建（`RuntimeRoleTemplate.Release` 自动回收全部按钮）；新增普通船员/普通内鬼兜底模板（CodeName：crewmate/impostor），分配器对未命中玩家兜底下发 |
| 2026-10-04 | **GUID 变更**：LightAPI `com.moonscar.lightapi` → `cn.moonscar.lightapi`，Light 本体 `cn.moonscar.lid`；版本统一 0.0.1；LightPlugin 新增 7 个 `BepInIncompatibility` 声明 |
| 2026-10-04 | **Html/MarkDown 文档渲染实装**：新增 `LightInDark.Documents.RoleDocument` 静态类（嵌入资源读取 + Html/MarkDown → TMP 富文本渲染，结果缓存）；`RoleTemplate` 新增 `GetDocumentText()` 按 `DocumentType` 分发；Html/MarkDown 时 `Describe` 键存嵌入资源路径（约定同 SfxManager）；帮助页职业详情已接入 |
| 2026-10-04 | **职业自动接入设置与 H 菜单**：设置界面职业页签（船员/内鬼/中立）现自动平铺全部 `lid.role.*` 配置块为职业按钮（点击进入该职业独立配置页，`RoleListPage.Show` 由死代码正式启用）；新增职业类经 `RoleRegistry` 注册后无需改代码即自动出现在设置与 H 菜单（`CanBeAssigned == false` 的兜底职业不出配置）；H 菜单各页构建异常时显示降级提示并记录错误日志（不再静默白屏），职业列表按注册序号稳定排序 |
| 2026-10-04 | **清理合并冲突 + 修复「设置看不到职业」**：`2fa4c6a` 合并提交残留 `<<<<<<< / ======= / >>>>>>>` 冲突标记（`LightPlugin.cs`、`LightOptionsRegistry.cs`，导致源码无法编译、改动无法进游戏），已清理；修复 MOD 设置页容器 `LightModSettingsPage` 挂载位置——原挂在 `RoleSettingsTab` 下，切 MOD 类页签时被 `ChangeTabPrefix` 整体 `SetActive(false)` 连带隐藏，改挂到其父级并换算世界变换，职业按钮列表与占位文字恢复正常显示 |
| 2026-10-05 | **H 菜单（帮助界面）重构 + 渲染栈修复**：`GUIScrollView` 修复 `SpriteMask` 缩放（改为按 `sprite.bounds` 反算；原先假设 sprite 为 1 世界单位，实际 `FullScreenSprite` 仅约 0.04，遮罩仅约 0.28×0.16 → 滚动区内职业按钮/说明/搜索结果被裁光）；`ApplyMask` 改为仅当材质与当前字体图集一致时才套用蒙版材质（消除「整字显示成别的字」）；`InnerScreen.SetWidget` 改为旧内容先脱离父级再销毁（消除同帧新旧并存）。新增 `Light.UI.Window.CjkFont`（`EnsureFallback()` 把 `NotoSansSC/JP/KR-Regular SDF` 注入所有 `TMP_FontAsset.fallbackFontAssetTable`，按语言定首选项，`LoadPatch.EnsureChineseFont` 启动时调用，`Invalidate()` 供语言切换重注入）。`Widgets.NoSGUIText` 文本统一改用 `fontSharedMaterial`，支持 `Wrapping` 换行并按 `textBounds` 自适应高度（`TextAttribute` 主构造新增可选参数 `wrapping`）。`GUITextField` 重写支持中文 IME（`imeCompositionMode`/`compositionString`/`compositionCursorPos` + 闪烁光标，公开接口不变）。`HelpScreen` 重构：UI（窗口 7.8×4.7、左页签栏+右内容、页签顺序与配色、详情子窗 7×4.5+导航）与功能入口不变，修复中文显示、中文搜索输入、搜索结果、职业列表与说明换行 |
| 2026-10-06 | **H 菜单交互与素材更新**：① 删除「幻灯片」页签（`HelpTab.Slides` 及全部引用）；② 修复横向溢出——内容区滚动宽度 `7.4→6.1`、搜索框宽 `5→4.5`，使「页签栏+间距+内容」容纳于窗口 7.8；③ 修复切页签时页签栏上下浮动——`SetWidget` 改用居中锚点 `(0.5,0.5)`（原默认锚点按「顶边对齐」使根节点 y 随内容高度变化），详情子窗同步；④ 替换窗口素材：`GUI/SettingGUI/CloseButton.png`（左默认/右悬浮）、`GUI/SettingGUI/NavButton.png`（左列悬浮态、右列默认态，上排左箭头、下排右箭头），并同步修正 `HudUIAssets.NavLeft*/NavRight*` 的切片索引；⑤ H 菜单接入 `UiModalGuard` 模态遮罩（打开时 `Push` 窗口根、关闭 `Pop`，并在 `HelpKeyPatch` 每帧 `Sweep`），打开 H 菜单期间自动临时禁用不属于本窗口的原版控件，修复误触背景聊天/设置按钮的问题；职业详情子窗同样登记，避免其按钮被遮罩误禁 |
| 2026-10-06 | **修复聊天框「删字冒残字、之后输入错乱」**：① `RichTextInputPatch.IsCharAllowed` 由「全盘接管放行规则」改为**只补 `<` `>`**、其余字符一律交回原版判断（自写规则与原版任一处不一致都会让 TMP 字符/光标索引错乱）；② `ChatRichTextPatch` 关闭输入框富文本时**同时**同步 `TMP_InputField.richText`（原先只关文本组件，字段仍按「解析后」索引 → 删字时索引与显示错位）。③ ESC 现在按层级关闭：有职业详情二级窗口时先关详情，再按一次才关 H 菜单（新增 `HelpScreen.TryCloseTopWindow`） |
| 2026-10-06 | **修复「打中文吞字」**：① 根因——`GUITextField.TextFieldBehaviour` 失焦时把**全局** `Input.imeCompositionMode` 置为 `Off` 且不还原，导致此后原版聊天框的输入法整体失效（中文打不进/被吞）。改为进入编辑前记录旧值、失焦时还原（默认 `Auto`），`OnDisable/OnDestroy` 路径一并覆盖。② `RichTextInputPatch.IsCharAllowed` 改为 **Postfix 只做补放行**（原版已放行的不动；原版拒绝时由白名单补放行中文/全角标点/`<` `>`），并显式挡掉 emoji/私有使用区等无字形区段，兼顾「中文能打」与「不取消原版行为、不产生豆腐块」 |
| 2026-10-06 | **修复「删字冒方块 + 后续输入报废」复发**：`RichTextInputPatch.IsCharAllowed` 补上**否决权**——先否决危险字符（代理对=emoji 半个、零宽/双向控制 U+200B–200F/2028–202E/2060–206F、软连字符、BOM U+FEFF、杂项符号、私有使用区、变体选择符等），再交回原版结论，最后才按白名单补放行中文/全角标点。原因：仅"补放行"无法阻止原版放行的零宽字符或**代理对的一半**漏入，删字留下孤立代理/零宽字符会让光标与输入整体错乱。另 `ChatControlPatch` 历史记录回填走 `Sanitize`（剥掉代理对/控制字符/BOM），因为 `SetText` 不经过输入校验 |
| 2026-10-06 | **修复「删字冒方块」根因（日志实证）**：诊断日志打出输入框内容 `U+0031 U+0032 U+0008`（即 `"12\b"`）——**退格键被当成普通字符插进了文本**。Among Us 的输入循环「先问 `IsCharAllowed`、再处理退格」，原版对 `'\b'` 返回 `false` 才落删除分支；我们的过滤一直为 `'\b'` 开例外（返回 `true`），于是退格被插入 → U+0008 无字形渲染成方块、且控制字符污染 TMP 字符/光标索引导致后续输入全乱。**改为 `'\b'` 一律不放行**（由 `char.IsControl` 覆盖，不再开例外）。同时 `ChatRichTextPatch` 不再直接写 `outputText.richText`（避免与 `TMP_InputField` 抢文本组件管理权），只设 `TMP_InputField.richText`；并保留 `DiagnoseChatText` 诊断（每次出现新可疑码位打一条告警日志） |
| 2026-10-06 | **缓解「小概率吞字」——把聊天相关逻辑移出输入链路**：原先预览按钮亮灭/诊断挂在聊天框 `OnChange` 上（该事件在**输入处理链路内同步触发**，每敲一字都执行"读文本+改颜色+写日志"），快速输入或输入法提交那一帧的额外开销可能挤掉字符。改动：① 新增 `ChatPreviewDriver`（独立 MonoBehaviour，`Update` 轮询，`ClassInjector` 注册），改用轮询驱动 `ChatRichTextPatch.Tick`，**不再挂 OnChange**；② `RefreshPreviewButton` 加状态去重（亮灭与长度未变即直接返回），并移除每键一条的 debug 日志（原日志量已出现"同类日志被省略"提示） |

| 2026-10-06 | **PR #6（合作者全项目审查 + 输入修复）**：① 新增 **`RoleData` 职业数据插槽**（`RegisterId/KeyOf/Get/Set/Add`；房主权威同步 RPC `SetRoleData`，落地回调 `OnRoleData`），`Player` 增 `GetRoleData/SetRoleData`；② `RoleTemplate` 增 `IsGhostRole`/`NeutralType`/`TeamCode`/`CanKill`/`KillCooldown`/`CanUseVents`/`BlockColor`/`BlockHighlightColor`+`Resolve*`/`RoleImagePath`/`RoleImageLoader`/`RoleImage`/`RoleImagePPU`/`IsSpawnable()`/`CreateRuntimeFor`；③ `RuntimeRoleTemplate.Role` 由**抽象属性改为基类只读属性**（子类不再 override `=> MyRole`），`MyPlayer` 改懒加载自愈，新增 `OnRoleData` 与具名钩子 `OnDied/OnRevived/OnExiled/OnDisconnected/OnMeetingStarted/OnMeetingEnded`；④ `RoleRegistry` 增 `RoleRegistered` 事件，注册时反射取 `MyRole` 单例并同步 `Id`；⑤ `RoleCategory` 增 `Ghost`（鬼魂职业，死亡时由 `GhostRoleAllocator` 授予，归 `ConfigCategory.Ghost`）；⑥ 分配：`StandardRoleAllocator` 增 `GetMaxCount/GetChance`（读 `lid.assignment.*`，删除 `PickByChance`），新增 `GhostRoleAllocator`/`RoleHealthCheck`/`AssignmentConfigRegistrar`，`RolePinManager` 增 owner 双校验；⑦ 按钮系统增强（`RoleButtonBase` 公开 `UsesLeft/StartCooldown/Break/InputBlocked` 等，`RoleButtonManager.ReleaseButtonsOf(RuntimeRoleTemplate)`，`HudGrid`/`EffectButton`/`Meeting*Button`/`RoleButtonConfig` 新成员）；⑧ RPC：`SetRole` 加 `OnlyHost` 并支持早到挂起重放 `FlushPendingRoles()`，新增 `SetRoleData`，`ShowSystemMessage` 挂起补发；⑨ **配置模块清理**：删除 `GameConfig` 硬编码常量与 `RoleConfig` 静态类，统一用 `ConfigItem/ConfigBlock/ConfigRegistry`；⑩ **聊天输入**：`IsCharAllowed` 改为「先否决危险字符 → 交回原版 → 白名单补放行」的 Postfix（**退格 U+0008 不可插入**，修复"删字冒方块"；emoji 字体回退移除）；⑪ `IntroPatch` 新增 `IntroRoleTextDriver`（`LateUpdate` 每帧覆盖开场白职业名，修复开场动画露出原版职业） |

| 2026-10-07 | **PR #7（按钮默认条件修正）**：`RoleButtonConfig` 三个默认值由"恒真"改为**绑定本地玩家状态** —— `CanUse = () => PlayerControl.LocalPlayer.CanMove`（原 `() => true`）、`CanShow = () => !PlayerControl.LocalPlayer.Data.IsDead`（原 `() => true`）、`CanRunCooldown = t => PlayerControl.LocalPlayer.IsKillTimerEnabled`（原 `t => true`）。语义变为「默认能移动时可用、死亡时隐藏、按击杀计时器跑冷却」。`Jackal` 与 `AbilityButtonFactory.CreateKill` 相应把附加条件并入 `CanUse`（`SetCanRunCooldownPredicate` 方法**保留**，只是不再被这两处使用） |
| 2026-10-07 | **握手系统恢复启用**：新增设置 `EnableHandshake`（bool，默认 **false**）与 `HandshakeTimeoutSeconds`（float，默认 **10**，夹 1~60），模组设置新增「握手验证」三项——启用握手验证（开关）/ 验证失败处理（仅提示 Warn · 踢出 Kick）/ 握手超时(秒)；启用判定由「`VerifyServerUrl` 非空」改为「`EnableHandshake` 为真 **且** 地址非空」；状态拆两态——`_pending`（等待验证，**不红名、不算未通过**）与 `_unverified`（真正失败：红名 + 按 `HandshakeMode` 提示或延迟踢出）；超时窗口改读配置（默认 10s，夹 1~60，窗口内不判定）；取不到本次票据时（服务器不可达/拒绝）**不发握手**，交由房主按超时判定（**不使用缓存票据"宽限"**——缓存票据的 nonce 属上一次挑战，必然被房主判为 `Tampered`，会把"服务器不可达"误报成"疑似被篡改"）；清理改为不残留（超时循环常驻，对已离开玩家清理 `_pending`/`_unverified`/红名等）；`HandshakeManager` 新增公开 `IsEnabled` 与 `HasPending()`（`HasUnverified()`/`IsUnverified()`/`MarkNameRed()`/`RestoreNameColor()`/`CleanupPlayer()` 语义不变）；恢复三个补丁 `HandshakeJoinPatch`（房主发挑战）、`HandshakeLobbyGatePatch`（未通过者红名，存在等待或未通过者时阻止开始，按钮禁用文案"正在等待玩家"，解除后恢复可用）、`HandshakePlayerDestroyPatch`（离开清理），并恢复 `LightPlugin.Load` 中的 `HandshakeManager.Initialize()` 调用；`HandshakeCrypto.ComputeLocalHashes` 改为从**已加载程序集位置**推导（API 用 `typeof(LIDPlugin).Assembly.Location`，模组本体遍历 `AppDomain` 找名为 `Light` 的程序集），并在 `Paths.PluginPath` **递归查找**兜底，找不到时打明确告警（不再静默返回 0）；签名格式注释修正为 **IEEE P1363（64 字节，非 DER）**。发送者校验（RPC 层）本次未做，列为后续评估项 |

| 2026-10-07 | **握手接入线上服务器 + 版本号单一来源**：删除 `HandshakeCrypto.ProtocolVersion`（原 `"1.0.0"`）；版本改由**模组版本 `LightPlugin.Version`** 单一来源——客户端上报与房主验签都用它，`HandshakeCrypto.TryParse` 新增参数 `expectedVersion`；内置 `PublicKeyPem` 更新为线上 `GET /pubkey` 的公钥（原与线上不一致 → 房主验签必失败）。不引入 build num。`VerificationServer` 初始 `official.json` 改为**空白名单**，`/verify` 对 0 hash / 超长字段直接 403，签名格式注释更正为 IEEE P1363 |
| 2026-10-07 | **握手系统代码审查修复（11 项）**：① **RPC 发送者校验**——`CustomRPC` 新增 `CurrentSender` 与 `HandleRpcFrom(senderId, reader)`，`CustomRpcHandlePatch`/`PlayerControlRpcPatch` 从 `__instance` 推导真实发送者并透传；握手侧校验 Challenge 来自房主、Handshake 的 `playerId == 发送者`（堵住"任意客户端伪造挑战/握手栽赃无辜玩家"）；② **Warn 模式不再拦开始**——`HandshakeLobbyGatePatch` 仅 `HandshakeMode=Kick` 时用 `HasUnverified()` 阻止开始，`HasPending()` 始终阻止；③ 解除阻塞只在"本补丁置灰过"时恢复按钮（不覆盖原版可用性判断）；④ 超时文案改为"未完成模组验证"；⑤ 票据随闭包传递（`SendHandshake(ticket)`），消除多挑战并发下共享字段竞态；⑥ `CoKickAfter` 延迟踢出前复检（已离开/已通过/PlayerId 复用则不踢）；⑦ 服务端 `BuildPayload` 长度字节与实际写入一致；⑧ 空白名单 + 0 hash 拒绝（见上条）；⑨ 删除失效的票据缓存 `SaveCache/LoadCache` 与 `_ticketExp`；⑩ `TryParse` 补 `accLen` 边界校验（与 `verLen` 对称）；⑪ 版本单一来源（见上条） |

| 2026-10-07 | **接管原版分配 + 按钮/任务/HUD 一批修复（合作者 PR `0ce4e38`）**：① **接管分配**——`RoleInterceptPatch.VanillaSelectRolesBlockPatch` 对 `RoleManager.SelectRoles` 打 Prefix（仅主机 + `GameModes.Normal`）并返回 false，用 `AssignBaseRoles()` 复刻原版"收集名单 → `GetAdjustedNumImpostors` 定内鬼数 → 随机挑人"，**只发 `Impostor`/`Crewmate` 底色职业**；`DowngradeVanillaSpecialRoles()` 兜底把原版技能职业降级为底色；`RuntimeRoleTemplate.NormalizeVanillaRole(bool asImpostor=false)` 供 `Caller`/`Jackal`/`Sidekick` 在 `OnActivated` 规整底层职业。② **职业 RPC 改按 CodeName**——新增 `SetRoleByCode(byte, string, int[])`[OnlyHost]，`SetRole` 保留兼容但内部转 CodeName；挂起表 `PendingRoles` 值类型改 `(CodeName, Arguments)`；`Player.SetRole` 改发 `SetRoleByCode`；`ApplyRole` 持久化存 **CodeName** 且中立职业 `TasksCountTowardProgress=false`；新增 `LightPlayerData.DisplayRole(CodeName)` 反查显示名。③ **任务数**——`RoleTemplate.TaskCount` 由占位改为可覆写（默认 1 = 不干预），新增 `RoleTaskCountPatch`（`NetworkedPlayerInfo.RpcSetTasks` Prefix）按 `TaskCount` 增/减任务；新增 `LightUtils.GetCurrentTaskCount(player, countOnlyProgress)`。④ **按钮**——`RoleButtonBase.ShouldTickCooldown()`（通风管/变形/会议/放逐/开场/角色菜单/非白名单小游戏中不推进冷却）；`RoleButtonManager.SuppressVanillaAbilityButtons()` 有自定义职业时隐藏原版能力按钮；`AbilityButton.ApplyIcon(Sprite)` / `MeetingAbilityButton` 换图标后重算冷却遮罩 UV；`EffectButton.UpdateCooldownDisplay()` 刷效果进度环。⑤ **HUD 布局配置**——新增 `lid.hud` 块（`smallGrid` / `buttonArrangement`，默认 = 现状）。⑥ 修复追踪器轮廓不显示（`PlayerTracker.SetOutline` 同时写 `LongModeParts` 渲染器） |

> **注意：API 更新后须同步更新本文档。** 涉及新成员、签名变更、模块调整时，请在本表追加记录并更新对应章节。
