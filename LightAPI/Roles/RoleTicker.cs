using LightInDark.Events;

namespace LightInDark.Roles
{
    internal static class RoleWinWatcher
    {
        public static void OnHudUpdate(GameHudUpdateEvent ev)
        {
            // VictoryUpdatePatch is the single host driver. CheckWin remains supported by VictoryRuntime.
        }
    }

    internal static class RoleNamePainter
    {
        public static void OnHudUpdate(GameHudUpdateEvent ev)
        {
            try
            {
                var local = LightInDark.Game.GameManager.Instance?.LocalPlayer;
                if (local?.Role == null) return;
                RoleTeam.PaintMemberNames(local.Role);
            }
            catch { }
        }
    }
}
