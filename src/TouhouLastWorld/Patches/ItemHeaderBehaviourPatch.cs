using HarmonyLib;

using static TouhouLastWorld.Patches.HarmonyPrefixFlow;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(global::ItemHeaderBehaviour))]
public static class ItemHeaderBehaviourPatch
{
    [HarmonyPatch(nameof(ItemHeaderBehaviour.SetBackgroundRepeatInfo))]
    [HarmonyPrefix]
    public static bool SetBackgroundRepeatInfo_Prefix(ItemHeaderBehaviour __instance)
        => __instance.backgroundRepeatButton != null ? RunOriginal : SkipOriginal;

    [HarmonyPatch(nameof(ItemHeaderBehaviour.SetBackgroundRepeatVisible), typeof(bool))]
    [HarmonyPrefix]
    public static bool SetBackgroundRepeatVisible_Prefix(ItemHeaderBehaviour __instance)
        => __instance.backgroundRepeatButton != null ? RunOriginal : SkipOriginal;
}
