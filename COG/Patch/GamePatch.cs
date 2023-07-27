using COG.Listener;
using COG.Listener.Impl;
using HarmonyLib;
using System.Linq;

namespace COG.Patch;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
class CoBeginPatch
{
    public static void Prefix()
    {
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnCoBegin();
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
class EndGamePatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
    {
        var list = ListenerManager.GetManager().GetListeners().ToList();
        foreach (var listener in list)
        {
            listener.OnGameEnd(__instance, endGameResult);
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
class GameStartManagerStartPatch
{
    public static void Postfix(GameStartManager __instance)
    {
        HostSartPatch.timer = 600f;

        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnGameStart(__instance);
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
internal class MakePublicPatch
{
    public static bool Prefix(GameStartManager __instance)
    {
        bool returnAble = false;
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            if (!listener.OnMakePublic(__instance) && !returnAble)
            {
                returnAble = true;
            }
        }

        if (returnAble) return false;

        return true;
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
class SelectRolesPatch
{
    public static void Prefix()
    {
        var listeners = ListenerManager.GetManager().GetListeners().ToList();
        foreach (var listener in listeners)
        {
            listener.OnSelectRoles();
        }
    }
}

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
class SetEverythingUpPatch
{
    public static void Postfix(EndGameManager __instance)
    {
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnGameEndSetEverythingUp(__instance);
        }
    }
}