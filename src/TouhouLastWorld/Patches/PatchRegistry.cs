using HarmonyLib;

namespace TouhouLastWorld.Patches;

public static class PatchRegistry
{
    public static void ApplyAll(Harmony harmony)
    {
        harmony.PatchAll(typeof(NetworkSystemPatch));
        harmony.PatchAll(typeof(GGPublishLoaderPatch));
        harmony.PatchAll(typeof(AssetLoaderPatch));
        harmony.PatchAll(typeof(HomeBehaviourPatch));
        harmony.PatchAll(typeof(ItemHeaderBehaviourPatch));
        harmony.PatchAll(typeof(GachaBehaviorPatch));
        harmony.PatchAll(typeof(GachaPanelPatch));
    }
}
