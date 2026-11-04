using HarmonyLib;

using static TouhouLastWorld.Patches.HarmonyPrefixFlow;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(global::GGPublishLoader))]
public static class GGPublishLoaderPatch
{
    public const string LocalUrl = "offline://version/check";

    [HarmonyPatch(nameof(GGPublishLoader.GetUrl), typeof(GGLoaderType))]
    [HarmonyPrefix]
    public static bool GetUrl_Prefix(GGLoaderType __0, ref string __result)
    {
        if (__0 != GGLoaderType.CheckVersion)
            return RunOriginal;
        __result = LocalUrl;
        return SkipOriginal;
    }
}
