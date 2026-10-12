using System;
using System.Collections.Generic;
using System.Linq;
using LightInDark.Configuration;

namespace LightInDark.Game;

/// <summary>Neutral victory policy, independent of the vanilla role used for movement/abilities.</summary>
public enum NeutralWinCondition { Manual, Parity, LastSurvivor }

/// <summary>Immutable input to victory rules. Disconnected players never count or win.</summary>
public sealed record VictoryPlayer(byte PlayerId, RoleCategory Category, string TeamCode,
    bool IsAlive, bool Disconnected, bool BlocksParity = false,
    NeutralWinCondition NeutralCondition = NeutralWinCondition.Manual);

public sealed class VictoryContext
{
    public IReadOnlyList<VictoryPlayer> Players { get; }
    public IReadOnlyList<VictoryPlayer> Alive => Players.Where(p => p.IsAlive && !p.Disconnected).ToArray();
    public GameEndReason RequestedReason { get; }
    public bool TasksComplete { get; }
    public VictoryContext(IEnumerable<VictoryPlayer> players, GameEndReason requestedReason = GameEndReason.None,
        bool tasksComplete = false)
    {
        Players = Array.AsReadOnly(players.ToArray());
        RequestedReason = requestedReason;
        TasksComplete = tasksComplete;
    }
}

/// <summary>The authoritative result sent before the vanilla end-game message.</summary>
public sealed class VictoryResult
{
    public GameEndReason Reason { get; }
    public string TeamCode { get; }
    public IReadOnlyList<byte> WinnerIds { get; }
    public VictoryResult(GameEndReason reason, string teamCode, IEnumerable<byte> winnerIds)
    {
        Reason = reason;
        TeamCode = teamCode ?? "";
        WinnerIds = Array.AsReadOnly((winnerIds ?? Array.Empty<byte>()).Distinct().OrderBy(x => x).ToArray());
    }
    public bool IsWinner(byte playerId) => WinnerIds.Contains(playerId);
}

/// <summary>Public extension point. Higher priority runs first; null means no victory.</summary>
public static class VictoryRules
{
    private sealed record Rule(string Id, int Priority, Func<VictoryContext, VictoryResult> Evaluate);
    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.Ordinal);

    public static void Register(string id, Func<VictoryContext, VictoryResult> evaluate, int priority = 100)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Rule id is required", nameof(id));
        if (evaluate == null) throw new ArgumentNullException(nameof(evaluate));
        if (!Rules.TryAdd(id, new Rule(id, priority, evaluate)))
            throw new ArgumentException($"Victory rule already registered: {id}", nameof(id));
    }
    public static bool Unregister(string id) => Rules.Remove(id);

    public static VictoryResult Evaluate(VictoryContext context)
        => EvaluateCustom(context) ?? EvaluateStandard(context);

    public static VictoryResult EvaluateCustom(VictoryContext context)
    {
        foreach (var rule in Rules.Values.OrderByDescending(r => r.Priority).ThenBy(r => r.Id).ToArray())
        {
            try
            {
                var result = rule.Evaluate(context);
                if (result != null) return Normalize(context, result);
            }
            catch (Exception ex) { Core.LightLogger.LogError($"[VictoryRules] {rule.Id}", ex); }
        }
        return null;
    }

    public static VictoryResult ForTeam(VictoryContext context, string teamCode,
        GameEndReason reason = GameEndReason.CustomWin)
        => new(reason, teamCode, context.Players.Where(p => !p.Disconnected && p.TeamCode == teamCode)
            .Select(p => p.PlayerId));

    public static VictoryResult Normalize(VictoryContext context, VictoryResult result)
        => new(result.Reason, result.TeamCode, result.WinnerIds.Where(id =>
            context.Players.Any(p => p.PlayerId == id && !p.Disconnected)));

    /// <summary>Tasks and sabotage end immediately. Numeric victories count distinct hostile teams.</summary>
    public static VictoryResult EvaluateStandard(VictoryContext context)
    {
        if (context.RequestedReason == GameEndReason.Invalid)
            return new(GameEndReason.Invalid, "", Array.Empty<byte>());
        if (context.RequestedReason == GameEndReason.ImpostorsBySabotage)
            return CategoryWin(context, RoleCategory.Impostor, GameEndReason.ImpostorsBySabotage);
        if (context.TasksComplete)
            return CategoryWin(context, RoleCategory.Crewmate, GameEndReason.CrewmatesByTasks);

        var alive = context.Alive;
        if (alive.Count == 0) return new(GameEndReason.Invalid, "", Array.Empty<byte>());
        var impostors = alive.Count(p => p.Category == RoleCategory.Impostor);
        foreach (var team in alive.Where(p => p.Category == RoleCategory.Neutral).GroupBy(p => p.TeamCode)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var policy = team.Any(p => p.NeutralCondition == NeutralWinCondition.Parity) ? NeutralWinCondition.Parity
                : team.Any(p => p.NeutralCondition == NeutralWinCondition.LastSurvivor) ? NeutralWinCondition.LastSurvivor
                : NeutralWinCondition.Manual;
            var others = alive.Where(p => p.TeamCode != team.Key).ToArray();
            if (policy == NeutralWinCondition.Manual) continue;
            if (others.Any(p => p.Category == RoleCategory.Impostor || p.BlocksParity)) continue;
            if ((policy == NeutralWinCondition.Parity && team.Count() * 2 >= alive.Count)
                || (policy == NeutralWinCondition.LastSurvivor && others.Length == 0))
                return ForTeam(context, team.Key);
        }
        if (alive.Any(p => p.Category == RoleCategory.Neutral && p.BlocksParity)) return null;
        if (impostors == 0)
            return CategoryWin(context, RoleCategory.Crewmate,
                context.RequestedReason == GameEndReason.CrewmatesByDisconnect
                    ? GameEndReason.CrewmatesByDisconnect : GameEndReason.CrewmatesByVote);
        if (impostors * 2 >= alive.Count)
            return CategoryWin(context, RoleCategory.Impostor,
                context.RequestedReason == GameEndReason.ImpostorsByVote
                    ? GameEndReason.ImpostorsByVote : context.RequestedReason == GameEndReason.ImpostorsByDisconnect
                    ? GameEndReason.ImpostorsByDisconnect : GameEndReason.ImpostorsByKill);
        return null;
    }

    private static VictoryResult CategoryWin(VictoryContext context, RoleCategory category, GameEndReason reason)
        => new(reason, category.ToString(), context.Players.Where(p => !p.Disconnected && p.Category == category)
            .Select(p => p.PlayerId));
}
