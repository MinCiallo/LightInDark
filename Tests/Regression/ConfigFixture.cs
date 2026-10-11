using UnityEngine;
using LightInDark.Configuration;

partial class ConfigFixture
{
    private static ConfigBlock _singleBlock;
    private static ConfigCategory[] _categoryFilter;
    private static Transform _container;
    private static GameObject _page;
    private static GameOptionsMenu _hostMenu, _templates;
    private static readonly List<GameObject> _spawned=new();
    private static readonly Dictionary<ConfigItem,Driver> _drivers=new();
    private static readonly Dictionary<int,ConfigItem> RowMap=new();
    private static int _rowCount;
    private const float RoleBackY=1.05f, BackBtnH=0.38f, HeaderHeight=0.63f, SpacingY=0.45f, StartY=0.713f, RowZ=-2f;
    private static float CurHeaderX=>-2.03f;
    private static float CurRowX=>-1.84f;
    private static float StartYFor(GameOptionsMenu m)=>StartY+0.6f;
    private class Driver : Component {}
    private static void UpdateScrollBounds(GameOptionsMenu host,float start,float last){}
    private static void SnapshotBuiltKeys(){}
    private static void LogRowLayoutOnce(){}
    private static float AddConfigRow(ConfigItem item,float y)
    {
        var go=new GameObject("LightConfigRow_"+item.Key);
        go.transform.SetParent(_container,false);
        go.transform.localPosition=new(CurRowX,y,RowZ);
        _spawned.Add(go); _drivers[item]=go.AddComponent<Driver>(); RowMap[go.GetInstanceID()]=item; _rowCount++;
        return y-SpacingY;
    }
    public static void Setup(ConfigBlock selected)
    {
        _spawned.Clear(); _drivers.Clear(); RowMap.Clear(); _rowCount=0;
        _singleBlock=selected; _categoryFilter=null;
        _page=new GameObject("LightConfigPage"); _container=_page.transform;
        _hostMenu=new GameObject("Host").AddComponent<GameOptionsMenu>(); _templates=_hostMenu;
        var header=new GameObject("LightConfigHeader_"+selected.Key); _spawned.Add(header);
        header.transform.SetParent(_container,false);
        float y=RoleBackY-BackBtnH-0.14f;
        header.transform.localPosition=new(CurHeaderX,y,RowZ); y-=HeaderHeight;
        foreach(var item in selected.Items) if(item.IsVisible) y=AddConfigRow(item,y);
    }
    public static bool Refresh()=>TryIncrementalVisibility();
    public static int Count=>_drivers.Count;
    public static float Y(ConfigItem item)=>_drivers[item].transform.localPosition.y;
    public static float Bottom()=>LastRowY();
}
