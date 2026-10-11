using System;
using System.Collections.Generic;
using System.Linq;
using LightInDark.Roles;

namespace LightInDark.Game;

/// <summary>Public mode contract. Allocators remain supplied by the main plugin or addons.</summary>
public sealed class GameModeDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public bool AllowAutomaticEnd { get; }
    public bool AllowRoleChanges { get; }
    public int MinimumPlayers { get; }
    public Func<IRoleAllocator> CreateAllocator { get; }
    public Action OnStarted { get; init; }
    public Action OnEnded { get; init; }
    public GameModeDefinition(string id, string displayName, Func<IRoleAllocator> createAllocator,
        bool allowAutomaticEnd = true, bool allowRoleChanges = false, int minimumPlayers = 4)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Mode id is required", nameof(id));
        Id = id;
        DisplayName = displayName ?? id;
        CreateAllocator = createAllocator ?? throw new ArgumentNullException(nameof(createAllocator));
        AllowAutomaticEnd = allowAutomaticEnd;
        AllowRoleChanges = allowRoleChanges;
        MinimumPlayers = Math.Clamp(minimumPlayers, 1, 15);
    }
}

public static class GameModeRegistry
{
    public const string Standard = "lid.standard";
    public const string FreePlay = "lid.freeplay";
    private static readonly Dictionary<string, GameModeDefinition> Modes = new(StringComparer.Ordinal);
    public static IReadOnlyCollection<GameModeDefinition> All => Modes.Values
        .OrderBy(m => m.Id == Standard ? 0 : m.Id == FreePlay ? 1 : 2)
        .ThenBy(m => m.Id, StringComparer.Ordinal).ToList().AsReadOnly();
    public static event Action<GameModeDefinition> Registered;
    public static void Register(GameModeDefinition mode)
    {
        if (mode == null) throw new ArgumentNullException(nameof(mode));
        if (!Modes.TryAdd(mode.Id, mode)) throw new ArgumentException($"Mode already registered: {mode.Id}");
        Registered?.Invoke(mode);
    }
    public static GameModeDefinition Get(string id) => id != null && Modes.TryGetValue(id, out var mode) ? mode : null;
}

/// <summary>A mode is frozen before assignment. Lobby settings apply to the next round.</summary>
public static class GameModeManager
{
    public static string CurrentId { get; private set; } = GameModeRegistry.Standard;
    public static GameModeDefinition Current => GameModeRegistry.Get(CurrentId);
    public static bool AllowAutomaticEnd => Current?.AllowAutomaticEnd ?? true;
    public static bool AllowRoleChanges => Current?.AllowRoleChanges ?? false;
    public static bool IsRoundActive { get; private set; }
    public static event Action<string> ModeChanged;

    public static bool Select(string id)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || IsRoundActive || GameModeRegistry.Get(id) == null) return false;
        RPCs.RpcDefinitions.SyncGameMode(id);
        return true;
    }
    internal static bool ApplyRemote(string id)
    {
        if (IsRoundActive || GameModeRegistry.Get(id) == null) return false;
        CurrentId = id;
        EndGameManager.OnNewGameStart();
        ModeChanged?.Invoke(id);
        return true;
    }
    public static void BeginRound()
    {
        if (IsRoundActive) return;
        IsRoundActive = true;
        try { Current?.OnStarted?.Invoke(); }
        catch (Exception ex) { Core.LightLogger.LogError("[GameMode] OnStarted", ex); }
    }
    public static void EndRound()
    {
        bool wasActive = IsRoundActive;
        IsRoundActive = false;
        try { if (wasActive) Current?.OnEnded?.Invoke(); }
        catch (Exception ex) { Core.LightLogger.LogError("[GameMode] OnEnded", ex); }
    }
    public static void Reset()
    {
        EndRound();
        CurrentId = GameModeRegistry.Standard;
    }
}
