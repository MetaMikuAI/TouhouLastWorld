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
            var record = records[index];
            if (record.type == GachaMaster.TYPE_VOTIVE) continue;
            if (record.id != 10) records.RemoveAt(index);
            else
            {
                record.name = "本地验证池";
                record.gacha_rate_hidden = 1;
                record.gacha_point_hidden = 1;
                record.gacha_button_id3 = 0;
            }
        }
    }
}
