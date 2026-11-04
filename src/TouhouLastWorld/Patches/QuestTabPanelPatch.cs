using HarmonyLib;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(QuestTabPanel))]
public static class QuestTabPanelPatch
{
    [HarmonyPatch(nameof(QuestTabPanel.OnStart))]
    [HarmonyPrefix]
    public static bool OnStart_Prefix(QuestTabPanel __instance, QuestBehavior qstbeh, int events, int special)
    {
        if (__instance.yukkuriToggle != null)
            return true;

        // 旧探索场景只提供活动和特殊标签，保留其序列化的标签图案。
        __instance.m_qstbeh = qstbeh;
        __instance.SetupDifficultyButtonTint();
        __instance.eventToggle.SetActive(events > 0);
        __instance.specialToggle.SetActive(special > 0);
        if (events <= 0)
            __instance.specialToggle.transform.position = __instance.eventToggle.transform.position;
        return false;
    }

}
