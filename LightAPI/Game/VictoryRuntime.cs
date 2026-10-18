using System;
using System.Collections.Generic;
using System.Linq;
using LightInDark.Configuration;
using LightInDark.Core;
using LightInDark.Events;

namespace LightInDark.Game;

/// <summary>Unity adapter for the deterministic victory rules.</summary>
public static class VictoryRuntime
{
    public static VictoryContext Capture(GameEndReason reason = GameEndReason.None)
    {
        var players = new List<VictoryPlayer>();
        int tasks = 0, completed = 0;
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.Data == null) continue;
            var data = pc.Data;
            var player = GameManager.Instance.GetPlayer(pc.PlayerId);
            var template = player?.Role?.Role;
            if (template?.IsGhostRole ?? false) template = player.VictoryRole ?? template;
            var category = template?.RoleCategory ?? (data.Role != null && data.Role.IsImpostor
                ? RoleCategory.Impostor : RoleCategory.Crewmate);
            var team = template?.VictoryTeamCode ?? category.ToString();
            players.Add(new VictoryPlayer(pc.PlayerId, category, team, !data.IsDead, data.Disconnected,
                template?.BlocksParityVictory ?? false, template?.NeutralVictory ?? NeutralWinCondition.Manual));
            if (data.Disconnected || !(template?.CountsTasksTowardVictory ?? category == RoleCategory.Crewmate)
                || data.Tasks == null) continue;
            foreach (var task in data.Tasks)
            {
                if (task == null) continue;
                tasks++;
                if (task.Complete) completed++;
            }
        }
        return new VictoryContext(players, reason, tasks > 0 && completed == tasks);
    }

    public static VictoryResult Evaluate(GameEndReason reason = GameEndReason.None)
    {
        var context = Capture(reason);
        var custom = VictoryRules.EvaluateCustom(context);
        if (custom != null) return custom;
        foreach (var p in context.Players.Where(p => !p.Disconnected).OrderBy(p => p.PlayerId))
        {
            var runtime = GameManager.Instance.GetPlayer(p.PlayerId)?.Role;
            try
            {
                if (runtime != null && runtime.CheckWin()) return VictoryRules.ForTeam(context, p.TeamCode);
            }
            catch (Exception ex) { LightLogger.LogError($"[VictoryRuntime] CheckWin({p.PlayerId})", ex); }
        }
        return VictoryRules.EvaluateStandard(context);
    }

    public static VictoryResult FinalizeWinners(VictoryContext context, VictoryResult result)
    {
        if (result.Reason == GameEndReason.Invalid) return result;
        var ids = new List<byte>();
        foreach (var p in context.Players.Where(p => !p.Disconnected))
        {
            var pc = GameManager.Instance.GetPlayer(p.PlayerId)?.Control;
            if (pc == null) continue;
            var reason = result.Reason.ToString();
            bool win = result.IsWinner(p.PlayerId);
            win |= EventTriggers.OnPlayerCheckWin(pc, reason)?.IsWin ?? false;
            win |= EventTriggers.OnPlayerCheckExtraWin(pc, reason)?.IsExtraWin ?? false;
            if (EventTriggers.OnPlayerBlockWin(pc, win, reason)?.IsBlocked ?? false) win = false;
            if (win) ids.Add(p.PlayerId);
        }
        return new VictoryResult(result.Reason, result.TeamCode, ids);
    }
}
