# RoleTool 节点与生成契约

## 1. 目的与工作方式

RoleTool 是《Light In The Dark》（暗中辉）职业开发辅助工具：可视化搭节点蓝图 +
填职业表单，一键生成可直接编译的 C# 职业源码、翻译条目与说明文档，并可写入主插件源码树。

### 数据流

```
node-defs.js   ── 注册 70 种节点（类型串 / 中文标题 / 端口 / 属性）
emit-map.js    ── 节点类型 → C# 片段 的声明式映射表（EMIT）
codegen.js     ── 消费 EMIT：遍历蓝图 → 生成 C# / 翻译 / 文档
form.js        ── 职业元数据表单（阵营 / 颜色 / 配置项 / 文档类型 …）
docs.js        ── 应用内「文档」页签：每个节点的作用/端口含义/用法 + 使用教程
```

> `docs.js` 的端口列表是**实例化真实节点后读取**的（`docPortsOf`），因此
> 端口信息不会与 `node-defs.js` 脱节；该文件只补充文字说明（`NODE_DOC`）。
> 新增节点时若忘了写 `NODE_DOC`，文档页会显示「（暂无说明）」而不是静默缺失。

- **节点图 → `RuntimeInstance` 方法体**：蓝图决定职业的运行时行为，生成的
  `RuntimeInstance : RuntimeRoleTemplate` 的成员（生命周期方法、事件监听、
  按钮处理、数据字段、RPC 方法）由节点图推导。
- **元数据表单 → `RoleTemplate` 类头**：表单决定职业的外层声明
  （`Code` / `Color` / `RoleCategory` / `Ability`/`CanKill` / `Allocation` /
  `DocumentType` / `Describe` 等），生成一个 `public class <ClassName> : RoleTemplate`。

「统一元素先行」：任何跨节点统一的形态（端口约定、代码片段、字段声明、属性名）
必须先落到 `emit-map.js` 这张表里，再由 `codegen.js` 统一消费，不在生成器内散写。

## 2. 三种生成产出（emit kind）

`EMIT[type].kind` 决定该节点在代码里扮演什么角色：

| kind | 含义 | 产出物 | 典型节点 |
| --- | --- | --- | --- |
| `life` | 顶层生命周期方法 | `RuntimeInstance` 里一个 `override` 方法，方法体由执行链展开 | `life/onActivated`、`life/onDied` |
| `exec` | 执行链语句 | 出现在某个执行流里的一行/多行语句，自身可继续向下连「下一」 | `act/systemMessage`、`data/set` |
| `data` | 表达式生产者 | 一段 C# 表达式，被上层 `exec`/`life` 通过连线取值 | `val/number`、`logic/compare`、`data/get` |

补充：`ev`（私有事件监听方法，编译进 `private void On_*`）与 `rpcdef`
（生成 `[LidRPC]` 静态方法）是 `life`/`exec` 的特化形态；`rpc/call` 仍是 `exec`。

## 3. 节点类型全表（共 70 种）

按类型串前缀分组。端口方向：**入** = 输入端口，**出** = 输出端口。

### life（生命周期，9）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `life/onActivated` | 激活时 | — | 执行(exec) |
| `life/onInactivated` | 失活时 | — | 执行(exec) |
| `life/checkWin` | 胜负判定 | 条件(bool) | — |
| `life/onDied` | 死亡时 | — | 执行(exec)、事件(event) |
| `life/onRevived` | 复活时 | — | 执行(exec)、事件(event) |
| `life/onExiled` | 被放逐时 | — | 执行(exec)、事件(event) |
| `life/onMeetingStarted` | 会议开始 | — | 执行(exec)、事件(event) |
| `life/onMeetingEnded` | 会议结束 | — | 执行(exec)、事件(event) |
| `life/onRoleData` | 数据位变化 | — | 执行(exec)、dataId(number)、value(number) |

### btn（技能/击杀按钮，2）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `btn/ability` | 技能按钮 | — | 点击(exec)、下一(exec) |
| `btn/kill` | 击杀按钮 | 执行(exec) | 点击(exec)、下一(exec)、目标(player) |

### act（动作，16）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `act/emergencyMeeting` | 召开紧急会议 | 执行(exec) | 下一(exec) |
| `act/setRoleByCode` | 设为职业(按CodeName) | 执行(exec)、玩家(player) | 下一(exec) |
| `act/systemMessage` | 系统消息 | 执行(exec)、玩家(player)、文本(string) | 下一(exec) |
| `act/kill` | 击杀目标 | 执行(exec)、凶手(player)、受害者(player) | 下一(exec) |
| `act/suicide` | 自杀 | 执行(exec)、玩家(player) | 下一(exec) |
| `act/revive` | 复活玩家 | 执行(exec)、玩家(player) | 下一(exec) |
| `act/invisible` | 隐身/显形 | 执行(exec)、玩家(player)、开关(bool) | 下一(exec) |
| `act/teleport` | 传送玩家 | 执行(exec)、玩家(player)、坐标(vector2) | 下一(exec) |
| `act/forceEndMeeting` | 强制结束会议 | 执行(exec) | 下一(exec) |
| `act/breakEmergencyButton` | 破坏紧急按钮 | 执行(exec) | 下一(exec) |
| `act/showChat` | 显示聊天 | 执行(exec) | 下一(exec) |
| `act/hideChat` | 隐藏聊天 | 执行(exec) | 下一(exec) |
| `act/sendChat` | 发送聊天 | 执行(exec)、玩家(player)、文本(string) | 下一(exec) |
| `act/kick` | 踢出玩家 | 执行(exec)、玩家(player) | 下一(exec) |
| `act/flash` | 屏幕闪烁 | 执行(exec)、颜色(color) | 下一(exec) |
| `act/log` | 日志输出 | 执行(exec)、文本(string) | 下一(exec) |

### data（数据位，4）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `data/registerId` | 注册数据位 | — | id(number) |
| `data/get` | 读取数据 | 玩家(player)、id(number) | 值(number) |
| `data/set` | 写入数据 | 执行(exec)、玩家(player)、id(number)、值(number) | 下一(exec) |
| `data/add` | 累加数据 | 玩家(player)、id(number)、增量(number) | 值(number) |

### ev（事件监听，8）

8 种事件节点端口形态完全一致：**出**：执行(exec)、事件(event)。属性：`OnlyHost`、
`OnlyMyPlayer`、`Local`、`Priority`。

| 类型串 | 中文标题 | 事件类 |
| --- | --- | --- |
| `ev/playerMurder` | 玩家击杀 | `PlayerMurderEvent` |
| `ev/tryMurder` | 尝试击杀 | `PlayerTryMurderEvent` |
| `ev/playerDeath` | 玩家死亡 | `PlayerDeathEvent` |
| `ev/vote` | 玩家投票 | `PlayerVoteEvent` |
| `ev/ventUsed` | 使用管道 | `VentUsedEvent` |
| `ev/meetingStart` | 会议开始 | `MeetingStartEvent` |
| `ev/meetingEnd` | 会议结束 | `MeetingEndEvent` |
| `ev/playerRevive` | 玩家复活 | `PlayerReviveEvent` |

### target（目标追踪，2）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `target/closest` | 最近目标 | — | 玩家(player) |
| `target/createTracker` | 创建追踪器 | 执行(exec) | 下一(exec)、追踪器(tracker) |

### logic（逻辑，7）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `logic/if` | 条件分支 | 执行(exec)、条件(bool) | 真(exec)、假(exec) |
| `logic/compare` | 数值比较 | A(number)、B(number) | 结果(bool) |
| `logic/and` | 与 | A(bool)、B(bool) | 结果(bool) |
| `logic/or` | 或 | A(bool)、B(bool) | 结果(bool) |
| `logic/not` | 非 | A(bool) | 结果(bool) |
| `logic/cancelEvent` | 取消事件 | 执行(exec)、事件(event) | 下一(exec) |
| `logic/math` | 数学运算 | A(number)、B(number) | 结果(number) |

### val（数值/文本，19）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `val/text` | 文本 | — | 值(string) |
| `val/number` | 数值 | — | 值(number) |
| `val/player` | 玩家 | — | 值(player) |
| `val/bool` | 布尔 | — | 值(bool) |
| `val/color` | 颜色 | — | 值(color) |
| `val/playerName` | 玩家名字 | 玩家(player) | 值(string) |
| `val/isDead` | 玩家是否死亡 | 玩家(player) | 值(bool) |
| `val/isImpostor` | 玩家是否内鬼 | 玩家(player) | 值(bool) |
| `val/randomAlive` | 随机存活玩家 | — | 值(player) |
| `val/aliveCount` | 存活人数 | — | 值(number) |
| `val/distance` | 两点距离 | A(player)、B(player) | 值(number) |
| `val/inRange` | 是否在范围内 | A(player)、B(player)、距离(number) | 值(bool) |
| `val/vector2` | 坐标 | — | 值(vector2) |
| `val/gameState` | 游戏状态 | — | 值(bool) |
| `val/isMap` | 是否某张地图 | — | 值(bool) |
| `val/hostPlayer` | 房主玩家 | — | 值(player) |
| `val/isHost` | 玩家是否房主 | 玩家(player) | 值(bool) |
| `val/playerById` | 按ID取玩家 | id(number) | 值(player) |
| `val/allPlayerCount` | 玩家总数 | — | 值(number) |

> `vector2` 是新增端口类型（对应 `UnityEngine.Vector2`），只用于
> `act/teleport` 的「坐标」输入；类型检查会自动拒绝其它类型连入。

### rpc（自定义 RPC，2）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `rpc/customDefinition` | 自定义RPC | — | 方法体(exec) |
| `rpc/call` | 调用RPC | 执行(exec) | 下一(exec) |

### cfg（配置，1）

| 类型串 | 中文标题 | 入 | 出 |
| --- | --- | --- | --- |
| `cfg/get` | 读取配置 | — | 值(number) |

合计：9+2+16+4+8+2+7+19+2+1 = **70 种**（与 `node-defs.js` 注册数一致，
应用内「文档」页签的节点手册条数也应等于该值）。

### 按钮类节点的端口语义（重要）

| 端口 | 方向 | 含义 | 生成代码对应 |
| --- | --- | --- | --- |
| `btn/kill`.`点击` | 出(exec) | **击杀成功之后**的回调体 | `AbilityButtonFactory.CreateKill(this, target => { … })` 的 lambda 体 |
| `btn/kill`.`下一` | 出(exec) | 与「点击」**平行**的另一条执行链，接着往下串 | lambda **之后**的下一条语句 |
| `btn/kill`.`目标` | 出(player) | 本次被击杀的玩家 | lambda 形参 `target`，**只能在「点击」分支内使用** |
| `btn/ability`.`点击` / `下一` | 出(exec) | 同上：点击回调体 / 平行的后续链 | `AbilityButtonFactory.Create(this, cfg, () => { … })` 内外 |

> 击杀动作本身由 `AbilityButtonFactory.CreateKill` 内部完成（自动锁定最近合法目标）；
> 用户只需决定「击杀后做什么」。
>
> ⚠️ `目标` 的价值绑定在击杀回调形参上，若把它接到「下一」链路的节点，
> 生成代码会引用到作用域外的 `target` 而**编译失败**。`validation.js` 第 11 项检查
> 专门拦截这种用法（判定：`目标` 的消费者必须能沿着下游连线汇入「点击」分支）。

### 游戏状态 / 玩家查询 API 的同步（2026-10-17）

主插件新增了 `LightInDark.Game.GameState`（全局状态查询）与 `Player` 的一组静态查询口子，
RoleTool 已把它们落成节点：

| 节点 | 生成代码 | 对应主插件 API |
| --- | --- | --- |
| `val/gameState` | `LightInDark.Game.GameState.<State>` | `GameState` 的 25 个布尔状态（`IsLobby` / `IsInGame` / `IsInMeeting` / `IsVoting` / `IsCanMove` / `IsInVent` / `AmHost` / `IsModHost` …） |
| `val/isMap` | `LightInDark.Game.GameState.IsMap(MapNames.<M>)` | `GameState.IsMap(MapNames)`；成员为 `Skeld` / `MiraHQ` / `Polus` / `Dleks` / `Airship` / `Fungle`（⚠️ 是 `Skeld`，不是 `TheSkeld`） |
| `val/hostPlayer` | `LightInDark.Game.Player.HostPlayer` | `Player.HostPlayer` |
| `val/isHost` | `(<玩家>).IsHost` | `Player.IsHost`（实例成员） |
| `val/playerById` | `LightInDark.Game.Player.Get((byte)(<id>))` | `Player.Get(byte)` |
| `val/allPlayerCount` | `LightInDark.Game.Player.AllPlayers.Count()` | `Player.AllPlayers` |

- `GameState.LocalPlayer` / `Ship` 不是布尔值，未纳入 `val/gameState` 的下拉。
- `LightPlayer` 只是 `Player` 的别名转发，**不另设节点**（同一个 API 不重复开口）。
- `Player.Find` / `FindAll` / `TryGetPlayer` 需要传入 `Func<Player,bool>` 谓词；
  当前由「最近目标 / 随机存活玩家 / 事件玩家」覆盖同类需求，未做通用谓词节点。
  若日后要支持任意条件查找，需要新增"谓词组合"类节点。

### 「玩家」节点各选项的上下文约束（重要）

`val/player` 的 `Killer` / `Victim` / `Reporter` 取自事件形参 `e` 的成员，
而**哪些成员存在取决于事件类型**；并且这三个成员是原版 `PlayerControl`，
必须经 `Player.Get(...)` 转成本层的 `Player`，才能被下游（取 `.Control.PlayerId`）使用。

| 事件类 | 可用玩家成员 |
| --- | --- |
| `PlayerDeathEvent` | `Player`(死者)、`Killer` |
| `PlayerReviveEvent` | `Player`、`Healer` |
| `PlayerExileEvent` | `Exiled` |
| `MeetingStartEvent` | `Reporter` |
| `MeetingEndEvent` | 无 |
| `PlayerMurderEvent` / `PlayerTryMurderEvent` | `Player`、`Killer`、`Victim` |
| `PlayerVoteEvent` | `Player`、`Voter`、`VoteFor` |
| `VentUsedEvent` | `Player` |

生成映射与合法上下文：

| 选项 | 生成 | 合法上下文 |
| --- | --- | --- |
| `Killer` | `LightInDark.Game.Player.Get(e.Killer)` | 死亡 / 玩家击杀 / 尝试击杀 |
| `Victim` | `LightInDark.Game.Player.Get(e.Victim)` | 玩家击杀 / 尝试击杀 |
| `Reporter` | `LightInDark.Game.Player.Get(e.Reporter)` | 会议开始 |
| `Target` | `target` | 击杀按钮的「点击」分支 |
| `MyPlayer` | `MyPlayer` | 任意位置 |

`validation.js` 第 12 项按上表检查：上下文不匹配时报错（并列出该事件可用的成员），
避免"生成能过、编译才报错"。

## 4. 产出约定

### 阵营 → 目录 / 命名空间（`CATEGORY_MAP`）

| category | 目录 | 命名空间 |
| --- | --- | --- |
| `Crewmate` | `Crewmates` | `Light.Roles.Crewmates` |
| `Impostor` | `Impostors` | `Light.Roles.Impostors` |
| `Neutral` | `Neutrals` | `Light.Roles.Neutrals` |
| `Ghost` | `Ghosts` | `Light.Roles.Ghosts` |

### 文件路径

- C# 源码：`Light\Roles\<Plural>\<ClassName>.cs`
  （例：`Crewmate` + 类名 `Guard` → `Light\Roles\Crewmates\Guard.cs`）
- 翻译键：合并进 `Light\Resources\Language\SChinese.json`（zh）与
  `Light\Resources\Language\English.json`（en）。键格式：
  `role.<codename小写>.name` / `.shortdes` / `.des` / `.intro`，以及
  `Button.<ClassName>.label`。
- 说明文档：`Light\Resources\Documents\<ClassName>.<ext>`，`ext` 由表单
  `documentType` 决定（`MarkDown`→`md`，`Html`→`html`，`Normal` 不生成）。
  生成源码里的 `Describe` 路径即 `./Resources/Documents/<ClassName>.<ext>`。
  资源目录**必须是 `Light\Resources\Documents\`（不是 `Docs`）**，
  且该目录在 `Light\LightPluginMain.csproj` 中已作为 `EmbeddedResource`
  （`Resources\**\*.*`）嵌入式打包。

### 写入落盘

桥接命令 `writeFiles` 负责把生成结果写入仓库根下（`repoRoot`）对应路径；
翻译以「合并」方式更新既有 JSON。一键编译由桥接命令 `buildPlugin`
（`dotnet build … -c Release`）执行，输出回显到前端构建模态框。

### 更新日志约定（每次更新都要写）

- 更新日志的数据源是 `Resources/js/changelog.js` 的 `CHANGELOG` 数组，
  由「关于」页签渲染（`renderChangelog()`，`main.js` 启动时调用）。
- **每次对 RoleTool 做更新，都要在数组顶部追加一条**：
  `{ date: 'YYYY-MM-DD', title: '一句话概括', items: ['具体改动…'] }`（新的放最上面）。
- 同时把 `Resources/js/constants.js` 的 `APP_VERSION` 改成当天日期（`YYYYMMDD`）——
  「关于」页签显示的版本号取自它，蓝图文件的兼容性判断也用它。
- 面向使用者写"能看到什么变化"（新增 / 修复 / 调整），不写实现细节。

## 5. 新增一个节点（统一元素先行）

1. **`node-defs.js`**：在 `registerAllNodes()` 里用 `defNode(type串, 中文标题, buildFn, C.<类>.fg, C.<类>.bg)`
   注册节点，`buildFn` 内 `addInput/addOutput/addProperty/addWidget` 定义端口与属性；
   分类前缀从 `C` 取色（见 `constants.js`）。
2. **`emit-map.js`**：在 `EMIT` 里为同一类型串补条目，声明
   `category` / `kind`（life|exec|data，或 ev|rpcdef）/ `imports` /
   生成函数（`method` / `lines` / `expr` / `dataOut`）。
3. **`docs.js`**：在 `NODE_DOC` 里补该类型串的 `desc` / `usage`（端口含义可选，
   端口清单会自动读取）。漏写会显示「（暂无说明）」。
4. **`codegen.js`** 无需改动：它按 `EMIT` 表统一消费。仅当引入**新的 kind**
   或新的顶层结构（如新的声明收集）时才改 `codegen.js`。
5. 若引入新的分类前缀，还需在 `constants.js` 的 `C`（配色）与 `CN_CATEGORY`
   （中文标签）各补一项，并在 `docs.js` 的 `DOC_CATEGORIES` 里登记。

## 6. 已知约束

- `OnRoleData` 的基类签名是 `protected internal`；跨程序集重写只能声明为
  `protected`（写成 `protected internal` 会 CS0507）。生成器已按 `protected` 输出。
- 事件特性必须用 `*Attribute` 全名：`[OnlyHostAttribute]` / `[OnlyMyPlayerAttribute]`
  / `[LocalAttribute]`（短名有同名别名类，会 CS1614 歧义）。
- `ConfigType` 没有 `String`：表单里 String 类型配置项映射为
  `Type = ConfigType.Value`，`Default = new[] { "…" }`。
- `RoleData.Add` 返回 `void`，`data/add` 要当表达式用时用内联 lambda
  先累加再 `Get` 回读新值。
- `rpc/customDefinition` 生成 `[LidRPC]`（不可靠时 `[LidRPC(Reliable = false)]`），
  与 `rpc/call` 通过 `<ClassName>Rpcs.<MethodName>(…)` 关联。
- **float 实参不能直接拼 `f`**：`ctx.resolveNumber()` 可能返回的是**表达式**
  （如 `RoleUtils.GetDistance(...)`），拼成 `…f` 会 `CS1003`。统一走 `csFloat()`：
  字面量加 `f`、其它表达式强转 `(float)(…)`。
- `LightUtils.PlayFlash` 的参数类型是 **`LightInDark.Color`**（`LightUtils` 位于
  `LightInDark.Utilities`，`Color` 按命名空间就近解析，而非 `UnityEngine.Color`），
  因此 `act/flash` 直接传 `LightInDark.Color` 表达式，**不要**做 `ToUnityColor()` 转换。
- 事件里的 `e.Killer` / `e.Victim` / `e.Reporter` 是原版 **`PlayerControl`**，
  不是本层的 `Player`；生成 `player` 值时必须包一层
  `LightInDark.Game.Player.Get(...)`，否则下游取 `.Control.PlayerId` 会 CS1061
  （`val/player` 已按此输出）。
- 原版 `MapNames` 枚举成员是 **`Skeld`**（不是 `TheSkeld`）；`MapNames` 在全局命名空间，
  生成代码无需额外 `using`。
- 生成源码在 `Light\LightPluginMain.csproj` 内编译（`net6.0`），
  `Light\` 源码本身不参与本工具的构建修改。
