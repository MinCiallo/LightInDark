using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using TMPro;
using Twitch;
using UnityEngine;

namespace Nebula;

public class VanillaAsset
{
    public class VanillaAudioClip
    {
        private string name;
        private AudioClip? clip = null;
        public AudioClip Clip { get
            {
                if (clip) return clip;
                clip = UnityHelper.FindAsset<AudioClip>(name);
                return clip!;
            } }

        public VanillaAudioClip(string name)
        {
            this.name = name;
        }
    }
    static public Sprite PopUpBackSprite { get; private set; } = null!;
    static public Sprite FullScreenSprite { get; private set; } = null!;
    static public Sprite TextButtonSprite { get; private set; } = null!;
    static public Sprite CloseButtonSprite { get; private set; } = null!;
    // ⚠️⚠️ 2026-10-11 **自愈改造**（我们的移植件 ✓）：
    //   原来是 `static public TMPro.TextMeshPro StandardTextPrefab { get; private set; } = null!;` ✗
    //   → 它只在**标题界面**的 `CoLoadAssetOnTitle()` 里被赋值 ✓，
    //     而那个方法第一句就读 `TwitchManager.Instance` ✗（本版可能取不到 ✓）→ 永远是 null ✗
    //   → 用户实测：`NoSGUIText.Instantiate` 抛 "The Object you want to instantiate is null" ✓
    //   改成**惰性自愈**：谁用它谁触发 `EnsureLoaded()` ✓
    static private TMPro.TextMeshPro? _standardTextPrefab;
    static public TMPro.TextMeshPro StandardTextPrefab
    {
        get { EnsureLoaded(); return _standardTextPrefab!; }
        private set => _standardTextPrefab = value;
    }
    static public VanillaAudioClip HoverClip { get; private set; } = new("UI_Hover");
    static public VanillaAudioClip SelectClip { get; private set; } = new("UI_Select");
    static public VanillaAudioClip HnSTransformClip { get; private set; } = new("HnS_ImpostorScream");
    static public Material StandardMaskedFontMaterial { get {
            if (standardMaskedFontMaterial == null) standardMaskedFontMaterial = UnityHelper.FindAsset<Material>("LiberationSans SDF - BlackOutlineMasked")!;
            return standardMaskedFontMaterial!;
        }
    }
    static public Material OblongMaskedFontMaterial { get { 
            if(oblongMaskedFontMaterial == null) oblongMaskedFontMaterial = UnityHelper.FindAsset<Material>("Brook Atlas Material Masked");
            return oblongMaskedFontMaterial!;
        } }
    
    static private Material? standardMaskedFontMaterial = null;
    static private Material? oblongMaskedFontMaterial = null;

    static private TMP_FontAsset? versionFont = null;
    static public TMP_FontAsset VersionFont
    {
        get
        {
            if (versionFont == null) versionFont = UnityHelper.FindAsset<TMP_FontAsset>("Barlow-Medium SDF");
            return versionFont!;
        }
    }

    static private TMP_FontAsset? preSpawnFont = null;
    static public TMP_FontAsset PreSpawnFont { get
        {
            if(preSpawnFont==null) preSpawnFont = UnityHelper.FindAsset<TMP_FontAsset>("DIN_Pro_Bold_700 SDF")!;
            return preSpawnFont;
        }
    }

    static private TMP_FontAsset? brookFont = null;
    static public TMP_FontAsset BrookFont
    {
        get
        {
            if (brookFont == null) brookFont = UnityHelper.FindAsset<TMP_FontAsset>("Brook SDF")!;
            return brookFont;
        }
    }

    // ⚠️⚠️ 2026-10-11 **自愈改造**（同上 ✓）：
    //   `GenerateScroller` 里 `PlayerOptionsMenuPrefab.transform.GetChild(4).FindChild("UI_ScrollbarTrack")` ✗
    //   → 这个 prefab 是 null 时就在那一行 NRE ✓（用户堆栈实证 ✓）
    //   同样改成惰性自愈 ✓
    static private PlayerCustomizationMenu? _playerOptionsMenuPrefab;
    static public PlayerCustomizationMenu PlayerOptionsMenuPrefab
    {
        get { EnsureLoaded(); return _playerOptionsMenuPrefab!; }
        private set => _playerOptionsMenuPrefab = value;
    }

    /// <summary>
    /// ★★ 2026-10-11：**原版素材的自愈加载**（我们的移植件 ✓）
    ///
    /// 为什么需要：Nova 原本只在 `CoLoadAssetOnTitle()` 里加载这些素材 ✓，
    /// 而它第一句是 `TwitchManager.Instance.transform.GetChild(0)` ✗ —— 本版取不到 → 那条路整个废掉 ✗
    /// → `StandardTextPrefab` / `PlayerOptionsMenuPrefab` 永远是 null ✓
    /// → 用户实测两条 NRE 都是它 ✓（滚动条那条 / 文字那条 ✓）
    ///
    /// 现在改成"**用的时候自己补**" ✓，并且按可靠性从高到低逐个试 ✓，每一步都打日志 ✓（便于定位 ✓）。
    /// </summary>
    static public void EnsureLoaded()
    {
        // ── ① 滚动条用的那个原版菜单 prefab ✓ ──
        if (_playerOptionsMenuPrefab == null)
        {
            // (a) 按资源名找 ✓（Nova 原本的写法 ✓）
            try
            {
                var found = UnityHelper.FindAsset<PlayerCustomizationMenu>("LobbyPlayerCustomizationMenu");
                if (found != null) { PlayerOptionsMenuPrefab = found; LightInDark.Core.LightLogger.Log($"原版素材：PlayerOptionsMenuPrefab ← FindAsset(\"LobbyPlayerCustomizationMenu\") ✓"); }
            }
            catch (Exception ex) { LightInDark.Core.LightLogger.Log($"原版素材：FindAsset 失败 {ex.Message}"); }

            // (b) 兜底：拿**大厅里活着的那个实例** ✓（结构一样 ✓，Instantiate 它照样有 UI_ScrollbarTrack ✓）
            if (_playerOptionsMenuPrefab == null)
            {
                try
                {
                    var live = GameObject.FindObjectOfType<PlayerCustomizationMenu>();
                    if (live != null) { PlayerOptionsMenuPrefab = live; LightInDark.Core.LightLogger.Log($"原版素材：PlayerOptionsMenuPrefab ← 场景实例 {live.name} ✓"); }
                }
                catch (Exception ex) { LightInDark.Core.LightLogger.Log($"原版素材：找场景实例失败 {ex.Message}"); }
            }

            // (c) 校验滚动条那条子路径真的在 ✓（免得下次还是同一行 NRE ✗）
            if (_playerOptionsMenuPrefab != null)
            {
                try
                {
                    var track = _playerOptionsMenuPrefab.transform.GetChild(4).FindChild("UI_ScrollbarTrack");
                    LightInDark.Core.LightLogger.Log(track != null
                        ? "原版素材：滚动条路径 UI_ScrollbarTrack ✓ 存在 ✓"
                        : "原版素材：⚠️ 这个对象里没有 UI_ScrollbarTrack ✗（还是会 NRE ✗，要换素材源）");
                }
                catch (Exception ex) { LightInDark.Core.LightLogger.Log($"原版素材：校验滚动条路径失败 {ex.Message}"); }
            }
            else LightInDark.Core.LightLogger.Log("原版素材：PlayerOptionsMenuPrefab 仍为 null ✗");
        }

        // ── ② 文字模板 ✓ ──
        if (_standardTextPrefab == null)
        {
            System.Func<TMPro.TextMeshPro?>[] sources =
            {
                // (a) Nova 原本的来源：标题界面的 Twitch 弹窗 ✓
                () => { try { return TwitchManager.Instance != null ? TwitchManager.Instance.transform.GetChild(0).GetChild(1).GetComponent<TMPro.TextMeshPro>() : null; } catch { return null; } },
                // (b) 主菜单里任意一个 TMP ✓
                () => { try { var m = GameObject.FindObjectOfType<MainMenuManager>(); return m != null ? m.gameObject.GetComponentsInChildren<TMPro.TextMeshPro>(true).FirstOrDefault() : null; } catch { return null; } },
                // (c) Hud 里任意一个 TMP ✓
                () => { try { var h = HudManager.Instance; return h != null ? h.gameObject.GetComponentsInChildren<TMPro.TextMeshPro>(true).FirstOrDefault() : null; } catch { return null; } },
            };

            foreach (var src in sources)
            {
                TMPro.TextMeshPro? t = null;
                try { t = src(); } catch { }
                if (t == null) continue;
                try
                {
                    var clone = GameObject.Instantiate(t, null);
                    clone.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    GameObject.DontDestroyOnLoad(clone.gameObject);

                    // ★★ 2026-10-11 **必须把模板的排版属性锁死** ✓✓
                    //   （用户实测："字糊的不像人字" ✗ —— 根因是我兜底抓的是
                    //     `WarningText`（警告大字 ✓，`enableAutoSizing = true` ✗）
                    //     → TMP 会**无视 fontSize 自己缩放** ✓（AGENTS §12.2 记过这个坑 ✗）
                    //     → 字被拉得又大又糊 ✓）
                    //   规矩（AGENTS §12.1/§12.2/§12.3）：**默认粗体 ✓、
                    //   关 autoSizing 并锁死 min/max ✓、会变的字用 Overflow 不裁 ✓**
                    clone.enableAutoSizing = false;
                    clone.fontSize = 2.0f;
                    clone.fontSizeMin = 2.0f;
                    clone.fontSizeMax = 2.0f;
                    clone.fontStyle = FontStyles.Bold;
                    clone.color = Color.white;
                    clone.overflowMode = TMPro.TextOverflowModes.Overflow;
                    clone.enableWordWrapping = false;
                    clone.alignment = TMPro.TextAlignmentOptions.Left;
                    try { clone.transform.localScale = Vector3.one; } catch { }

                    StandardTextPrefab = clone;
                    LightInDark.Core.LightLogger.Log($"原版素材：StandardTextPrefab ← {t.name} 的克隆 ✓（字体 {clone.font?.name} ✓ 字号已锁 {clone.fontSize} ✓ 粗体 ✓）");
                    break;
                }
                catch (Exception ex) { LightInDark.Core.LightLogger.Log($"原版素材：克隆 TMP 失败 {ex.Message}"); }
            }

            if (_standardTextPrefab == null) LightInDark.Core.LightLogger.Log("原版素材：StandardTextPrefab 仍为 null ✗（文字画不出来 ✓）");
        }
    }

    static public readonly ShipStatus[] MapAsset = new ShipStatus[6];

    public record MapInfo(MapNames Name, Sprite MapIcon, Sprite MapImage, Sprite NameImage);
    public static MapInfo[] MapImages = null!;
    public static Sprite TitleBackgroundSprite = null!;

    static public Material GetMapMaterial()
    {
        var mat = GetMinimapRenderer(0).material;
        return mat;
    }
    static public Color MapBlue => new Color(0.05f, 0.2f, 1f, 1f);
    static public SpriteRenderer GetMinimapRenderer(byte mapId) => MapAsset[mapId].MapPrefab.ColorControl.gameObject.GetComponent<SpriteRenderer>();
    static public Vector2 GetMapCenter(byte mapId) => MapAsset[mapId].MapPrefab.transform.GetChild(5).localPosition;
    static public float GetMapScale(byte mapId) => VanillaAsset.MapAsset[mapId].MapScale;
    static public VVector2 ConvertToMinimapPos(VVector2 pos,VVector2 center, float scale)=> (pos / scale) + center;
    static public VVector2 ConvertToMinimapPos(VVector2 pos, byte mapId) => ConvertToMinimapPos(pos, GetMapCenter(mapId), GetMapScale(mapId));
    static public VVector2 ConvertFromMinimapPosToWorld(VVector2 minimapPos, VVector2 center, float scale) => (minimapPos - center) * scale;
    static public VVector2 ConvertFromMinimapPosToWorld(VVector2 minimapPos, byte mapId) => ConvertFromMinimapPosToWorld(minimapPos, GetMapCenter(mapId), GetMapScale(mapId));

    static public void LoadAssetAtInitialize()
    {   
        //PlayerOptionsMenuPrefab = UnityHelper.FindAsset<PlayerCustomizationMenu>("LobbyPlayerCustomizationMenu")!;
    }

    public static void PlaySelectSE() => AmongUsLLImpl.SoundManagerInstance.PlaySound(SelectClip.Clip, false, 0.8f);
    public static void PlayHoverSE() => AmongUsLLImpl.SoundManagerInstance.PlaySound(HoverClip.Clip, false, 0.8f);

    static private Dictionary<string, UnityEngine.Object> VanillaAudioClips = [];
    static public AudioClip? GetAudioClip(string name)
    {
        if(VanillaAudioClips.TryGetValue(name, out var found))
        {
            return found.TryCast<AudioClip>();
        }
        return null;
    }

    static public string[] GetAudioKeys() => VanillaAudioClips.Keys.ToArray();

    static public void LoadOnMainMenu(MainMenuManager mainMenu)
    {
        if (VanillaAsset.MapImages == null) VanillaAsset.MapImages = mainMenu.createGameScreen.mapPicker.AllMapIcons.ToArray().Select(m => new VanillaAsset.MapInfo(m.Name, m.MapIcon.MarkDontUnload(), m.MapImage.MarkDontUnload(), m.NameImage.MarkDontUnload())).ToArray();
        if (VanillaAsset.TitleBackgroundSprite == null) VanillaAsset.TitleBackgroundSprite = mainMenu.transform.GetChild(5).GetChild(1).GetChild(2).GetComponent<SpriteRenderer>().sprite.MarkDontUnload();
    }
    static public IEnumerator CoLoadAssetOnTitle()
    {
        PlayerOptionsMenuPrefab = UnityHelper.FindAsset<PlayerCustomizationMenu>("LobbyPlayerCustomizationMenu")!;

        var twitchPopUp = TwitchManager.Instance.transform.GetChild(0);
        PopUpBackSprite = twitchPopUp.GetChild(3).GetComponent<SpriteRenderer>().sprite;
        TextButtonSprite = twitchPopUp.GetChild(2).GetComponent<SpriteRenderer>().sprite;
        FullScreenSprite = twitchPopUp.GetChild(0).GetComponent<SpriteRenderer>().sprite;
        CloseButtonSprite = UnityHelper.FindAsset<Sprite>("closeButton")!;
        

        StandardTextPrefab = GameObject.Instantiate(twitchPopUp.GetChild(1).GetComponent<TMPro.TextMeshPro>(),null);
        StandardTextPrefab.gameObject.hideFlags = HideFlags.HideAndDontSave;
        GameObject.Destroy(StandardTextPrefab.spriteAnimator);
        GameObject.DontDestroyOnLoad(StandardTextPrefab.gameObject);

        while (AmongUsClient.Instance == null) yield return null;


        //AsyncOperationHandle<GameObject> handle;
        //AmongUsClient.Instance.ShipPrefabs[2].RuntimeKey;
        //UnityEngine.AddressableAssets.Addressables.LoadAssetAsync(AmongUsClient.Instance.ShipPrefabs[0].RuntimeKey, null, false, false);
        for (int i = 0; i < MapAsset.Length; i++) {
            if (i == 3) continue;
            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(AmongUsClient.Instance.ShipPrefabs[i].RuntimeKey);
            yield return handle;
            MapAsset[i] = handle.Result.GetComponent<ShipStatus>();
        }

        foreach(var audio in UnityEngine.Object.FindObjectsOfTypeAll(Il2CppType.Of<AudioClip>()))
        {
            if (audio.name != null) VanillaAudioClips[audio.name] = audio;
        }

        //マップの部屋名フォントを読み込んだうえで再度フォントを適用
        Language.ReflectFallBackFont();

        //Polus
        //handle = AmongUsClient.Instance.ShipPrefabs[2].InstantiateAsync(null, false);
        //yield return handle;
        //var polus = handle.Result.GetComponent<PolusShipStatus>();


        /*
        //Airship
        handle = AmongUsClient.Instance.ShipPrefabs[4].InstantiateAsync(null, false);
        yield return handle;
        */

        yield break;
    }

    static public Scroller GenerateScroller(VVector2 size, Transform transform, VVector3 scrollBarLocalPos, Transform target, FloatRange bounds, float scrollerHeight)
    {
        var barBack = GameObject.Instantiate(PlayerOptionsMenuPrefab.transform.GetChild(4).FindChild("UI_ScrollbarTrack").gameObject, transform);
        var bar = GameObject.Instantiate(PlayerOptionsMenuPrefab.transform.GetChild(4).FindChild("UI_Scrollbar").gameObject, transform);
        barBack.transform.localPosition = scrollBarLocalPos + new VVector3(0.12f, 0f, 0f);
        bar.transform.localPosition = scrollBarLocalPos;

        var scrollBar = bar.GetComponent<Scrollbar>();

        var scroller = UnityHelper.CreateObject<Scroller>("Scroller", transform, new(0, 0, 5));
        var collider = scroller.gameObject.AddComponent<BoxCollider2D>();
        collider.size = size;

        scrollBar.parent = scroller;
        scrollBar.graphic = bar.GetComponent<SpriteRenderer>();
        scrollBar.trackGraphic = barBack.GetComponent<SpriteRenderer>();
        scrollBar.trackGraphic.size = new Vector2(scrollBar.trackGraphic.size.x, scrollerHeight);

        var ratio = scrollerHeight / 3.88f;

        scroller.Inner = target;
        scroller.SetBounds(bounds, null);
        scroller.allowY = true;
        scroller.allowX = false;
        scroller.ScrollbarYBounds = new FloatRange(-1.8f * ratio + scrollBarLocalPos.y + 0.4f, 1.8f * ratio + scrollBarLocalPos.y - 0.4f);
        scroller.ScrollbarY = scrollBar;
        scroller.active = true;

        scroller.name = ClickMaskName;
        var instanceId = scroller.gameObject.GetInstanceIdFast();
        var buttonManager = PassiveButtonManager.Instance;
        var predicate = (Il2CppSystem.Predicate<PassiveUiElement>)(b => b != null && b && b.isActiveAndEnabled && b.name == ClickMaskName);
        scroller.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += () =>
        {
            var found = buttonManager.Buttons.Find(predicate);
            if (found.AsBoolFast() && found.gameObject.GetInstanceIdFast() == instanceId)
            {
                scroller.MouseMustBeOverToScroll = false;
                collider.size = size;
            }
            else
            {
                scroller.MouseMustBeOverToScroll = true;
                collider.size = Vector2.zero;
                scroller.mouseOver = false;
            }
        };

        //scroller.Colliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider2D>(new Collider2D[] { hitBox });

        scroller.ScrollToTop();

        return scroller;
    }

    private const string ClickMaskName = "NebulaScroller";

    private static Material? highlightMaterial = null;
    public static Material GetHighlightMaterial()
    {
        if (highlightMaterial != null) return new Material(highlightMaterial);
        foreach (var mat in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
        {
            if (mat.name == "HighlightMat")
            {
                highlightMaterial = mat.TryCast<Material>();
                break;
            }
        }
        return new Material(highlightMaterial);
    }

    private static Material? maskMaterial = null;
    public static Material GetMaskingMaterial()
    {
        if (maskMaterial != null) return new Material(maskMaterial);
        foreach (var mat in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
        {
            if (mat.name == "MaskingShader")
            {
                maskMaterial = mat.TryCast<Material>();
                break;
            }
        }
        return new Material(maskMaterial);
    }

    public static PlayerDisplay GetPlayerDisplay(bool withPhysicComponents = false, bool withScaler = false)
    {
        AmongUsClient.Instance.PlayerPrefab.gameObject.SetActive(false);
        var display = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab.gameObject);
        AmongUsClient.Instance.PlayerPrefab.gameObject.SetActive(true);
        
        GameObject.Destroy(display.GetComponent<PlayerControl>());
        GameObject.Destroy(display.GetComponent<PlayerPhysics>());
        if (withScaler) PlayerExtension.SetUpScaler(display.gameObject);
        if (!withPhysicComponents)
        {
            GameObject.Destroy(display.GetComponent<Rigidbody2D>());
            GameObject.Destroy(display.GetComponent<CircleCollider2D>());
            GameObject.Destroy(display.GetComponent<BoxCollider2D>());
            GameObject.Destroy(display.GetComponent<AudioSource>());
            GameObject.Destroy(display.GetComponent<PassiveButton>());
        }
        GameObject.Destroy(display.GetComponent<CustomNetworkTransform>());
        GameObject.Destroy(display.GetComponent<DummyBehaviour>());
        GameObject.Destroy(display.GetComponent<HnSImpostorScreamSfx>());

        display.gameObject.SetActive(true);

        GameObject.Destroy(display.GetComponent<UncertifiedPlayer>());
        display.GetComponentInChildren<NebulaCosmeticsLayer>().RejectZOrdering = true;
        return display.AddComponent<PlayerDisplay>();
    }
}
