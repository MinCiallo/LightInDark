using System.Collections.Generic;
using System.Linq;
using LightInDark.Core;
using LightInDark.Game;

namespace LightInDark.Roles
{
    public static class RoleTeam
    {
        public static string CodeOf(Player player) => player?.Role?.Role?.TeamCode;

        public static bool IsTeammate(Player a, Player b)
        {
            var code = CodeOf(a);
            return !string.IsNullOrEmpty(code) && CodeOf(b) == code;
        }

        public static IEnumerable<Player> Members(string teamCode, bool onFieldOnly = false)
        {
            if (string.IsNullOrEmpty(teamCode)) return Enumerable.Empty<Player>();

            var all = LightInDark.Game.GameManager.Instance?.AllPlayers;
            if (all == null) return Enumerable.Empty<Player>();

            return all.Where(p => CodeOf(p) == teamCode && (!onFieldOnly || RoleUtils.IsOnField(p)));
        }

        public static void PaintMemberNames(RuntimeRoleTemplate viewer)
        {
            if (viewer == null) return;

            var code = viewer.Role?.TeamCode;
            if (string.IsNullOrEmpty(code)) return;

            var unity = viewer.Role.Color.ToUnityColor();
            var members = Members(code).ToList();

            // ⚠️ 2026-10-10 用户："游戏内不要打那么多日志，太刷屏了" ✓
            //    这里原来有一条**每帧**的 `[Team]` 诊断（实测一局 1 万多条 ✗）→ 已删除 ✓
            //    要排查队伍/名字颜色时，把下面这行取消注释即可（走 verbose 通道，默认静默 ✓）：
            // LightLogger.LogVerbose($"[Team] code={code} count={members.Count}");

            foreach (var member in members)
            {
                var cosmetics = member.Control?.cosmetics;
                if (cosmetics == null) continue;

                var nameText = cosmetics.nameText;
                if (nameText == null) continue;

                cosmetics.SetNameColor(unity);
                nameText.color = unity;
            }
        }
    }
}
