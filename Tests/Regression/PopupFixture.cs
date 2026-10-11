using UnityEngine;
using TMPro;

partial class PopupFixture
{
    class PopupStyle { public float MinWidth=0.3f, MinHeight=0.2f, MaxWidth; public Color Fill,Border; }
    private static PopupStyle Style=new();
    private static GameObject _root;
    private static Transform _parent,_sourceParent,_anchor;
    private static TextMeshPro _text;
    private static SpriteRenderer _fill,_frame,_top,_bottom,_left,_right;
    private static Sprite _panelSprite;
    private static bool _canPin,_pinned,_showing,_textSourceIsRowText;
    private static int _updateFrame=-1;
    private static float _boxWidth=1,_boxHeight=0.3f,_lastWrapWidth=-1;
    private static string _currentText="";
    private const string PinHint="";
    private static void ApplyConfig(){}
    private static void RefreshPartSprites(){}
    private static void ApplyRenderSettings(Transform anchor)=>_root.layer=anchor.gameObject.layer;
    private static void ApplyWrapWidth(){}
    private static void FitBox(){}
    private static void LogDiagnostics(Transform anchor){}
    private static Sprite GetWhiteSprite()=>new();
    private static Sprite EnsurePanelSprite()=>new();
    private static void HidePart(SpriteRenderer p){}
    private static SpriteRenderer MakePart(string name,Vector3 pos,Color c,Sprite sprite)
    {
        var go=new GameObject(name); go.transform.SetParent(_root.transform,false); return go.AddComponent<SpriteRenderer>();
    }
    private static TextMeshPro CreateText(Transform parent,Transform anchor)
    {
        var go=new GameObject("Text"); go.transform.SetParent(parent,false); return go.AddComponent<TextMeshPro>();
    }
    private static SpriteRenderer FindReferenceRenderer(Transform anchor)=>anchor?.GetComponent<SpriteRenderer>();
    public static Vector3 WorldPosition=>_root.transform.position;
    public static bool Visible=>_root?.activeSelf ?? false;
    public static string Text=>_text?.text;
    public static Transform Parent=>_parent;
}
