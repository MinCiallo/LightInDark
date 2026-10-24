using Epic.OnlineServices;
using LightInDark;
using LightInDark.Core;
using LightInDark.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Color = UnityEngine.Color;

namespace Light.Patches;

[HarmonyPatch(typeof(GameData),nameof(GameData.ShowNotification))]
public class PlayerLeftPatch_2 // 哪个猪占用了这个名字。
{
    [HarmonyPrefix]
    public static bool pre(string playerName, DisconnectReasons reason)
    {
        LightPlayer p;
        LightPlayer.TryGetPlayer(x=>x.Name==playerName,out p);
        Color playerColor;
        if (p != null)
        {
            playerColor = p.PlayerColor.ToUnityColor();
        }
        else
        {
            playerColor = LightInDark.Color.ModGolden.ToUnityColor();
        }
        
        
        if(reason == DisconnectReasons.ExitGame || reason == DisconnectReasons.IntentionalLeaving)
        {
            // 故意退出。
            show(Language.Translate("notifier.disconnectmsg.exit"), playerName, reason,playerColor);
            return false;
        }
        else if (reason == DisconnectReasons.Sanctions || reason == DisconnectReasons.Hacking || reason == DisconnectReasons.Banned)
        {
            show(Language.Translate("notifier.disconnectmsg.ban"), playerName, reason, playerColor);
            return false;
        }
        else if (reason == DisconnectReasons.Kicked)
        {
            show(Language.Translate("notifier.disconnectmsg.kick"), playerName, reason, playerColor);
            return false;
        }
        else
        {
            show(Language.Translate("notifier.disconnectmsg.disconnect"), playerName, reason, playerColor);
            return false;
        }
    }
    private static void sdn(string text,Color c)
    {
        text = text.Replace("%COLOR%",ColorUtility.ToHtmlStringRGB(c));
        if (DestroyableSingleton<HudManager>._instance) DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
    }
    private static void show(string content,string n, DisconnectReasons r,Color pc)
    {
        LightLogger.Log($"{n} 离开了游戏。({r})");
        sdn(Language.Translate("notifier.disconnectmsg").Replace("%CONTENT%",content).Replace("%N%",n).Replace("%R%",r.ToString()),pc);
    }
}
