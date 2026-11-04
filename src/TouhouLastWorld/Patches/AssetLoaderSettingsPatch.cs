using HarmonyLib;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(AssetLoaderSettings))]
public static class AssetLoaderSettingsPatch
{
    [HarmonyPatch(nameof(AssetLoaderSettings.SetVersionGet))]
    [HarmonyPostfix]
    public static void SetVersionGet_Postfix()
    {
        if (Plugin.Server.HasResourceOverrides)
            AssetLoaderSettings.ReplaceNetWorkPath();
    }
}
