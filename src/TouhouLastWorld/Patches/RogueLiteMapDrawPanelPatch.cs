using HarmonyLib;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(RogueLiteMapDrawPanel))]
public static class RogueLiteMapDrawPanelPatch
{
    [HarmonyPatch(nameof(RogueLiteMapDrawPanel.OnDrawSealOkeyButton))]
    [HarmonyPrefix]
    public static bool ConfirmPrefix(RogueLiteMapDrawPanel __instance)
    {
        var toggles = __instance.m_drawSealToggles;
        var offered = RogueLiteTop.drawnSeals;
        var table = MasterDB.This()?.AbilitySeal;
        if (toggles == null || offered == null || table == null) return false;
        for (var i = 0; i < toggles.Length && i < offered.Count; i++)
        {
            if (!toggles[i].isOn) continue;
            if (!table.HasRerollEffect(offered[i])) return true;
            PopupSystem.This()?.CreateMessage("Effect redraw unavailable.");
            return false;
        }
        return true;
    }
}
