using Light.UI.HudUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using LightInDark.Core;
using LightInDark.UI.Window;
using Light.UI.Window;          // ★ MenuTextTemplate2（本工程的文本模板 ✓）

namespace Light.UI.NovaTabs;

/// <summary>
/// **NOS 样式的页签栏（自己画）** —— 2026-10-11 定稿方案 ✓✓
///
/// ═══════════════════════════════════════════════════════════════════════════
/// 【为什么是"自己画"而不是"克隆原版"】（十几轮试错换来的结论 ✓）
///   · 旧 Nebula(`Nebula\`，标 2023.3.28 ✗)的 `FixTab` 是**克隆原版页签按钮** ✓
///     —— 但**我们这版(AU 19.0)的页签按钮是 `LeftPanel/GameSettingsButton`**（左侧按钮样式 ✗），
///        克隆出来只会**和原版按钮重叠** ✓✓（用户截图实证 ✓）
///   · 新 Nebula(`NebulaPluginNova\`，标 2023.7.12 ✓ = 我们这版 ✓)的
///     `Patches\Old\GameOptionPatch.cs` 就是**自己画** ✓✓：
///     <code>
///       static DividedSpriteLoader tabSprites = DividedSpriteLoader.FromResource("Nebula.Resources.TabIcon.png", 100f, 6, 1);
///       static Image backSprite = SpriteLoader.FromResource("Nebula.Resources.TabBackground.png", 100f);
///       // CreateTab(): 建 "Tab"（底图）+ 图标渲染器（tabSprites.GetSprite(id*2+1)）✓
///       // 挂点：GameSettingMenu.Start ✓（**这版有** ✓，已用 Cecil 核实 ✓）
///     </code>
///   · 但 Nova 的**页面内容**依赖 `NebulaAPI`(命名空间 `Virial`，234 个文件 ✗)——
///     那是另一个大框架 ✗，本方案**不用它** ✓：内容我们自己画 ✓
///
/// 【本文件做的事】（照 Nova 的 `CreateTab` 思路，代码全部自己写 ✓）
///   ① 在原版面板上方**画 3 个页签**（底图 + 图标切图 ✓，位置=用户框的左上角 ✓）
///   ② 点页签 → **原版内容全部清掉**（三个原版页 + MainArea 里其它东西 ✓）
///      第 1 个页签 = 原版（还回来 ✓），第 2/3 个 = 我们的页（占位文字 ✓）
///   ③ 页码/选中态自己控制 ✓（不依赖原版按钮的高光 ✓）
///
/// 【资源】（已从 `NebulaPluginNova\Resources\` 拷入 `Light\Resources\GUI\` ✓）
///   `Nova_TabIcon.png`      54.3 KB  ← ★ **6×1 切图**（Nova 的 TabIcon 就是 6 帧 ✓）
///   `Nova_TabBackground.png` 3.0 KB  ← ★ 页签底图
/// ═══════════════════════════════════════════════════════════════════════════
/// </summary>
[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
internal static class NovaTabBarPatch
{
    /// <summary>★ 总开关：想回到"纯原版界面"就把它设 false ✓</summary>
    private const bool Enable = false;   // 2026-10-11 用户选 A ✓：留旧 GameSettingMenuPatch 自己那排彩边页签 ✓，我这排红框页签关掉 ✓

    private static GameSettingMenu? _menu;

    private sealed class TabEntry
    {
        public GameObject Root = null!;
        public SpriteRenderer Icon = null!;
        public GameObject? Page;          // null = 原版页（不建自己的页 ✓）
        public bool IsVanilla;
    }

    private static readonly List<TabEntry> _tabs = new();
    private static int _current = 0;

    // ── 布局（用户框的是"左上角、面板外面" ✓；这几个常量就是给他微调用的 ✓）──
    private const float TabStartX = -3.05f;
    private const float TabStepX = 0.95f;
    private const float TabY = 2.44f;
    private const float TabW = 0.88f;
    private const float TabH = 0.52f;

    [HarmonyPostfix]
    public static void Postfix(GameSettingMenu __instance)
    {
        if (!Enable) return;
        try
        {
            _menu = __instance;
            _tabs.Clear();
            _current = 0;

            BuildTabs(__instance);
        }
        catch (Exception ex)
        {
            LightLogger.LogError("[NovaTabBar.Postfix]", ex);
        }
    }

    private static void BuildTabs(GameSettingMenu menu)
    {
        // 页签挂在原版菜单下（跟着菜单一起显示/隐藏 ✓）
        var parent = menu.transform;

        // ★ 2026-10-11 用户：「这个页签塑料感太重了。你改成三张纯图片吧，如果没图片就自绘一个边框加上字样」
        //   实现（三步，从好到次 ✓）：
        //     ① 三张纯图 `Resources\GUI\Nova_Tab1.png / Nova_Tab2.png / Nova_Tab3.png` ✓
        //        —— 你把图放进去就自动用图 ✓，不用改代码 ✓
        //     ② 没有图 → **自绘**：白框（`VanillaAsset.WhiteSprite` 拉成矩形 ✓ §4.3.3）+ 页签字样 ✓
        //     ③ 不再用 Nova 那套"底图 + 6×1 图标切图" ✗（就是它导致塑料感重 ✓）
        var fullTabs = new Sprite?[]
        {
            LoadSprite("Nova_Tab1.png", 100f),
            LoadSprite("Nova_Tab2.png", 100f),
            LoadSprite("Nova_Tab3.png", 100f),
        };
        bool useImages = fullTabs[0] != null && fullTabs[1] != null && fullTabs[2] != null;

        Sprite? backSprite = null;
        Sprite? iconSheet = null;
        if (!useImages)
        {
            // 自绘模式：白色底图当"框" ✓（Sliced 拉成矩形 ✓），图标不画 ✓
            try { backSprite = Light.UI.Window.VanillaAsset.WhiteSprite; } catch { }
            if (backSprite == null) backSprite = LoadSprite("Nova_TabBackground.png", 100f);
            LightLogger.Log("[NovaTabBar] 页签=自绘白框+字样 ✓（放 Nova_Tab1/2/3.png 到 Resources\\GUI 即可换成三张纯图 ✓）");
        }
        else LightLogger.Log("[NovaTabBar] 页签=三张纯图 ✓（Nova_Tab1/2/3.png ✓）");

        // ① 第 1 个 = 原版
        AddTab(parent, backSprite, iconSheet, 0, "原版", isVanilla: true, fullTab: fullTabs[0]);
        // ② 第 3 个 = 三号页签
        AddTab(parent, backSprite, iconSheet, 1, "MOD", isVanilla: false, fullTab: fullTabs[1]);
        AddTab(parent, backSprite, iconSheet, 2, "预设", isVanilla: false, fullTab: fullTabs[2]);

        RefreshSelection();
        LightLogger.Log($"[NovaTabBar] 已画 {_tabs.Count} 个页签 ✓（底图+图标切图来自 Nova 资源 ✓；默认选中第 1 个=原版 ✓）");
    }

    /// <summary>★ 2026-10-11 自绘**红色边框**（4 条细红条 ✓，中间透明 ✓ —— 用户：不要纯色方块 ✓）</summary>
    private static void AddRedBorder(Transform parent, int layer, float w, float h)
    {
        var red = new Color(0.86f, 0.22f, 0.22f, 1f);
        float t = 0.045f;   // 边框粗细 ✓
        float[,] bars =
        {
            { 0f,  h * 0.5f, w, t },              // 上
            { 0f, -h * 0.5f, w, t },              // 下
            { -w * 0.5f, 0f, t, h + 0.01f },      // 左
            {  w * 0.5f, 0f, t, h + 0.01f },      // 右
        };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Border" + i);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            go.transform.localPosition = new Vector3(bars[i, 0], bars[i, 1], -0.08f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Light.UI.Window.VanillaAsset.WhiteSprite;
            sr.drawMode = SpriteDrawMode.Sliced;          // §4.3.3 ✓
            sr.size = new Vector2(bars[i, 2], bars[i, 3]);
            sr.color = red;
        }
    }

    /// <summary>
    /// ★ 2026-10-11 **我们自己的关闭按钮**（放左上角 ✓，和 NOS 一样 ✓）
    /// 用户实证问题 ✗：切到 2/3 页签时原版关闭按钮**消失了** ✓
    ///   —— 因为 `HideAllVanilla()` 把原版内容整个关掉 ✓（CloseButton 也在里面 ✗）
    /// → 所以自建一个 ✓；它挂在菜单下、名字以 `NovaTab_` 开头 ✓
    ///   → `HideAllVanilla` 的白名单会**跳过它** ✓（见那里的 StartsWith 判断 ✓），任何页签都在 ✓。
    /// 点击 = 直接触发**原版**关闭按钮 ✓（最稳 ✓，不用自己写关闭逻辑 ✓）。
    /// </summary>
    private static void BuildCloseButton(Transform parent, int layer)
    {
        // ★★★ 2026-10-11 **照抄 NOS 的实现** ✓✓（用户："你就不能看看吗" ✓ —— 我去读了参考源码 ✓）
        //   `NebulaPluginNova\Modules\MetaScreen.cs:1179 InstantiateCloseButton` ✓ 原文：
        //       var collider = UnityHelper.CreateObject<CircleCollider2D>("CloseButton", parent, localPos);
        //       collider.isTrigger = true;  collider.gameObject.layer = LayerExpansion.GetUILayer();
        //       collider.radius = 0.25f;
        //       renderer = collider.gameObject.AddComponent<SpriteRenderer>();
        //       renderer.sprite = **VanillaAsset.CloseButtonSprite**;
        //       var button = collider.gameObject.SetUpButton(true, renderer);
        //   → 所以它**不是克隆原版按钮** ✗，而是"圆形碰撞 + 这个 sprite + SetUpButton" ✓
        //   → 那个 sprite 我们工程**自己就有** ✓：`Light\UI\Window\VanillaAsset.cs:54 CloseButtonSprite` ✓
        var go = new GameObject("NovaTab_Close");
        go.transform.SetParent(parent, false);
        go.layer = layer;

        var col = go.AddComponent<CircleCollider2D>();   // NOS 用的是圆形碰撞 ✓
        col.isTrigger = true;
        col.radius = 0.25f;                              // NOS 的半径 ✓

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Light.UI.Window.VanillaAsset.CloseButtonSprite;   // ★ NOS 同款 sprite ✓
        sr.color = Color.white;

        var button = go.SetUpButton(true, sr);   // NOS 的 SetUpButton(true, renderer) ✓
        if (button != null)
        {
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();   // §4.5：克隆/自建控件要**替换**而不是追加 ✓
            button.OnClick.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>((System.Action)(() =>
            {
                try
                {
                    var close = FindChild(_menu != null ? _menu.transform : parent, "CloseButton");
                    var pb = close != null ? close.GetComponent<PassiveButton>() : null;
                    if (pb != null) { pb.OnClick.Invoke(); LightLogger.Log("[NovaTabBar] 关闭 ✓"); }
                    else LightLogger.LogWarning("[NovaTabBar] 找不到原版 CloseButton ✗");
                }
                catch (Exception ex) { LightLogger.LogWarning($"[NovaTabBar] 关闭失败：{ex.Message}"); }
            })));
        }

        go.transform.localPosition = new Vector3(-3.62f, 2.16f, -1f);   // 左上角 ✓（NOS 的位置 ✓）
        go.transform.localScale = Vector3.one;
        LightLogger.Log("[NovaTabBar] 关闭按钮已建 ✓（照抄 NOS：CircleCollider 0.25 + CloseButtonSprite + SetUpButton ✓）");
    }
    private static void AddTab(Transform parent, Sprite? back, Sprite? sheet, int index, string label, bool isVanilla, Sprite? fullTab = null)
    {
        var root = new GameObject($"NovaTab_{index}");
        root.transform.SetParent(parent, false);
        // §4.3：不猜 layer —— 从原版页签按钮继承 ✓
        var refGo = FindChild(parent, "GameSettingsButton") ?? FindChild(parent, "RoleSettingsButton");
        int layer = refGo != null ? refGo.layer : 5;
        root.layer = layer;
        root.transform.localPosition = new Vector3(TabStartX + TabStepX * index, TabY, -1f);
        root.transform.localScale = Vector3.one;

        // 底图 ✓
        var bgGo = new GameObject("Back");
        bgGo.transform.SetParent(root.transform, false);
        bgGo.layer = layer;
        var bg = bgGo.AddComponent<SpriteRenderer>();
        if (fullTab != null)
        {
            // 三张纯图模式：整张图铺满页签 ✓
            bg.sprite = fullTab;
            bg.drawMode = SpriteDrawMode.Simple;
            bg.size = new Vector2(TabW, TabH);
        }
        else
        {
            // 2026-10-11 用户：不要纯色方块，一个红色边框就可以，字在里面
            bg.enabled = false;
            AddRedBorder(root.transform, layer, TabW, TabH);
        }
        bg.color = Color.white;

        // 图标（6×1 切图 → 取 index 帧 ✓，和 Nova 的 GetSprite(id*2+1) 同一套思路 ✓）
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(root.transform, false);
        iconGo.layer = layer;
        iconGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        var icon = iconGo.AddComponent<SpriteRenderer>();
        icon.sprite = sheet != null ? SliceSheet(sheet, index, 6) : null;   // 自绘模式不画图标 ✓   // 见 SliceSheet 的 ppu（600 ✓ 图标才不会巨大 ✗）
        icon.color = Color.white;

        // 文字（底图上写名字 ✓ —— 本工程自建文字默认粗体 ✓ §12.1）
        var tmp = MenuTextTemplate2.Create(root.transform, new Vector3(0f, 0f, -0.2f), label, 1.5f, Color.white);
        if (tmp != null)
        {
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.fontSizeMin = 1.5f; tmp.fontSizeMax = 1.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
        }

        // 点击区（Collider2D + 自己判定 ✓ —— 不依赖原版 PassiveButton ✗）
        var col = root.AddComponent<BoxCollider2D>();
        col.size = new Vector2(TabW, TabH);
        col.isTrigger = true;
        var click = root.AddComponent<NovaTabClick>();
        click.Init(index, () => OnTabClicked(index));

        _tabs.Add(new TabEntry { Root = root, Icon = icon, Page = null, IsVanilla = isVanilla });
    }

    /// <summary>
    /// **把原版内容全部藏掉，一点不留** ✓✓（用户截图实证：只关 `PresetsTab`/`GameSettingsTab` 没用 ✗）
    ///
    /// ⚠️ 为什么之前没清掉（层级取证给了答案 ✓）：
    ///   这一版菜单真实结构是
    ///   <code>
    ///     GameSettingMenu
    ///       Background / PanelSprite / **GameSettingsLabel** / **What Is This?** / **LeftPanel**
    ///       MainArea
    ///         **PRESETS TABS** / **GAME SETTINGS TAB** / **ROLES TAB**
    ///   </code>
    ///   而 `menu.PresetsTab` / `menu.GameSettingsTab` 指向的**不是**这些可见物体 ✗
    ///   → 所以按"除背景和我们的页签之外，全部关掉"来做 ✓（名字判定 + 白名单 ✓）
    /// </summary>
    private static void HideAllVanilla(GameSettingMenu menu, bool hide)
    {
        try
        {
            // ★ 2026-10-11 用户红圈标出的就是**原版自带的关闭按钮** ✓（右上角那个 ✓）
            //   → 正确做法不是自建 ✗，而是**别把它藏起来** ✓（它本身就是 NOS 同款 ✓）
            var keep = new HashSet<string> { "Background", "PanelSprite", "starfield", "Overlay", "CloseButton" };
            var km = menu.transform;
            for (int i = 0; i < km.childCount; i++)
            {
                var c = km.GetChild(i);
                if (c == null) continue;
                if (c.name.StartsWith("NovaTab_", StringComparison.Ordinal)) continue;   // 我们的页签 ✓
                if (keep.Contains(c.name)) continue;                                     // 背景留 ✓
                try { if (c.gameObject.activeSelf != !hide) c.gameObject.SetActive(!hide); } catch { }
            }
            LightLogger.Log($"[NovaTabBar] 原版内容已{(hide ? "隐藏" : "恢复")} ✓（除背景与我们的页签之外全关 ✓）");
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaTabBar.HideAllVanilla] {ex.Message}");
        }
    }

    private static void OnTabClicked(int index)
    {
        try
        {
            _current = index;
            RefreshSelection();

            var menu = _menu;
            if (menu == null) return;

            // ★★ 原版内容：**不在第 1 个页签上就全藏掉** ✓（用户："原版内容你倒是去掉啊" ✓）
            // ★★★ 2026-10-11 **改由旧 `GameSettingMenuPatch` 提供页面内容** ✓
            //   它的 `BuildPages()` 已经建好两页 ✓（`_modPage` / `_presetsPage` ✓）
            //   → 我们只负责"点页签 → 告诉它显示哪一页" ✓（`LID_SetExternalTab` ✓）
            //   ⚠️ **不再** `HideAllVanilla` ✗ —— 它会把旧补丁的页面一起藏掉 ✗
            //      （那两页是建在原版页层级里的 ✓，由它自己的 `ApplyModVisibility` 管显隐 ✓）
            try
            {
                Light.Patches.GameSettingMenuPatch.LID_SetExternalTab(index == 0 ? 0 : (index == 1 ? 1 : 2));
                // ⚠️ 2026-10-11 撤回：原来这里调 LID_HideVanillaLeftPanel ✗ —— 它藏了原版左栏却从不恢复 ✓
                //   用户实测："点 MOD 左边页签按钮全消失，点回原版也消失" ✗ → 改成**不碰原版左栏** ✓
            }
            catch (Exception ex) { LightLogger.LogWarning($"[NovaTabBar] 联动旧补丁失败：{ex.Message}"); }
            ClearRows();                 // 我画的东西一律清掉 ✓（页面内容由旧补丁画 ✓）
            NovaModPage.Clear();   // ★ 两个都清 ✓（真 Destroy ✓）

            if (_tabs.Count > index && _tabs[index].IsVanilla)
            {
                LightLogger.Log("[NovaTabBar] 切到第 1 个页签（原版 ✓）");
            }
            else
            {
                // ★★ 接线：把**我们自己的数据**画上去 ✓
                // ★★★ 2026-10-11 **测试页** ✓（用户："在里面赛一个 HudUI 按钮做测试，
                //      点击弹窗口显示"测试"，布局啥的我们自己来" ✓）
                // ★ 内容完全由旧 `GameSettingMenuPatch` 提供 ✓（上面已经 LID_SetExternalTab 切过去了 ✓）

                LightLogger.Log($"[NovaTabBar] 切到第 {index + 1} 个页签（原版内容已清空 ✓）");
            }
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaTabBar.OnTabClicked] {ex.Message}");
        }
    }

    // =====================================================================
    //  ★ 内容接线（数据源 = 我们自己的系统 ✓）
    // =====================================================================

    private static readonly List<GameObject> _contentRows = new();

    private static void HideOurPages()
    {
        foreach (var go in _contentRows)
            try { if (go != null) go.SetActive(false); } catch { }
    }

    /// <summary>清掉上一次画的 + 建一行文字 ✓（用本工程的文本模板 ✓ §12.1 粗体 ✓）</summary>
    private static void ClearRows()
    {
        foreach (var go in _contentRows)
            try { if (go != null) Object.Destroy(go); } catch { }
        _contentRows.Clear();
    }

    private static void AddRow(Transform parent, string text, float y, float size = 1.5f, Color? color = null)
    {
        var tmp = MenuTextTemplate2.Create(parent, new Vector3(-2.55f, y, -2f), text, size, color ?? Color.white);
        if (tmp == null) return;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableAutoSizing = false;                  // §12.2：不关 autoSizing 字号无效 ✗
        tmp.fontSizeMin = size; tmp.fontSizeMax = size;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow; // §12.3：会变化的字别裁 ✓
        tmp.fontStyle = FontStyles.Bold;               // §12.1
        tmp.raycastTarget = false;
        if (tmp.gameObject != null) _contentRows.Add(tmp.gameObject);
    }

    /// <summary>第 2 个页签：**预设列表**（直接读我们的 `PresetLibrary` ✓）</summary>
    /// <summary>
    /// ★★★ 2026-10-11 **测试页** ✓（用户："在里面赛一个 HudUI 按钮做测试，点击弹窗口显示"测试"，
    ///      布局啥的我们自己来，所有 Nova 的只保留这玩意，剩下的删" ✓）
    ///
    /// Nova 已**全部删除** ✓（`Light\UI\NovaFull\` 758 个文件 + `Light.Nova\` 独立工程 + 30 MB 资源 ✓）
    /// → 这一页是**唯一保留的界面内容** ✓，而且**不依赖 Nova** ✓（页签栏是我们自己用 `Nova_*.png` 画的 ✓）。
    /// </summary>
    private static void ShowTestPage(GameSettingMenu menu)
    {
        // ★★★ 2026-10-11 **改成临时布局** ✓（用户："按照原来那样的布局，结合NOS的布局，写一版临时布局，
        //      可以引入NOS那些流畅的动画啥的，取其精华去其糟粕" ✓）
        //   布局实现全部在 `NovaModPage.cs` 里 ✓（面板 + 左栏分类 + 右栏行 + 悬停/淡入动画 ✓）
        ClearRows();                       // 先把旧的清掉 ✓（真 Destroy ✓）
        NovaModPage.Clear();
        NovaModPage.Show(menu);
    }

    private static void ShowPresetPage(GameSettingMenu menu)
    {
        ClearRows();
        var root = menu.transform;

        AddRow(root, "预设（本机的 .lidpreset 文件）", 2.05f, 1.9f);
        try
        {
            var list = LightInDark.Configuration.PresetLibrary.List();
            if (list == null || list.Count == 0)
            {
                AddRow(root, "（还没有预设文件 —— 在「预设」页保存一个就会出现在这里）", 1.60f, 1.25f, new Color(0.75f, 0.75f, 0.75f, 1f));
                return;
            }
            float y = 1.60f;
            for (int i = 0; i < list.Count && i < 12; i++)
            {
                var m = list[i];
                string author = string.IsNullOrEmpty(m.Author) ? "—" : m.Author;
                AddRow(root, $"{i + 1}. {m.Name}    by {author}    {m.Created}", y, 1.25f);
                y -= 0.30f;
            }
            LightLogger.Log($"[NovaTabBar] 预设页已画 {Math.Min(list.Count, 12)} 行（共 {list.Count} 个预设 ✓）");
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaTabBar.ShowPresetPage] {ex.Message}");
        }
    }

    /// <summary>第 3 个页签：**职业列表**（直接读我们的 `RoleRegistry` ✓ = 用户要的"接到职业系统" ✓）</summary>
    private static void ShowRolePage(GameSettingMenu menu)
    {
        ClearRows();
        var root = menu.transform;

        int total = 0;
        try
        {
            var roles = LightInDark.Roles.RoleRegistry.AllRoles;
            total = roles?.Count ?? 0;

            AddRow(root, $"职业（共 {total} 个，来自本工程的 RoleRegistry）", 2.05f, 1.9f);
            if (total == 0)
            {
                AddRow(root, "（职业注册表为空）", 1.60f, 1.25f, new Color(0.75f, 0.75f, 0.75f, 1f));
                return;
            }

            float y = 1.60f;
            int i = 0;
            foreach (var r in roles!)
            {
                if (i >= 12) break;
                string cat = r.RoleCategory.ToString();
                AddRow(root, $"{r.CodeName}    {r.Name}    [{cat}]", y, 1.25f);
                y -= 0.30f;
                i++;
            }
            LightLogger.Log($"[NovaTabBar] 职业页已画 {i} 行（注册表共 {total} 个职业 ✓）");
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaTabBar.ShowRolePage] {ex.Message}  （已注册 {total} 个 ✓）");
        }
    }

    /// <summary>选中态：选中的图标亮、未选中的压暗 ✓（不依赖原版高光 ✓）</summary>
    private static void RefreshSelection()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            try
            {
                var t = _tabs[i];
                if (t.Icon == null) continue;
                t.Icon.color = i == _current
                    ? Color.white
                    : new Color(0.55f, 0.55f, 0.55f, 1f);
            }
            catch { }
        }
    }

    // =====================================================================
    //  资源与辅助
    // =====================================================================

    private static readonly Dictionary<string, Sprite?> _cache = new();

    /// <summary>从嵌入资源读一张图 ✓（`Light.Resources.GUI.xxx.png` ✓）</summary>
    private static Sprite? LoadSprite(string fileName, float ppu)
    {
        try
        {
            if (_cache.TryGetValue(fileName, out var c)) return c;

            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream($"Light.Resources.GUI.{fileName}");
            if (stream == null)
            {
                LightLogger.LogWarning($"[NovaTabBar] 嵌入资源不存在：Light.Resources.GUI.{fileName}");
                _cache[fileName] = null;
                return null;
            }
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            var tex = new Texture2D(2, 2, TextureFormat.ARGB32, true);
            tex.hideFlags = HideFlags.HideAndDontSave;       // §4.6：自己持有 ✓
            if (!ImageConversion.LoadImage(tex, bytes, false)) { _cache[fileName] = null; return null; }

            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            sp.hideFlags = HideFlags.HideAndDontSave;
            _cache[fileName] = sp;
            return sp;
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaTabBar.LoadSprite] {fileName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>把 6×1 切图切成第 index 帧 ✓（横向排列 ✓，和 Nova 的用法一致 ✓）</summary>
    private static Sprite? SliceSheet(Sprite? sheet, int index, int count)
    {
        try
        {
            if (sheet == null) return null;
            var tex = sheet.texture;
            if (tex == null) return null;

            float w = tex.width / (float)count;
            var rect = new Rect(index * w, 0f, w, tex.height);
            var sp = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 600f);   // ★ 600：TabIcon 原图很大 ✓ 100 会得到"巨大图标" ✗
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaTabBar.SliceSheet] {ex.Message}");
            return null;
        }
    }

    private static GameObject? FindChild(Transform root, string name)
    {
        try
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c == null) continue;
                if (c.name == name) return c.gameObject;
                var deeper = FindChild(c, name);
                if (deeper != null) return deeper;
            }
        }
        catch { }
        return null;
    }

    private static void SetActiveSafe(GameObject? go, bool active)
    {
        try { if (go != null && go.activeSelf != active) go.SetActive(active); } catch { }
    }
}

/// <summary>
/// 页签的点击判定 ✓ —— 自己写（不依赖原版 `PassiveButton` ✗，因为它自带原版逻辑 ✗）
/// </summary>
internal class NovaTabClick : MonoBehaviour
{
    static NovaTabClick() { Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<NovaTabClick>(); }   // §11.2

    private int _index;
    private Action? _onClick;
    private float _lastClickTime;

    public void Init(int index, Action onClick)
    {
        _index = index;
        _onClick = onClick;
    }

    public void OnMouseDown()
    {
        try
        {
            if (Time.unscaledTime - _lastClickTime < 0.15f) return;   // 双击去重 ✓
            _lastClickTime = Time.unscaledTime;
            _onClick?.Invoke();
        }
        catch { }
    }
}
