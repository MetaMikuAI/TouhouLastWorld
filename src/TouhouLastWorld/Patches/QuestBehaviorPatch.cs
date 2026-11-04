using HarmonyLib;
using UnityEngine.UI;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(QuestBehavior))]
public static class QuestBehaviorPatch
{
    private const int PrologueTutorialEpisode = 100100103;

    [HarmonyPatch(nameof(QuestBehavior.Start))]
    [HarmonyPrefix]
    public static void Start_Prefix(QuestBehavior __instance)
    {
        if (__instance.captionPanelBackground == null)
            __instance.captionPanelBackground = __instance.questScrollView?.episodeScrollView?.Find("CaptionPanel")?.gameObject;

        // 序章引导先等待主线页动画，再开放章节按钮。
        if (QuestBehavior.episodeID == 0 && QuestBehavior.tabID != QuestBehavior.TabMain
            && User.This()?.playdata?.GetEpisodeInfo(PrologueTutorialEpisode, PlayData.Difficulty.Normal)?.state == PlayData.State.New)
            QuestBehavior.tabID = QuestBehavior.TabMain;
    }

    [HarmonyPatch(nameof(QuestBehavior.SetEnableTab))]
    [HarmonyPrefix]
    public static bool SetEnableTab_Prefix(QuestBehavior __instance, bool __0)
    {
        if (__instance.yukkuriToggle != null)
            return true;

        foreach (var tab in new[] { __instance.mainToggle, __instance.eventToggle, __instance.dailyToggle, __instance.specialToggle })
        {
            var toggle = tab?.GetComponent<Toggle>();
            if (toggle != null)
                toggle.enabled = __0;
        }
        return false;
    }
}
