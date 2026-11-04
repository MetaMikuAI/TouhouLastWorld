using HarmonyLib;

namespace TouhouLastWorld.Patches;

public static class PatchRegistry
{
    public static void ApplyAll(Harmony harmony)
    {
        harmony.PatchAll(typeof(NetworkSystemPatch));
        harmony.PatchAll(typeof(GGPublishLoaderPatch));
        harmony.PatchAll(typeof(AssetLoaderPatch));
        harmony.PatchAll(typeof(AssetLoaderSettingsPatch));
        harmony.PatchAll(typeof(HomeBehaviourPatch));
        harmony.PatchAll(typeof(ItemHeaderBehaviourPatch));
        harmony.PatchAll(typeof(MissionBehaviorPatch));
        harmony.PatchAll(typeof(QuestBehaviorPatch));
        harmony.PatchAll(typeof(QuestTabPanelPatch));
        harmony.PatchAll(typeof(GachaBehaviorPatch));
        harmony.PatchAll(typeof(GachaPanelPatch));
        harmony.PatchAll(typeof(RogueLiteMapDrawPanelPatch));
    }
}
