using HarmonyLib;
using UnityEngine;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(MissionBehavior))]
public static class MissionBehaviorPatch
{
    [HarmonyPatch(nameof(MissionBehavior.Start))]
    [HarmonyPrefix]
    public static void Start_Prefix(MissionBehavior __instance)
    {
        if (__instance.m_battlePassPanel != null || __instance.missionScrollView == null)
            return;

        // 旧任务场景只有普通任务列表。原生切页仍会关闭战令面板并检查待领奖任务。
        __instance.m_missionScrollView = __instance.missionScrollView.gameObject;
        var unavailable = new GameObject("UnavailableBattlePassPanel");
        unavailable.SetActive(false);
        unavailable.transform.SetParent(__instance.transform, false);
        __instance.m_battlePassPanel = unavailable.AddComponent<BattlePassBehaviour>();
        __instance.m_battlePassPanel.m_msntags = new();

        var firstTab = MissionBehavior.firstTab;
        if (firstTab == MissionBehavior.c_menu_battle_pass_reward || firstTab == MissionBehavior.c_menu_battle_pass_mission)
            MissionBehavior.firstTab = 0;
    }

    [HarmonyPatch(nameof(MissionBehavior.UpdateBattlePassMissionBadge))]
    [HarmonyPrefix]
    public static bool UpdateBattlePassMissionBadge_Prefix(MissionBehavior __instance)
        => __instance.m_battlePassMissionToggle != null;
}
