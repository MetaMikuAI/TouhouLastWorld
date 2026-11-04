using HarmonyLib;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(global::GachaPanel))]
public static class GachaPanelPatch
{
    [HarmonyPatch(nameof(GachaPanel.UpdateGachaButton), typeof(int), typeof(int))]
    [HarmonyPrefix]
    public static void UpdateGachaButton_Prefix(GachaPanel __instance)
    {
        // 旧场景缺少气泡节点，原方法即使没有气泡内容也会访问它。
        if (__instance.m_goldBalloonBonusImage == null)
        {
            var node = new UnityEngine.GameObject("UnavailableGoldBalloon");
            node.transform.SetParent(__instance.gachaBehavior.transform, false);
            __instance.m_goldBalloonBonusImage = node.AddComponent<UnityEngine.UI.Image>();
        }
    }
}
