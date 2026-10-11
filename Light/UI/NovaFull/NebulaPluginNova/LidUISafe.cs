using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using UnityEngine;

namespace Nebula;

/// <summary>
/// ★★ 2026-10-11：**给 `MetaScreen` 的 UI 登记做兜底**（我们的移植件 ✓）
///
/// 背景（用户堆栈实证 ✓）：
/// <code>
///   NullReferenceException at Nebula.Modules.MetaScreen.GenerateWindow(MetaScreen.cs:1309)
///     ← PresetSettingMenu.Start → GUIButton 点击回调
///   —— 那一行是 `NebulaManager.Instance.RegisterUI(obj, button);` ✓
///      `NebulaManager.Instance` 是 null ✗（它的实例由 `SceneManager.sceneLoaded` 创建 ✓）
/// </code>
///
/// 为什么不能建 `NebulaManager`（A 方案的核心决定 ✓）：
///   实测一建它就开始接管游戏 HUD ✓ → `HudManager.Update()` 刷 NRE ✗✗
///   = "NOS 和 LID 混跑" ✓ → 违背"只搬设置界面"的目标 ✗
///
/// 所以：`RegisterUI` 只是把控件**登记给 NebulaManager 做鼠标/输入管理** ✓，
/// 拿不到就**跳过** ✓ —— 界面照样画、照样能点 ✓（少数悬停提示会没有 ✗，可接受 ✓）。
/// </summary>
public static class LidUISafe
{
    /// <summary>照 `NebulaManager.RegisterUI(GameObject, PassiveUiElement)` 的签名做兜底 ✓</summary>
    public static void Register(GameObject? obj, UnityEngine.Object? button)
    {
        // ★★ 2026-10-11 追加：**顺手把渲染层/排序修好** ✓✓
        //    （用户实测："没异常了，但看着是空的，而且能点" ✓ ——
        //      控件/碰撞盒/贴图都在 ✓，就是**被原版那层深色底板盖住** ✓ =
        //      AGENTS §4.3 记的老坑：`GameObject.layer` 决定哪台相机画它 ✗）
        //    这个方法**正好在窗口建好时被调** ✓（`MetaScreen.GenerateWindow` 的尾部 ✓）
        //    → 挂在这儿最省事 ✓，不用改 `MetaScreen` ✓
        //    ⚠️ 但窗口刚建时树是空的 ✓（诊断实证 TMP=0 ✗）→ 所以改成"**登记 + 每帧补**" ✓
        ApplyRenderSettings(obj);
        Track(obj);

        try
        {
            var manager = NebulaManager.Instance;
            if (manager == null) return;   // 没建管理器 = A 方案下的正常情况 ✓ 静默跳过 ✓
            manager.RegisterUI(obj!, button as PassiveButton);
        }
        catch (Exception ex)
        {
            // ⚠️ 只打一次，免得刷屏 ✗（这个方法是每画一个控件就调一次 ✓）
            if (!_warned)
            {
                _warned = true;
                LightInDark.Core.LightLogger.Log($"[LidUISafe] RegisterUI 跳过（{ex.Message} ✓ 之后不再重复报 ✓）");
            }
        }
    }

    /// <summary>
    /// 把整棵 UI 树挪到"大厅 UI 层"并把排序抬高 ✓（否则会被原版底板盖住 → "看得见是空的、但能点" ✗）
    /// ⚠️ 只改 layer 与 sorting ✓，**不动位置/颜色** ✗（AGENTS §4.4：别吃掉原版反馈 ✓）
    /// </summary>
    public static void ApplyRenderSettings(GameObject? root)
    {
        if (root == null) return;
        try
        {
            int uiLayer = 5;   // 大厅 UI Camera 的层（AGENTS §4.3 实测表：UI Camera = {5,15} ✓）
            try { uiLayer = LayerExpansion.GetUILayer(); } catch { }

            int count = 0;
            root.layer = uiLayer;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;
                count++;
            }

            // ⚠️ 排序：**相对抬高** ✓，不要用绝对值覆盖 ✗
            //    （Nova 自己有一套 `SpriteMask` + 各自的 sorting ✓（`MetaScreen.cs:771/808` ✓）——
            //      我上一版把它们一律改成 100/200 ✗ → 会**跳出 mask 的排序范围** ✗
            //      → 被遮罩掉 = "能点但看不见" ✓ → 改回 +50 的相对偏移 ✓，保持 Nova 原本的前后关系 ✓）
            // ★★ 2026-10-11 **停掉所有 SpriteMask** ✓✓
            //    （用户截图实证：只有中间一小块透出来 ✓，其余全黑 ✓，字也被切成碎片 ✗
            //      = **遮罩坏了** 的典型样子 ✓✓）
            //    Nova 的滚动视图用 `SpriteMask` 裁剪内容 ✓（`MetaScreen.cs:771 / :808` ✓：
            //      `UnityHelper.CreateObject<SpriteMask>("Mask", view.transform, …)` ✓）
            //    而 SpriteMask 靠一个 **sprite** 决定形状 ✓ —— 在我们这套"只搬 UI"的初始化下
            //    那个 sprite 取不到 ✗ → 遮罩范围全错 ✓ → 只留中间一块 ✓
            //   → 先**全部停掉** ✓（代价：内容可能溢出窗口边缘 ✗，但**能看见**才是第一步 ✓）
            int masks = 0;
            foreach (var mask in root.GetComponentsInChildren<SpriteMask>(true))
            {
                try { mask.enabled = false; masks++; } catch { }
            }

            int order = 0;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true)) { sr.sortingOrder += 50; order++; }
            foreach (var tmp in root.GetComponentsInChildren<TMPro.TextMeshPro>(true)) { tmp.sortingOrder += 50; order++; }

            if (!_renderLogged)
            {
                _renderLogged = true;
                LightInDark.Core.LightLogger.Log($"[LidUISafe] 渲染修正：{count} 个对象 → layer {uiLayer} ✓；停掉 {masks} 个 SpriteMask ✓；{order} 个渲染器排序 +50 ✓");

                // ★★ 2026-10-11 **一次性诊断** ✓✓（用户："这不是字糊，这好像是乱码呢" ✓）
                //    要分清两种情况 ✓：
                //      ① 文字**重叠**在同一个位置 → 看起来像乱码 ✓（坐标/布局问题 ✗）
                //      ② 字**真的错了**（字体缺字形 / 编码）→ 也是乱码 ✓
                //    判据就在下面这几行 ✓：**坐标是否都一样** + **text 内容是否正常** ✓
                try
                {
                    var tmps = root.GetComponentsInChildren<TMPro.TextMeshPro>(true);
                    int shown = 0;
                    LightInDark.Core.LightLogger.Log($"[LidUISafe 诊断] 树内 TMP 共 {tmps.Length} 个，前 12 个：");
                    foreach (var tmp in tmps)
                    {
                        if (shown++ >= 12) break;
                        string txt = (tmp.text ?? "").Replace("\n", "\\n");
                        if (txt.Length > 24) txt = txt.Substring(0, 24) + "…";
                        float fs = tmp.fontSize;
                        LightInDark.Core.LightLogger.Log(
                            $"    [{shown}] \"{txt}\"  pos={tmp.transform.localPosition}  scale={tmp.transform.localScale}  " +
                            $"w={tmp.rectTransform.sizeDelta.x:0.##} h={tmp.rectTransform.sizeDelta.y:0.##}  " +
                            $"fs={fs:0.##} auto={tmp.enableAutoSizing}  font={(tmp.font != null ? tmp.font.name : "null")}  " +
                            $"enabled={tmp.enabled} sr={tmp.sortingOrder}");
                    }
                    var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
                    int shown2 = 0;
                    LightInDark.Core.LightLogger.Log($"[LidUISafe 诊断] 树内 SpriteRenderer 共 {srs.Length} 个，前 8 个：");
                    foreach (var sr in srs)
                    {
                        if (shown2++ >= 8) break;
                        LightInDark.Core.LightLogger.Log(
                            $"    [{shown2}] \"{sr.gameObject.name}\"  pos={sr.transform.localPosition}  scale={sr.transform.localScale}  " +
                            $"sprite={(sr.sprite != null ? sr.sprite.name : "null ✗")}  size={sr.size}  " +
                            $"enabled={sr.enabled}  color={sr.color}  order={sr.sortingOrder}  layer={sr.gameObject.layer}");
                    }
                }
                catch (Exception ex) { LightInDark.Core.LightLogger.LogWarning($"[LidUISafe 诊断] {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            if (!_renderLogged) { _renderLogged = true; LightInDark.Core.LightLogger.LogWarning($"[LidUISafe.ApplyRenderSettings] {ex.Message}"); }
        }
    }

    private static bool _warned;
    private static bool _renderLogged;

    // ═══════════════════════════════════════════════════════════════════════
    //  ★★ 2026-10-11 **持续应用渲染修正** ✓✓
    //
    //  为什么（诊断日志实证 ✓）：
    //    `渲染修正：6 个对象 → layer 5` 之后紧跟
    //      **`树内 TMP 共 0 个`** ✗、`SpriteRenderer 共 3 个` ✗
    //    → `MetaScreen` 建窗口时**树里几乎是空的** ✓，控件是**之后才逐个建出来**的 ✓
    //    → 我原来只在"创建那一刻"应用一次 ✗ → **对真正的内容一个都没生效** ✓✓
    //      这就一次性解释了：改层没变化 ✓、只有中间一小块 ✓、按钮不显示 ✓
    //
    //  → 改成一个**每帧驱动**（只在有窗口时干活 ✓，窗口没了自动停 ✓）
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly List<GameObject> _roots = new();
    private static LidUIRenderDriver? _driver;

    internal static void Track(GameObject? root)
    {
        if (root == null) return;
        if (!_roots.Contains(root)) _roots.Add(root);

        if (_driver == null)
        {
            try
            {
                var go = new GameObject("LidUIRenderDriver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _driver = go.AddComponent<LidUIRenderDriver>();
                LightInDark.Core.LightLogger.Log("[LidUISafe] 渲染驱动已启动 ✓（每帧给新出现的控件补 layer/排序 ✓）");
            }
            catch (Exception ex) { LightInDark.Core.LightLogger.LogWarning($"[LidUISafe] 驱动启动失败：{ex.Message}"); }
        }
    }

    /// <summary>每帧给所有跟踪的窗口补渲染设置 ✓（只对新出现的对象动手 ✓，幂等 ✓）</summary>
    internal static void Tick()
    {
        // ★★ 2026-10-11 **主动找屏幕，而不是等 Register** ✓✓
        //   为什么（日志实证 ✓）：
        //     本次运行(19:21)里 `LidUISafe` **一条日志都没有** ✗ ——
        //     因为 `Register` 挂在 `MetaScreen.GenerateWindow` 上 ✓，
        //     而**主设置页走的是 `MetaScreen.SetWidget`** ✗（`GenerateWindow` 只有"二级页"才用 ✓）
        //     → 我那个每帧驱动**从没跟踪到主页面** ✓ → layer/遮罩/排序修正**全部没生效** ✓✓
        //   → 改成每帧**自己把场景里的 `MetaScreen` 找出来** ✓（同程序集 ✓ 直接引用 ✓）
        try
        {
            foreach (var screen in UnityEngine.Object.FindObjectsOfType<MetaScreen>())
            {
                if (screen == null) continue;
                var go = screen.gameObject;
                if (go == null) continue;
                if (!_roots.Contains(go)) Track(go);
            }
        }
        catch { }

        for (int i = _roots.Count - 1; i >= 0; i--)
        {
            var r = _roots[i];
            if (r == null) { _roots.RemoveAt(i); continue; }   // Unity 假 null 要用 == ✓（AGENTS §4.6.1）
            ApplyToNewObjects(r);
        }

        // ★★ 2026-10-11 **延迟诊断** ✓✓（用户："还是糊成一团，还是遮罩" ✗）
        //   上次诊断在"窗口刚建那一刻"采样 ✓ → 拿到 `TMP 共 0 个` ✗，等于什么也没看到 ✓
        //   这次改成**建好后第 2 秒、第 6 秒各采一次** ✓ —— 那时控件才是真建好了 ✓
        //   要判的两件事：
        //     ① 内容是不是被**缩成一团**（`scale` 很小 / 坐标都挤在原点 ✓）
        //     ② 是不是**被遮罩裁掉**（`SpriteMask` 的 scale/enabled ✓ + 控件的 sorting ✓）
        if (_roots.Count > 0)
        {
            _diagTimer += Time.deltaTime;
            if (!_diagDone2s && _diagTimer > 2f) { _diagDone2s = true; DumpTree("2 秒"); }
            else if (!_diagDone6s && _diagTimer > 6f) { _diagDone6s = true; DumpTree("6 秒"); }
        }
    }

    private static float _diagTimer;
    private static bool _diagDone2s, _diagDone6s;

    /// <summary>把窗口树的真实状态打出来 ✓（坐标/缩放/图/字 ✓ —— 一次定性 ✓）</summary>
    private static void DumpTree(string when)
    {
        try
        {
            var root = _roots.Count > 0 ? _roots[_roots.Count - 1] : null;
            if (root == null) return;

            var tmps = root.GetComponentsInChildren<TMPro.TextMeshPro>(true);
            var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
            var masks = root.GetComponentsInChildren<SpriteMask>(true);
            LightInDark.Core.LightLogger.Log(
                $"[LidUISafe 诊断@{when}] root=\"{root.name}\" scale={root.transform.localScale} " +
                $"TMP={tmps.Length} SR={srs.Length} Mask={masks.Length}");

            for (int i = 0; i < masks.Length && i < 4; i++)
                LightInDark.Core.LightLogger.Log($"    [Mask{i + 1}] \"{masks[i].gameObject.name}\" enabled={masks[i].enabled} " +
                    $"scale={masks[i].transform.localScale} sprite={(masks[i].sprite != null ? masks[i].sprite.name : "null")} " +
                    $"alphaCutoff={masks[i].alphaCutoff} front={masks[i].frontSortingOrder} back={masks[i].backSortingOrder}");

            for (int i = 0; i < srs.Length && i < 10; i++)
            {
                var sr = srs[i];
                LightInDark.Core.LightLogger.Log($"    [SR{i + 1}] \"{sr.gameObject.name}\" pos={sr.transform.localPosition} " +
                    $"scale={sr.transform.localScale} size={sr.size} sprite={(sr.sprite != null ? sr.sprite.name : "null ✗")} " +
                    $"enabled={sr.enabled} order={sr.sortingOrder} layer={sr.gameObject.layer}");
            }

            for (int i = 0; i < tmps.Length && i < 10; i++)
            {
                var tmp = tmps[i];
                string txt = (tmp.text ?? "").Replace("\n", "\\n");
                if (txt.Length > 18) txt = txt.Substring(0, 18) + "…";
                LightInDark.Core.LightLogger.Log($"    [TMP{i + 1}] \"{txt}\" pos={tmp.transform.localPosition} " +
                    $"scale={tmp.transform.localScale} fs={tmp.fontSize:0.##} auto={tmp.enableAutoSizing} " +
                    $"font={(tmp.font != null ? tmp.font.name : "null")} enabled={tmp.enabled} order={tmp.sortingOrder} layer={tmp.gameObject.layer}");
            }
        }
        catch (Exception ex) { LightInDark.Core.LightLogger.LogWarning($"[LidUISafe 诊断@{when}] {ex.Message}"); }
    }

    /// <summary>只给"还没处理过"的对象补 ✓（用一个标记组件记 ✓，避免每帧重复抬高排序 ✗）</summary>
    private static void ApplyToNewObjects(GameObject root)
    {
        try
        {
            int uiLayer = 5;
            try { uiLayer = LayerExpansion.GetUILayer(); } catch { }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (go.layer != uiLayer) go.layer = uiLayer;
                if (go.GetComponent<LidRenderMark>() != null) continue;
                go.AddComponent<LidRenderMark>();      // 打过标记 = 不再重复处理 ✓

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder += 50;  // 相对抬高 ✓（保持 Nova 自己的前后关系 ✓）
                var tmp = go.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null) tmp.sortingOrder += 50;
                var mask = go.GetComponent<SpriteMask>();
                if (mask != null) mask.enabled = false; // 遮罩在这套初始化下算不对 ✓ → 先不裁 ✓
            }
        }
        catch { }
    }

    /// <summary>给每个处理过的对象打的标记 ✓（空组件 ✓）</summary>
    public class LidRenderMark : MonoBehaviour
    {
        static LidRenderMark() { try { Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<LidRenderMark>(); } catch { } }
    }

    /// <summary>每帧驱动 ✓</summary>
    public class LidUIRenderDriver : MonoBehaviour
    {
        static LidUIRenderDriver() { try { Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<LidUIRenderDriver>(); } catch { } }
        private void Update() { Tick(); }
    }
}
