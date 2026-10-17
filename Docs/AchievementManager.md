# AchievementManager 设计文档

## 1. 对外公开 API

```csharp
public static class AchievementManager
{
    // 按 Key 解锁一个成就
    public static void Unlock(string achievementKey);

    // 按 Key 上锁一个成就
    public static void Lock(string achievementKey);

    // 解锁所有已注册成就
    public static void UnlockAll(bool includeDev = false);

    // 上锁所有已注册成就
    public static void LockAll(bool includeDev = false);

    // 在 Load() 中调用，注册一个成就
    public static void RegisterAchievement(
        string achievementKey,
        AchievementLevelType levelType,
        AchievementFilterType filterType,
        string[]? modifierKeys = null,
        bool isHidden = false,
        AchievementPortraitSource portraitSource = AchievementPortraitSource.Class,
        string? imagePath = null,
        bool showUnlockTipImmediately = false,
        string? customFilterKey = null
    );
}
```

---

## 2. 枚举定义

### 2.1 AchievementLevelType

```csharp
public enum AchievementLevelType
{
    Bronze,
    Silver,
    Gold,
    Challenge,
    Colorful,
    Dev
}
```

说明：

- `Dev` 表示开发成就。
- `UnlockAll()` / `LockAll()` 默认跳过 `Dev`。

### 2.2 AchievementFilterType

```csharp
public enum AchievementFilterType
{
    Crewmates,
    Impostors,
    Neutrals,
    Modifiers,
    Ghosts,
    Special,
    Custom,
    Other
}
```

说明：

- `Custom`：使用 `customFilterKey` 去语言文件取分类名。
- `Other`：显示为“其他”，用于默认分类。
- `Custom` 必须提供 `customFilterKey`，否则注册时抛 `ArgumentException`。
- `filterType != Custom` 时，`customFilterKey` 会被忽略。

### 2.3 AchievementPortraitSource

```csharp
public enum AchievementPortraitSource
{
    None,
    Class,
    Custom
}
```

说明：

- `None`：不显示立绘。
- `Class`：使用当前职业立绘；若职业立绘不存在，则不显示。
- `Custom`：使用 `imagePath` 指定的立绘；若 `imagePath` 为空，注册时抛 `ArgumentException`。

---

## 3. 方法详细说明

### 3.1 Unlock(string achievementKey)

按 Key 解锁一个成就。

行为：

1. 在已注册成就中查找 `achievementKey`。
2. 找不到：抛 `AchievementKeyNotFoundException`。
3. 已解锁：幂等，无操作，不弹提示。
4. 未解锁：
   - 标记为已解锁。
   - 如果 `showUnlockTipImmediately == true`，立即在右上角显示获得成就提示。
   - 如果 `showUnlockTipImmediately == false`，加入结束场景展示队列。

异常：

- `AchievementKeyNotFoundException`

---

### 3.2 Lock(string achievementKey)

按 Key 上锁一个成就。

行为：

1. 在已注册成就中查找 `achievementKey`。
2. 找不到：抛 `AchievementKeyNotFoundException`。
3. 未解锁：幂等，无操作。
4. 已解锁：
   - 标记为未解锁。
   - 从结束场景展示队列中移除。
   - 不弹任何提示。

异常：

- `AchievementKeyNotFoundException`

---

### 3.3 UnlockAll(bool includeDev = false)

解锁所有已注册成就。

行为：

1. 遍历所有已注册成就。
2. 如果 `includeDev == false`，跳过 `AchievementLevelType.Dev` 的成就。
3. 如果 `includeDev == true`，包含 `Dev` 成就。
4. 批量解锁不触发右上角即时提示。
5. 所有本次新解锁的成就统一加入结束场景展示队列。
6. 已解锁的成就保持幂等，无操作。

默认值：

- `includeDev = false`

---

### 3.4 LockAll(bool includeDev = false)

上锁所有已注册成就。

行为：

1. 遍历所有已注册成就。
2. 如果 `includeDev == false`，跳过 `AchievementLevelType.Dev` 的成就。
3. 如果 `includeDev == true`，包含 `Dev` 成就。
4. 清空结束场景展示队列。
5. 不弹任何提示。

默认值：

- `includeDev = false`。

---

### 3.5 RegisterAchievement(...)

在 `Load()` 中调用，注册一个成就到成就池。

参数：

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `achievementKey` | `string` | / | 成就唯一 Key |
| `levelType` | `AchievementLevelType` | / | 成就等级/类型 |
| `filterType` | `AchievementFilterType` | / | 成就面板分类头 |
| `modifierKeys` | `string[]?` | `null` | 可组合内容，`null` 视为空数组 |
| `isHidden` | `bool` | `false` | 是否为隐藏成就 |
| `portraitSource` | `AchievementPortraitSource` | `Class` | 立绘来源 |
| `imagePath` | `string?` | `null` | 仅当 `portraitSource == Custom` 时使用 |
| `showUnlockTipImmediately` | `bool` | `false` | `true`：解锁后立即右上角显示；`false`：只在游戏结束时显示 |
| `customFilterKey` | `string?` | `null` | 仅当 `filterType == Custom` 时生效 |

行为：

1. 如果 `achievementKey` 已注册：抛 `ArgumentException`。
2. 如果 `filterType == Custom` 且 `customFilterKey` 为空：抛 `ArgumentException`。
3. 如果 `portraitSource == Custom` 且 `imagePath` 为空：抛 `ArgumentException`。
4. `modifierKeys == null` 时，内部按空数组处理。
5. 注册成功后，该成就进入成就池，等待 `Unlock` / `Lock` / `UnlockAll` / `LockAll` 操作。

异常：

- `ArgumentException`

---

## 4. 行为约定

### 4.1 幂等性

- `Unlock` 已解锁成就：无操作。
- `Lock` 未解锁成就：无操作。
- `UnlockAll` / `LockAll` 可重复调用。

### 4.2 提示显示

- `showUnlockTipImmediately == true`：单次 `Unlock` 时立即右上角提示。
- `showUnlockTipImmediately == false`：单次 `Unlock` 时加入结束场景展示队列。
- 批量操作 `UnlockAll`：不触发即时提示，统一加入结束场景展示队列。
- `Lock` / `LockAll`：不弹提示，并清空相关待展示项。

---

## 5. 异常定义

```csharp
public class AchievementKeyNotFoundException : Exception
{
    public AchievementKeyNotFoundException(string key)
        : base($"Achievement key not found: {key}")
    {
    }
}
```

---

## 6. 最小调用示例

```csharp
// Load() 中注册
AchievementManager.RegisterAchievement(
    achievementKey: "role.crewmate.win",
    levelType: AchievementLevelType.Bronze,
    filterType: AchievementFilterType.Crewmates,
    modifierKeys: new[] { "role.crewmate.win.exileImps"},
    isHidden: false,
    portraitSource: AchievementPortraitSource.Class,
    showUnlockTipImmediately: false
);

AchievementManager.RegisterAchievement(
    achievementKey: "dev.devachievement",
    levelType: AchievementLevelType.Dev,
    filterType: AchievementFilterType.Other,
    isHidden: true,
    portraitSource: AchievementPortraitSource.None,
    showUnlockTipImmediately: true
);

// 游戏中解锁
AchievementManager.Unlock("role.crewmate.win");

// 调试时
AchievementManager.UnlockAll();          // 不包含 Dev
AchievementManager.UnlockAll(includeDev: true); // 包含 Dev
AchievementManager.LockAll();            // 不包含 Dev
```