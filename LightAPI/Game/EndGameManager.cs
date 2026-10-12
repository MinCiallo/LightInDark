using System;
using System.Linq;
using LightInDark.Core;
using LightInDark.Events;
using LightInDark.RPCs;

namespace LightInDark.Game;

/// <summary>Host-only, once-per-round ending with a result replicated to every client.</summary>
public static class EndGameManager
{
    public static GameEndReason CurrentReason { get; private set; } = GameEndReason.None;
    public static string CustomWinnerCode { get; private set; }
    public static VictoryResult Result { get; private set; }
    public static bool IsEnding { get; private set; }
    public static bool IsGameActive { get; private set; }
    public static event Action<VictoryResult> ResultReceived;
    public static Func<bool> AutomaticEndBlocked { get; set; }
    private static bool _forceEndRequested;

    public static void OnNewGameStart()
    {
        IsGameActive = true;
        IsEnding = false;
        _forceEndRequested = false;
        CurrentReason = GameEndReason.None;
        CustomWinnerCode = null;
        Result = null;
    }
    public static void OnGameEnded() => IsGameActive = false;
    public static void Reset() { OnNewGameStart(); IsGameActive = false; }

    public static void TryEndGame(GameEndReason reason)
    {
        var context = VictoryRuntime.Capture(reason);
        var result = EndGameReasonHelper.IsCustomWin(reason)
            ? VictoryRules.ForTeam(context, CustomWinnerCode ?? "", reason)
            : VictoryRules.EvaluateStandard(context);
        if (result != null) TryEndGame(result, reason == GameEndReason.Invalid);
    }

    /// <summary>force bypasses mode/debug gates. Returns true when the host sent the end.</summary>
    public static bool TryEndGame(VictoryResult result, bool force = false)
    {
        var client = AmongUsClient.Instance;
        var vanilla = global::GameManager.Instance;
        if (result == null || client == null || !client.AmHost || vanilla == null
            || client.GameState != InnerNet.InnerNetClient.GameStates.Started
            || !IsGameActive || IsEnding || Result != null) return false;
        if (!force && (!GameModeManager.AllowAutomaticEnd || (AutomaticEndBlocked?.Invoke() ?? false))) return false;
        try
        {
            var context = VictoryRuntime.Capture(result.Reason);
            result = VictoryRuntime.FinalizeWinners(context, VictoryRules.Normalize(context, result));
            var ev = new GameTryEndEvent
            {
                CrewmatesWin = result.TeamCode == Configuration.RoleCategory.Crewmate.ToString(),
                ImpostorsWin = result.TeamCode == Configuration.RoleCategory.Impostor.ToString(),
                Reason = result.Reason.ToString()
            };
            EventSystem.RunEvent(ev);
            if (!force && ev.IsCanceled) return false;
            IsEnding = true;
            _forceEndRequested = force;
            RpcDefinitions.SyncVictory(result.Reason, result.TeamCode, result.WinnerIds.ToArray());
            vanilla.RpcEndGame(ToVanillaReason(result.Reason), false);
            IsGameActive = false;
            return true;
        }
        catch (Exception ex)
        {
            Result = null;
            CurrentReason = GameEndReason.None;
            CustomWinnerCode = null;
            LightLogger.LogError("[EndGameManager.TryEndGame]", ex);
            return false;
        }
        finally { IsEnding = false; _forceEndRequested = false; }
    }

    internal static void ApplyResult(GameEndReason reason, string teamCode, byte[] winners)
    {
        if (Result != null) return;
        Result = new VictoryResult(reason, teamCode, winners);
        CurrentReason = reason;
        CustomWinnerCode = EndGameReasonHelper.IsCustomWin(reason) ? teamCode : null;
        if (ResultReceived == null) return;
        foreach (Action<VictoryResult> handler in ResultReceived.GetInvocationList())
        {
            try { handler(Result); }
            catch (Exception ex) { LightLogger.LogError("[EndGameManager] ResultReceived", ex); }
        }
    }
    public static void MarkInvalid() => CurrentReason = GameEndReason.Invalid;
    public static void MarkCustomWin(string winnerCode) => CustomWinnerCode = winnerCode;
    public static bool ConsumeForceEnd()
    {
        if (!_forceEndRequested) return false;
        _forceEndRequested = false;
        return true;
    }
    public static GameEndReason GetCurrentReason() => CurrentReason;
    public static GameEndReason FromVanillaReason(GameOverReason reason) => reason switch
    {
        GameOverReason.CrewmatesByTask => GameEndReason.CrewmatesByTasks,
        GameOverReason.ImpostorsBySabotage => GameEndReason.ImpostorsBySabotage,
        GameOverReason.ImpostorsByVote => GameEndReason.ImpostorsByVote,
        GameOverReason.ImpostorsByKill => GameEndReason.ImpostorsByKill,
        GameOverReason.ImpostorDisconnect => GameEndReason.CrewmatesByDisconnect,
        GameOverReason.CrewmateDisconnect => GameEndReason.ImpostorsByDisconnect,
        _ => GameEndReason.CrewmatesByVote
    };
    private static GameOverReason ToVanillaReason(GameEndReason reason) => reason switch
    {
        GameEndReason.CrewmatesByTasks => GameOverReason.CrewmatesByTask,
        GameEndReason.ImpostorsBySabotage => GameOverReason.ImpostorsBySabotage,
        GameEndReason.ImpostorsByVote => GameOverReason.ImpostorsByVote,
        GameEndReason.ImpostorsByKill or GameEndReason.ImpostorsByDisconnect => GameOverReason.ImpostorsByKill,
        _ => GameOverReason.CrewmatesByVote
    };
}
