using HarmonyLib;

namespace TouhouLastWorld.Patches;

[HarmonyPatch(typeof(global::GachaBehavior))]
public static class GachaBehaviorPatch
{
    [HarmonyPatch(nameof(GachaBehavior.Sort), typeof(bool))]
    [HarmonyPrefix]
    public static void Sort_Prefix(GachaBehavior __instance)
    {
        var records = __instance.gachaRecords;
        for (var index = records.Count - 1; index >= 0; index--)
        {
            if (records[index].id != 10) records.RemoveAt(index);
            else
            {
                records[index].name = "本地验证池";
                records[index].gacha_rate_hidden = 1;
                records[index].gacha_point_hidden = 1;
                records[index].gacha_button_id3 = 0;
            }
        }
    }
}
