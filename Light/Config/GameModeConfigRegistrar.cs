using System;
using System.Linq;
using LightInDark.Configuration;
using LightInDark.Game;
using Light.Roles.Assignment;

namespace Light.Config;

public static class GameModeConfigRegistrar
{
    public const string Key = "lid.gameMode";
    private static ConfigItem _item;
    private static string[] _modeIds;
    public static void Register()
    {
        if (_item != null) return;
        GameModeRegistry.Register(new GameModeDefinition(GameModeRegistry.Standard,
            "标准模式", () => new StandardRoleAllocator()));
        GameModeRegistry.Register(new GameModeDefinition(GameModeRegistry.FreePlay,
            "自由模式", () => new FreePlayRoleAllocator(), false, true, 1));
        var block = new ConfigBlock("lid.gameMode", "游戏模式", ConfigCategory.Mod);
        _item = block.AddConfiguration(Key, GameModeRegistry.All.Select(m => m.DisplayName).ToArray(),
            "游戏模式", "标准模式按配置分配职业并判定胜利；自由模式可切换职业，不会自动结束。下一局生效。");
        RefreshModes();
        GameModeRegistry.Registered += _ => RefreshModes();
        LightInDark.Game.EndGameManager.AutomaticEndBlocked = () => Patches.DebugMode.Enabled;
    }
    private static void RefreshModes()
    {
        _modeIds = GameModeRegistry.All.Select(m => m.Id).ToArray();
        _item.SetSelections(GameModeRegistry.All.Select(m => m.DisplayName).ToArray());
    }
    public static void SelectForRound()
    {
        GameModeManager.Select(SelectedMode.Id);
    }
    public static GameModeDefinition SelectedMode => GameModeRegistry.Get(_modeIds[
        Math.Clamp(_item?.GetInt() ?? 0, 0, _modeIds.Length - 1)]);
}
