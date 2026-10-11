# NOS(Nova)UI 移植状态 —— 2026-10-11

> 给下一次继续的人(或下一个会话的我)看的**一页纸状态** ✓。
> 目标:把 NOS(Nebula v353 / `NebulaPluginNova`)的设置界面**一模一样**移植进来,**接到我们的职业系统** ✓。

---

## 1. 现在的确定状态

| 项目 | 状态 |
|---|---|
| **主插件 `Light.dll`** | ✓ **0 错误、已部署**、可玩 —— 里面有"NOS 样式页签栏 + 图标修复 + 原版内容清空" ✓ |
| **`Light.Nova\Light.Nova.csproj`**(独立程序集,试验中) | ✗ **70 个错**,未产出 DLL —— 但**完全不影响主插件** ✓ |
| 主工程与 Nova 的隔离 | ✓ **已建立并验证** ✓(这是关键 ✓) |

启动构建:
```powershell
# 主插件（始终要绿 ✓）
& 'C:\Program Files\dotnet\dotnet.exe' build 'D:\Coding\C#\LightInDark\Light\LightPluginMain.csproj' -c Debug --nologo
# Nova 试验程序集（可以不绿 ✗）
& 'C:\Program Files\dotnet\dotnet.exe' build 'D:\Coding\C#\LightInDark\Light.Nova\Light.Nova.csproj' -c Debug --nologo
```

---

## 2. 为什么要独立程序集（今天的血泪结论 ✓✓）

Nova 的 `NebulaPluginNova\Nebula.cs` 里有 **全局 using** ✗:
```csharp
global using Color = UnityEngine.Color;
global using GUIWidget = Virial.Media.GUIWidget;
global using Language = ...（还有一堆）
```
它和我们主程序集 `LightAPI\LIDPlugin.cs` 的
```csharp
global using Color = LightInDark.Color;
```
**同名冲突** ✗ → 合在一起编译时实测炸成:
**12 → 110 → 1760 → 2044 → 12148 个错** ✓✓(我在这上面来回折腾了好几轮 ✗)

→ **独立程序集后，它的 global using 只影响它自己** ✓，主工程一个都不受影响 ✓✓。

### 以后抽离回主程序集时必须做（不做就会重演 ✗）
1. 把 `Light\UI\NovaFull\` 的 753 个文件移进主工程 ✓
2. 删掉 `Light.Nova.csproj` + 主 csproj 里的 `<Compile Remove … NovaFull …>` ✓
3. **同时**处理同名 global using —— 二选一 ✓:
   - 删掉 Nova 的 `global using Color = UnityEngine.Color;` ✓，或
   - 把主工程的 `global using Color = LightInDark.Color;` 改名(如 `LColor` ✓)并**全库替换一次** ✓
4. `Language` 等其它同名件同理 ✓

---

## 3. Nova 侧的错都在哪（下一步的工作清单 ✓）

**当前:80 个错** ✓，全部是**我对 Nova 内部 API 的垫片形状对不上** ✗ —— 不是 Nova 源码的问题 ✓。

修法：**读调用点，把垫片成员写成调用方期望的形状** ✓（每个 2 分钟，但不读就会来回改错 ✗ —— 我已经在 `RpcUseCamera` 上翻来覆去三次 ✗：它既要能当**值**用又要能当**方法**调 ✓，
说明垫片必须**同时**提供字段与方法两个名字不同的成员 ✓，或改成调用方期望的那一个 ✓）。

错误按文件的分布（最近一次实测 ✓）：
```
  8x  Patches\Old\SpecialMinigamePatch.cs     ← CameraVC 的接口形状
  6x  Modules\LobbySlide\ImageSlide.cs        ← NebulaPlugin.HttpClient / NebulaGameManager.LobbySlideManager
  6x  Roles\Neutral\Jackal.cs                 ← RoleRegistry.Register 的参数个数
  6x  Extensions\ShipExtension.cs             ← CameraVC.Activate 的参数
  4x  Modules\ClientOption.cs                 ← OpenSettingScreen / OpenServerSettingScreen
  4x  NebulaManager.cs                        ← NoSVCRoom 的静态/实例
  4x  Modules\LobbySlide\LobbySlide.cs / MovieSlide.cs
  2x  Roles\Impostor\Impostor.cs / Roles\Modifier\Lover.cs  ← Register 参数
  2x  Patches\Old\MeetingPatch.cs             ← NoSVCRoom.LocalMicLevel 静态/实例
  2x  Utilities\LogUtils.cs
```

⚠️ **两条已经验证过的铁律** ✓：
1. **不能排除任何 Nova 文件** ✗ —— 排一个 → 别的文件找不到它导出的类型 → 12↔274↔838↔970 死循环 ✓
2. **`Nebula.cs` 绝对不能排除** ✗ —— 它是 global using 的载体 ✓，删它 = **12148 个错** ✓

### 剩下的两大块（都不影响"设置界面一模一样" ✓，可最后做 ✓）
| 块 | 说明 |
|---|---|
| **VoiceChat / Interstellar** | `NoSVCRoom.cs` 789 行对着外部模组真实 API 写的 ✗ → 要么补全垫片 ✓，要么拿那个 DLL ✓ |
| **杂项** | `HttpClient` / `LobbySlideManager` / `Register` 参数等 Nova 内部件 ✓ |

---

## 4. 已经搬进来的东西（不用重做 ✓）

```
Light\UI\NovaFull\NebulaAPI\          234 个 .cs /  16,743 行   ← 单独编译是 0 错误 ✓✓（命名空间 Virial）
Light\UI\NovaFull\NebulaPluginNova\   519 个 .cs / 116,533 行
Light\Resources\NovaFull\             481 张 png / 9.4 MB（不嵌入 ✓，要用哪张单独拷 ✓）
Light\Resources\GUI\Nova_*.png          8 张（页签在用 ✓）
Light\Patches\NovaTabBarPatch.cs      ★ 我们手写的页签栏（现在能跑 ✓，最终会被 Nova 自己的替换 ✗）
Light\UI\Nova\                         19 个 .cs（早期挑的 UI 子集，备用 ✓）
```

**Nova 的页签真实现(最终要用的 ✓)**:
`NebulaPluginNova\Patches\Old\GameOptionPatch.cs`
```csharp
[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]   // ★ 这版有 Start ✓
static DividedSpriteLoader tabSprites = DividedSpriteLoader.FromResource("Nebula.Resources.TabIcon.png", 100f, 6, 1);
static Image backSprite = SpriteLoader.FromResource("Nebula.Resources.TabBackground.png", 100f);
CreateTab(__instance, phoneLeft, "VanillaTab"/"NebulaTab"/"PresetTab", screens, 0/1/2, allTabs);
```
**页面内容(最终要用的 ✓)**:
`NebulaPluginNova\Configuration\NebulaSettingMenu.cs`(479 行 ✓) + `PresetSettingMenu.cs` ✓
`NebulaPluginNova\Modules\MetaScreen.cs`(1533 行 ✓) + `Modules\MetaWidget\*`(~2000 行 ✓)

---

## 5. 我们自己的东西(不能弄坏 ✗)

| 系统 | 位置 |
|---|---|
| 职业注册表 | `LightInDark.Roles.RoleRegistry.AllRoles` ✓(每个 `RoleTemplate` 有 `CodeName` / `Name` / `RoleCategory` ✓) |
| 配置注册表 | `LightInDark.Configuration.ConfigRegistry` ✓ |
| 预设库 | `LightInDark.Configuration.PresetLibrary.List()` ✓(`PresetMeta.Name/Author/Created` ✓) |
| 语言 | `LightInDark.Language.Language.GetStringOrKey(key, fallback)` ✓ |
| 文本模板 | `Light.UI.Window.MenuTextTemplate2.Create(parent, pos, text, fontSize, color)` ✓ |
| 旧的规则界面实现 | **已停用+备份** ✓:`Light\Patches\Unused\GameSettingMenuPatch.cs.bak` ✓ |

---

## 6. 血泪教训（今天踩的 ✓，写下来免得重犯 ✗）

1. **不能靠"排除文件"绕过缺类型** ✗ —— Nova 是互相咬合的整体 ✓，
   排一个文件 → 别的文件找不到它导出的类型 → 又得放回来 ✓ → **死循环** ✓(今天在 12↔274↔838 之间来回 ✓)
2. **`Nebula.cs` 绝对不能排除** ✗ —— 它是 **global using 的载体** ✓，删它 = 抽掉全项目的隐式引用 ✓ → **12148 个错** ✓
3. **批量改文件前先确认能回滚** ✓ —— 未跟踪的新文件**没有 git 保护** ✗，
   今天两次把 `HelpScreen.cs`/`PresetWindow.cs` 改坏 ✗(靠 `git checkout` 和重拷救回 ✓)
4. **一次改一处 + 每处构建** ✓(AGENTS §11.6 早写着 ✓，我今天又犯了 ✗)
5. **别用脚本"注释多行语句"** ✗ —— 只注释首行会让后续行悬空 ✓ → 语法错一片 ✓
6. **namespace 的"文件级 `X;`"与"块状 `X {}`"互换要成对处理** ✓，
   只改一行会吃掉花括号 ✓ → 用"逐行数花括号深度"定位最快 ✓
