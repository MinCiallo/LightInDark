using System;
using System.Collections;
using System.Reflection;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Injection;
using LightInDark.Core;
using UnityEngine;

namespace Light.Patches;

/// <summary>
/// ★★ 2026-10-11：**NOS(Nova)UI 的初始化宿主** ✓
///
/// 为什么需要它（用户实测堆栈实证 ✓）：
/// <code>
///   NullReferenceException at Nebula.VanillaAsset.GenerateScroller(VanillaAsset.cs:177)
///     ← GUIScrollView.Instantiate ← MetaScreen.SetWidget ← NebulaSettingMenu.Start
///   NullReferenceException at Virial.NebulaAPI.get_GUI(NebulaAPI.cs:96)
///     ← PresetSettingMenu.Start
/// </code>
/// 两条都是"**Nova 的 UI 框架没初始化**" ✗：
///   ① `NebulaAPI.instance` 是 null ✗ → `GUI`（控件引擎）取不到 ✓
///      它在 `Nebula.cs:221` 由 `NebulaAPI.instance = new NebulaImpl();` 赋值 ✓
///      （`NebulaImpl` 的构造函数很轻 ✓，只注册 6 个模块 ✓，其中就含 `GUI.API` ✓）
///   ② `VanillaAsset.PlayerOptionsMenuPrefab` 是 null ✗ → 画滚动条时 NRE ✓
///      它由 `VanillaAsset.CoLoadAssetOnTitle()` **协程**加载 ✓
///      （要标题界面在场 ✓ —— 它第一句就读 `TwitchManager.Instance` ✓，所以要等 ✓）
///
/// 为什么不用 `LightPlugin` 直接跑：它不是 MonoBehaviour ✗（BepInEx 6 的 `BasePlugin` 是普通类 ✓），
/// 没有 `StartCoroutine` ✓ → 所以建这个常驻宿主 ✓。
/// </summary>
public class NovaBootstrap : MonoBehaviour
{
    static NovaBootstrap() => ClassInjector.RegisterTypeInIl2Cpp<NovaBootstrap>();

    private static NovaBootstrap? _instance;
    private static Assembly? _novaAsm;

    /// <summary>
    /// ★ **总开关**：是否挂 Nova 的"完整启动钩子"（= 创建 `NebulaManager` ✓）
    ///
    /// ⚠️ 2026-10-11 实测结论：**打开它会刷 `HudManager.Update` NRE** ✗
    ///    （`NebulaManager` 一存在就接管 HUD/行为 ✓ → "NOS 和 LID 混跑" ✓）
    /// → 现在**关着** ✓：页签会出现 ✓、界面画不出来 ✗、但**玩法一点不受影响** ✓
    /// → 要真正画出 NOS 的界面，得走"最小渲染初始化"（不建 NebulaManager ✓）——
    ///    即把 `ConfigurationTab` / `GameModes` 这类**只给 UI 用**的注册补上 ✓，
    ///    并给 `MetaScreen.RegisterUI`（它要 `NebulaManager.Instance` ✗）打兜底 ✓。
    /// </summary>
    private const bool BootHookEnabled = false;

    /// <summary>由 `LightPlugin.Load` 调一次 ✓（重复调只记日志 ✓）</summary>
    public static void Ensure(Assembly novaAsm)
    {
        _novaAsm = novaAsm;
        if (_instance != null) return;

        try
        {
            var go = new GameObject("LightNovaBootstrap");
            GameObject.DontDestroyOnLoad(go);
            _instance = go.AddComponent<NovaBootstrap>();
            _instance.StartCoroutine(_instance.CoBootstrap().WrapToIl2Cpp());
            LightLogger.Log("[NovaBootstrap] 宿主已创建 ✓");
        }
        catch (Exception ex)
        {
            LightLogger.LogError("[NovaBootstrap.Ensure]", ex);
        }
    }

    private IEnumerator CoBootstrap()
    {
        var asm = _novaAsm;
        if (asm == null) { LightLogger.LogWarning("[NovaBootstrap] 没有 Light.Nova 程序集 ✗"); yield break; }

        // ① NebulaAPI（控件引擎 ✓）—— 立刻就能做 ✓
        InitNebulaApi(asm);

        // ①b ⚠️⚠️ 2026-10-11 **暂时关掉 Nova 的启动钩子** ✗
        //
        //   为什么关（用户第四轮日志实证 ✓）：
        //     挂上 `SceneManager.sceneLoaded → new NebulaManager()` 之后，
        //     **`HudManager.Update()` 开始刷 NRE** ✗✗（刷屏级 ✓）——
        //     说明 `NebulaManager` 一存在就开始接管游戏的 HUD / 行为 ✓
        //     = 正是我们一直要避免的"**NOS 和 LID 混跑**" ✓
        //   → 先关掉 ✓，把游戏恢复到"只多一个页签、不影响玩法"的状态 ✓
        //   → 下一步要么走"最小渲染初始化"✓（不建 NebulaManager ✓），
        //     要么明确接受完整运行 NOS ✓ —— 由用户拍板 ✓（见对话里的 A/B 方案 ✓）
        // ①b1 ★★★ 2026-10-11 **最先要做的事：把 `NebulaAPI.Preprocessor` 建出来** ✓✓✓
        //
        //   为什么（用户第 8 轮日志，一路挖到最底层 ✓）：
        //     `Nebula.Roles.Crewmate.Crewmate` 的**静态构造**崩在 `Crewmate.cs:14` ✓：
        //       static readonly public RoleTeam MyTeam =
        //           **NebulaAPI.Preprocessor!**.CreateTeam("teams.crewmate", …);   // ← 它 null ✗✓
        //     而 `NebulaAPI.preprocessor` 只由 **`NebulaPreprocessorImpl` 的构造函数**赋值 ✓
        //       （`Modules\Preloader.cs:38` → `NebulaAPI.preprocessor = this;` ✓）
        //     → **那个单例实例从来没被创建过** ✗✓
        //   连锁反应（日志实证 ✓）：
        //     `Preprocessor` null ✗ → `Crewmate` cctor 炸 ✗ → `SetNebulaTeams()` 炸 ✗
        //       → `NoSRoleSetUp` cctor 炸 ✗ → **`SetTabs()` 压根没执行** ✗
        //       → `ConfigurationTab` 全 null ✗ → `GeneralConfigurations` cctor 炸 ✗ → 页面空白 ✓
        //   → 所以**先把它建出来** ✓（`Instance` 是静态字段 `= new NebulaPreprocessorImpl()` ✓，
        //      跑它的静态构造即可 ✓；构造函数很轻 ✓：只设 preprocessor + 建 16 个相的空列表 ✓）
        try
        {
            var implType = asm.GetType("Nebula.Modules.NebulaPreprocessorImpl");
            if (implType == null) LightLogger.LogWarning("[NovaBootstrap] 找不到 NebulaPreprocessorImpl ✗");
            else
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(implType.TypeHandle);

                var apiTy = asm.GetType("Virial.NebulaAPI");
                var flagsP = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var prepVal = apiTy?.GetProperty("Preprocessor", flagsP)?.GetValue(null)
                           ?? apiTy?.GetField("preprocessor", flagsP)?.GetValue(null);
                LightLogger.Log(prepVal != null
                    ? "[NovaBootstrap] ★★★ NebulaAPI.Preprocessor 已就位 ✓✓（角色/页签的静态构造这下能跑了 ✓）"
                    : "[NovaBootstrap] ✗ NebulaAPI.Preprocessor 仍是 null ✗（后面一定还会崩 ✓）");
            }
        }
        catch (Exception ex) { LightLogger.LogError("[NovaBootstrap] 建 Preprocessor 失败", ex); }

        // ①b2 ★★ 2026-10-11 **点名调用 `SetTabs()`**（不再绕预处理器 ✓✓）
        //
        //   为什么（用户第 5 轮日志实证 ✓）：
        //     逐相跑预处理器的代码**一条日志都没出现** ✗ → 那条路没生效 ✓
        //     而那 6 个 `ConfigurationTab` 的赋值就在**一个方法里** ✓：
        //       `NebulaPluginNova\Roles\Roles.cs:22  static private void SetTabs()`
        //       （它所在的类标着 `[NebulaPreprocess(PreprocessPhase.PreRoles)]` ✓
        //         → 正常流程由预处理器按相调用 ✗，预处理器没跑起来 ✓）
        //   → **直接反射调它** ✓，最短路径 ✓，不依赖任何相 ✓
        //   ⚠️ 它是 `internal class NoSRoleSetUp` + `static private void SetTabs()` ✗
        //      → 反射要带 `NonPublic` ✓（AGENTS：interop 里 private 常变 public，但反射两边都试 ✓）
        try
        {
            var flagsT = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var setUpType = asm.GetType("Nebula.Roles.NoSRoleSetUp")
                         ?? asm.GetType("Nebula.Roles.NoSRoleSetUp", false, true);
            if (setUpType == null) LightLogger.LogWarning("[NovaBootstrap] 找不到 Nebula.Roles.NoSRoleSetUp ✗");
            else
            {
                var m = setUpType.GetMethod("SetTabs", flagsT);
                if (m == null) LightLogger.LogWarning("[NovaBootstrap] 找不到 NoSRoleSetUp.SetTabs ✗");
                else
                {
                    m.Invoke(null, null);
                    // 校验真的建出来了 ✓（别用"我以为" ✓）
                    var tabType = asm.GetType("Virial.Configuration.ConfigurationTab");
                    var settingProp = tabType?.GetProperty("Settings", flagsT);
                    var val = settingProp?.GetValue(null);
                    LightLogger.Log(val != null
                        ? "[NovaBootstrap] ★ SetTabs() 调用成功 ✓ ConfigurationTab.Settings 已非空 ✓✓"
                        : "[NovaBootstrap] ✗ SetTabs() 调了但 ConfigurationTab.Settings 仍为 null ✗（方法内部可能提前抛了 ✓）");
                }
            }
        }
        catch (Exception ex) { LightLogger.LogError("[NovaBootstrap] 调 NoSRoleSetUp.SetTabs 失败", ex); }

        // ①b3 ★★ 2026-10-11 **跑那些"静态构造里做注册"的类** ✓✓
        //
        //   为什么（用户第 6 轮日志实证 —— 这一轮**堆栈变了** ✓✓）：
        //     `SetTabs()` 那步**成功了** ✓ —— 崩的位置从
        //       `ConfigurationsAPI.cs:29`（`ConfigurationTab` 的聚合 ✗）
        //     挪到了
        //       `ConfigurationsAPI.cs:30`（**`GameModeDefinition` 的聚合** ✗）
        //     → 也就是"**一行一行在往前走**" ✓，现在缺的是 `GameModes` ✓
        //   它的赋值在 `Game\GameMode.cs:15` 的**静态构造**里 ✓（`static GameModeDefinitionImpl()` ✓）
        //   → 这种必须用 `RunClassConstructor` ✓（普通方法用 `Invoke` ✓，两者都要 ✓）
        try
        {
            var flagsC = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            string[] cctorTypes =
            {
                "Nebula.Game.GameModeDefinitionImpl",   // ★ GameModes.Standard / FreePlay / AeroGuesser / PaintQuiz ✓
            };
            foreach (var tn in cctorTypes)
            {
                var ty = asm.GetType(tn);
                if (ty == null) { LightLogger.LogWarning($"[NovaBootstrap] 找不到 {tn} ✗"); continue; }
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(ty.TypeHandle);

                // 校验 ✓（还是那句：别用"我以为" ✓）
                var gmType = asm.GetType("Virial.Game.GameModes");
                var stdProp = gmType?.GetField("Standard", flagsC) ?? (System.Reflection.MemberInfo?)gmType?.GetProperty("Standard", flagsC);
                object? stdVal = stdProp is System.Reflection.FieldInfo fi ? fi.GetValue(null) : (stdProp as System.Reflection.PropertyInfo)?.GetValue(null);
                LightLogger.Log(stdVal != null
                    ? $"[NovaBootstrap] ★ {tn} 静态构造已跑 ✓ GameModes.Standard 已非空 ✓"
                    : $"[NovaBootstrap] ✗ {tn} 跑了但 GameModes.Standard 仍为 null ✗");
            }
        }
        catch (Exception ex) { LightLogger.LogError("[NovaBootstrap] 跑静态构造失败", ex); }

        // ①c 预处理器（保留作后备 ✓ —— 逐相容错 ✓，失败了也不影响上面那步 ✓）
        {
            object? prep = null;
            try
            {
                var implType = asm.GetType("Nebula.Modules.NebulaPreprocessorImpl");
                var flags3 = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                prep = implType?.GetProperty("Instance", flags3)?.GetValue(null)
                    ?? implType?.GetField("Instance", flags3)?.GetValue(null);
                if (prep == null) LightLogger.LogWarning("[NovaBootstrap] 拿不到 NebulaPreprocessorImpl.Instance ✗");
            }
            catch (Exception ex) { LightLogger.LogError("[NovaBootstrap] 取预处理器实例失败", ex); }

            if (prep != null)
            {
                var pt = prep.GetType();
                var flags4 = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var pickUp = pt.GetMethod("PickUpPreprocess", flags4);
                var runPhase = pt.GetMethod("RunPreprocess", flags4);

                if (pickUp == null || runPhase == null)
                    LightLogger.LogWarning($"[NovaBootstrap] 预处理器方法找不到 ✗（pickUp={pickUp != null} run={runPhase != null}）");
                else
                {
                    try { pickUp.Invoke(prep, new object[] { asm }); LightLogger.Log($"Preprocessor: PickUpPreprocess 成功 ✓"); }
                    catch (Exception ex) { LightLogger.LogError("[NovaBootstrap] PickUpPreprocess 失败", ex); }

                    var phaseEnum = runPhase.GetParameters()[0].ParameterType;
                    int total = Enum.GetValues(phaseEnum).Length;
                    LightLogger.Log($"[NovaBootstrap] 开始跑 Nova 预处理器：共 {total} 相 ✓");

                    for (int i = 0; i < total; i++)
                    {
                        // ⚠️ 每一相单独取协程 + 单独 try ✓（yield 不能进 try ✗ → 先取出来 ✓）
                        IEnumerator? phaseCo = null;
                        try
                        {
                            var phase = Enum.ToObject(phaseEnum, i);
                            phaseCo = runPhase.Invoke(prep, new[] { phase }) as IEnumerator;
                        }
                        catch (Exception ex) { LightLogger.LogWarning($"[NovaBootstrap] 第 {i} 相调用失败：{ex.Message}"); }

                        if (phaseCo != null)
                        {
                            string phaseName = Enum.GetName(phaseEnum, Enum.ToObject(phaseEnum, i)) ?? i.ToString();

                            // ⚠️ `yield return` 不能写在 try/catch 里 ✗（AGENTS §11.6 的老坑 ✓）
                            //    → 改成**手动迭代**这个协程 ✓：每步单独 try ✓，
                            //      这样"某一相抛异常"只警告 ✓，不会打断整个初始化 ✓（这是与 `CoLoad` 的关键区别 ✓）
                            bool failed = false;
                            while (true)
                            {
                                bool moved;
                                Exception? stepEx = null;
                                try { moved = phaseCo.MoveNext(); }
                                catch (Exception ex) { moved = false; stepEx = ex; }

                                if (stepEx != null)
                                {
                                    LightLogger.LogWarning($"[NovaBootstrap] 相 {i} ({phaseName}) 失败 ✗（继续后面的 ✓）：{stepEx.Message}");
                                    failed = true;
                                    break;
                                }
                                if (!moved) break;

                                yield return phaseCo.Current;
                            }

                            if (!failed) LightLogger.Log($"[NovaBootstrap] 相 {i} ({phaseName}) ✓ 跑完 ✓");
                        }
                    }
                    LightLogger.Log("[NovaBootstrap] Nova 预处理器全部跑完 ✓（ConfigurationTab / 职业 / 配置定义应已就位 ✓）");
                }
            }
        }

        // ★★ 2026-10-11：**常驻对象一定要挂** ✓（`withNebulaManager: false` = A 方案 ✓）
        //    用户实测：改配置时报 `DataSaver.WriteToFile()`（`DataSaver.cs:340` ✓）——
        //    存档协程挂在 `ResidentBehaviour` 上 ✓，没有它配置就存不下来 ✓
        try
        {
            var hook = asm.GetType("Nebula.LidBootHook");
            var flags2 = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            hook?.GetMethod("Install", flags2)?.Invoke(null, new object[] { BootHookEnabled });
            hook?.GetMethod("InitLanguage", flags2)?.Invoke(null, null);
            if (hook == null) LightLogger.LogWarning("[NovaBootstrap] 找不到 Nebula.LidBootHook ✗");
        }
        catch (Exception ex) { LightLogger.LogError("[NovaBootstrap] 挂 Nova 启动钩子失败", ex); }
        LightLogger.Log($"[NovaBootstrap] 启动钩子：常驻对象=建 ✓，NebulaManager={(BootHookEnabled ? "建" : "不建（A 方案 ✓）")}");

        // ② 等游戏进去（`AmongUsClient` 起来 = 过了启动画面 ✓）
        //    ⚠️ `CoLoadAssetOnTitle` 第一句就读 `TwitchManager.Instance` ✗（标题界面才有 ✓）
        //       → 所以要多等一会儿 ✓，并且后面的 try/catch 兜住 ✓
        float t = 0f;
        while (t < 90f)
        {
            bool ready = false;
            try { ready = AmongUsClient.Instance != null; } catch { }
            if (ready) break;
            t += Time.deltaTime;
            yield return null;
        }
        LightLogger.Log($"[NovaBootstrap] AmongUsClient 就绪（等了 {t:0.0}s ✓）");
        yield return new WaitForSeconds(1.5f);

        // ③ 反复尝试跑 Nova 的素材协程 ✓
        //    ⚠️ `CoLoadAssetOnTitle` **第一句就读 `TwitchManager.Instance`** ✗（只有标题界面有 ✓）
        //       —— 所以不能只等 `AmongUsClient` ✗，要**一直重试到 `PlayerOptionsMenuPrefab` 真的不为 null** ✓
        //       （它内部那句读不到就抛 ✗ → 我们 catch 住 → 隔一秒再来 ✓）
        float t2 = 0f;
        while (t2 < 300f && !IsVanillaAssetReady(asm))
        {
            IEnumerator? co = null;
            try
            {
                var vaType = asm.GetType("Nebula.VanillaAsset");
                var coMethod = vaType?.GetMethod("CoLoadAssetOnTitle",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (coMethod != null) co = coMethod.Invoke(null, null) as IEnumerator;
            }
            catch { /* 标题界面还没到 ✓ → 下一轮再试 ✓ */ }

            if (co != null)
            {
                // yield return 不能放在 try/catch 里 ✗ → 已经取出来了 ✓
                yield return co;
            }

            if (IsVanillaAssetReady(asm))
            {
                LightLogger.Log($"[NovaBootstrap] 原版素材已就位 ✓（PlayerOptionsMenuPrefab 非空 ✓，耗时 {t2:0.0}s ✓）");
                yield break;
            }

            // ★ 兜底路径 ✓（用户第二轮日志实证：`CoLoadAssetOnTitle` 那句 `TwitchManager.Instance`
            //    在这版可能一直取不到 ✗ → 那条路永远失败 ✓ → 我们自己填 ✓）
            //   ① `PlayerOptionsMenuPrefab`：先按 Nova 的名字找资源 ✓（`UnityHelper.FindAsset` ✓），
            //      找不到就**拿大厅里活着的那个实例** ✓（日志里就有 `PlayerOptionsMenu(Clone)` ✓，
            //      结构一样 ✓ → Instantiate 它照样能拿到 `UI_ScrollbarTrack` ✓）
            //   ② `StandardTextPrefab`：用**我们工程自己的**标准文本预制体 ✓（AGENTS §6 ✓）
            TryFillVanillaAsset(asm);

            t2 += 1f;
            yield return new WaitForSeconds(1f);
        }

        if (!IsVanillaAssetReady(asm))
            LightLogger.LogWarning($"[NovaBootstrap] 等了 {t2:0.0}s 原版素材仍没就位 ✗ —— Nova 的滚动条会 NRE ✓（要查 TwitchManager 是否可达 ✗）");
    }

    private static void InitNebulaApi(Assembly asm)
    {
        try
        {
            var apiType = asm.GetType("Virial.NebulaAPI");
            var implType = asm.GetType("Nebula.NebulaImpl");
            if (apiType == null || implType == null)
            {
                LightLogger.LogWarning($"[NovaBootstrap] 找不到 NebulaAPI/NebulaImpl ✗（api={apiType != null} impl={implType != null}）");
                return;
            }

            // ★★ `instance` 是**字段** ✗ 不是属性 ✗：
            //    `NebulaAPI.cs:79  static internal INebula instance = null!;` ✓
            //    我第一版用 `GetProperty("instance")` ✗ → 取不到 → 只打警告 →
            //    **`instance` 一直是 null** ✓ → `GUI` NRE 一直不变 ✓（用户第二轮日志实证 ✓）
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var field = apiType.GetField("instance", flags);

            if (field == null)
            {
                LightLogger.LogWarning("[NovaBootstrap] 找不到 NebulaAPI.instance 字段 ✗（连属性也试一下 ✓）");
                var prop = apiType.GetProperty("instance", flags);
                if (prop == null) { LightLogger.LogWarning("[NovaBootstrap] 属性也没有 ✗ 放弃 ✓"); return; }
                if (prop.GetValue(null) != null) { LightLogger.Log("[NovaBootstrap] NebulaAPI 已初始化过 ✓"); return; }
                prop.SetValue(null, Activator.CreateInstance(implType));
                LightLogger.Log("[NovaBootstrap] NebulaAPI.instance 已创建（属性路径 ✓）");
                return;
            }

            if (field.GetValue(null) != null) { LightLogger.Log("[NovaBootstrap] NebulaAPI 已初始化过，跳过 ✓"); return; }

            field.SetValue(null, Activator.CreateInstance(implType));
            LightLogger.Log("[NovaBootstrap] NebulaAPI.instance 已创建 ✓（字段路径 ✓ GUI 控件引擎就位 ✓）");
        }
        catch (Exception ex)
        {
            LightLogger.LogError("[NovaBootstrap.InitNebulaApi]", ex);
        }
    }

    /// <summary>取 `Nebula.VanillaAsset.PlayerOptionsMenuPrefab`（判断原版素材到底加载上没有 ✓）</summary>
    private static bool IsVanillaAssetReady(Assembly asm)
    {
        try
        {
            var vaType = asm.GetType("Nebula.VanillaAsset");
            var f = vaType?.GetField("PlayerOptionsMenuPrefab",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return f != null && f.GetValue(null) != null;
        }
        catch { return false; }
    }

    /// <summary>
    /// ★ 兜底：自己把 `Nebula.VanillaAsset` 要的两样东西填上 ✓
    /// （`CoLoadAssetOnTitle` 走不通时用 ✓ —— 它第一句就读标题界面独有的 `TwitchManager` ✗）
    /// </summary>
    private static void TryFillVanillaAsset(Assembly asm)
    {
        try
        {
            var vaType = asm.GetType("Nebula.VanillaAsset");
            if (vaType == null) return;
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // ① 滚动条用的原版预制体 ✓
            var pf = vaType.GetField("PlayerOptionsMenuPrefab", flags);
            if (pf != null && pf.GetValue(null) == null)
            {
                UnityEngine.Object? found = null;

                // 先按 Nova 的名字找资源 ✓
                try
                {
                    var uh = asm.GetType("Nebula.Utilities.UnityHelper");
                    var m = uh?.GetMethod("FindAsset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null)
                    {
                        var gm = m.MakeGenericMethod(typeof(PlayerCustomizationMenu));
                        found = gm.Invoke(null, new object?[] { "LobbyPlayerCustomizationMenu" }) as UnityEngine.Object;
                    }
                }
                catch { }

                // 找不到就拿大厅里**活着的那个实例** ✓（结构一样 ✓）
                if (found == null)
                {
                    try { found = UnityEngine.Object.FindObjectOfType<PlayerCustomizationMenu>(); } catch { }
                }

                if (found != null)
                {
                    pf.SetValue(null, found);
                    LightLogger.Log($"[NovaBootstrap] 兜底：PlayerOptionsMenuPrefab ← {found.name} ✓");
                }
            }

            // ② 文字模板 ✓（用我们工程自己的 ✓ —— AGENTS §6 的 `VanillaAsset.GetStandardTextPrefab()` ✓）
            var sf = vaType.GetField("StandardTextPrefab", flags);
            if (sf != null && sf.GetValue(null) == null)
            {
                try
                {
                    var tmp = Light.UI.Window.VanillaAsset.GetStandardTextPrefab();
                    if (tmp != null)
                    {
                        sf.SetValue(null, tmp);
                        LightLogger.Log("[NovaBootstrap] 兜底：StandardTextPrefab ← 工程自带的标准文本预制体 ✓");
                    }
                }
                catch (Exception ex) { LightLogger.LogWarning($"[NovaBootstrap] 取标准文本预制体失败：{ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            LightLogger.LogWarning($"[NovaBootstrap.TryFillVanillaAsset] {ex.Message}");
        }
    }
}
