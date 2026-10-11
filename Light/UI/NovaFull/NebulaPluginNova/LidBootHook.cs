using System;
using AmongUs.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Nebula;

/// <summary>
/// ★★ 2026-10-11：**NOS(Nova)启动钩子的精简版**（我们的移植件 ✓）
///
/// 为什么需要：Nova 原本在 `NebulaPlugin.LoadInternal()` 里做两件**UI 离不开**的事 ✓，
/// 而我只挂了 UI 补丁 ✗ → 缺了它们 ✓ → 用户实测两条 NRE（堆栈实证 ✓）：
/// <code>
///   ConfigurationTab 还是 null → `ConfigurationsAPI.cs:29` 里 tabs.Aggregate(...) NRE ✓
///   NebulaManager.Instance 是 null → `MetaScreen.cs:1309` RegisterUI(...) NRE ✓
/// </code>
///
/// 这里**照抄 `Nebula.cs:203-214` 与 `Nebula.cs:236`** 的两段 ✓，但**不碰**它的
/// `Harmony.PatchAll()` ✗（那才是"整个 NOS 一起跑"的来源 ✗，见 `LightPlugin.Load` 的说明 ✓）：
///   ① `SceneManager.sceneLoaded` → 每次场景加载新建 `NebulaManager` ✓
///      （**这就是 `NebulaManager.Instance` 的来源** ✓ —— 它不是在别处 new 的 ✓）
///   ② `Language.OnChangeLanguage(...)` → 语言/配置注册 ✓
///
/// ⚠️ 这是**对移植代码的增补** ✓，合并前会统一标注 ✓（不混进"原始移植"里 ✓）。
/// </summary>
public static class LidBootHook
{
    private static bool _installed;

    /// <summary>
    /// 挂场景加载钩子（照抄 `Nebula.cs:203` ✓）
    /// </summary>
    /// <param name="withNebulaManager">
    /// ⚠️ 2026-10-11 A 方案：**传 false** ✓ ——
    ///   只建"常驻对象" ✓（`ResidentBehaviour` = `DontDestroyOnLoad` 的所有者 ✓），
    ///   **不建 `NebulaManager`** ✗（它一存在就接管 HUD ✓ → `HudManager.Update` 刷 NRE ✗）
    /// ⚠️ 但常驻对象**必须建** ✓ —— 用户实测：改配置时报
    ///   `DataSaver.WriteToFile()`（`DataSaver.cs:340` ✓）
    ///   `ModSingleton&lt;ResidentBehaviour&gt;.Instance.StartCoroutine(...)` ✗
    ///   → **存档的协程就挂在它身上** ✓，没有它 = 配置改不动/存不下来 ✓
    /// </param>
    public static void Install(bool withNebulaManager = false)
    {
        if (_installed) return;
        _installed = true;

        bool isFirst = true;
        SceneManager.sceneLoaded += (UnityAction<Scene, LoadSceneMode>)((scene, loadMode) =>
        {
            try
            {
                // ① 常驻对象：**一定要建** ✓（存档要用 ✓）
                if (isFirst)
                {
                    isFirst = false;
                    var residentObj = new GameObject("ResidentObject");
                    residentObj.AddComponent<ResidentBehaviour>().MarkDontUnload();
                    residentObj.MarkDontUnload();
                    LightInDark.Core.LightLogger.Log("[NovaBootHook] 常驻对象 ResidentObject 已建 ✓（存档协程挂在它身上 ✓）");
                }

                // ② NebulaManager：**A 方案下不建** ✗（它才是"NOS 混跑"的来源 ✗）
                if (withNebulaManager)
                {
                    new GameObject("NebulaManager").AddComponent<NebulaManager>();
                    LightInDark.Core.LightLogger.Log($"[NovaBootHook] 场景 {scene.name}：已创建 NebulaManager ✓");
                }

                // ③ ★★ 2026-10-11 **每次场景加载都刷一次语言** ✓✓
                //    （用户截图实证：界面上全是 `*options.task` 这种**翻译键** ✗
                //      —— `*` 前缀就是 Nova 的"**没查到翻译**"标记 ✓✓）
                //    原因：我原来只在**插件加载那一刻**调了一次 `Language.OnChangeLanguage` ✗，
                //      而那时 `DataManager.Settings.Language` 还没就绪 ✓ → 等于没加载 ✓
                //    语言就绪是在进入场景之后 ✓ → 所以挂在这里每场景刷一次 ✓（幂等 ✓，代价极小 ✓）
                try
                {
                    Language.OnChangeLanguage((uint)AmongUs.Data.DataManager.Settings.Language.CurrentLanguage);
                    LightInDark.Core.LightLogger.Log($"[NovaBootHook] 场景 {scene.name}：语言已刷新 ✓（{AmongUs.Data.DataManager.Settings.Language.CurrentLanguage} ✓）");
                }
                catch (Exception lex) { LightInDark.Core.LightLogger.LogWarning($"[NovaBootHook] 语言刷新失败：{lex.Message}"); }
            }
            catch (Exception ex)
            {
                LightInDark.Core.LightLogger.LogWarning($"[NovaBootHook] 场景加载处理失败：{ex.Message}");
            }
        });

        LightInDark.Core.LightLogger.Log($"[NovaBootHook] 场景加载钩子已挂 ✓（常驻对象必建 ✓ NebulaManager={(withNebulaManager ? "建 ✓" : "不建 ✗ A 方案")}）");
    }

    /// <summary>语言/配置初始化（照抄 `Nebula.cs:236` ✓）</summary>
    public static void InitLanguage()
    {
        try
        {
            Language.OnChangeLanguage((uint)DataManager.Settings.Language.CurrentLanguage);
            LightInDark.Core.LightLogger.Log($"[NovaBootHook] 语言已初始化（{DataManager.Settings.Language.CurrentLanguage} ✓）");
        }
        catch (Exception ex)
        {
            LightInDark.Core.LightLogger.LogWarning($"[NovaBootHook] 语言初始化失败：{ex.Message}");
        }
    }
}
