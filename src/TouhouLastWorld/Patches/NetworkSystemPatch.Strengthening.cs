namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadStrengthening(UnitSpecActualizeRequest? request)
    {
        if (request == null) return null;
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        var master = MasterDB.This();
        var cost = unit == null ? 0 : master?.UnitReincarnationCorrection?.Find(unit.reincarnation_stage)?.count_correction ?? 0;
        var entries = request.material_item_list?.ToArray().Select(entry =>
        {
            var item = master?.Item?.Find(entry.item_id);
            var stat = item?.type == 4 ? item.effect_type : 0;
            var values = unit == null ? (-1, -1) : stat switch
            {
                1 => (unit.add_life_point, unit.spec_actualize_limit_life_point),
                2 => (unit.add_yang_attack, unit.spec_actualize_limit_yang_attack),
                3 => (unit.add_yang_defense, unit.spec_actualize_limit_yang_defense),
                4 => (unit.add_yin_attack, unit.spec_actualize_limit_yin_attack),
                5 => (unit.add_yin_defense, unit.spec_actualize_limit_yin_defense),
                6 => (unit.add_speed, unit.spec_actualize_limit_speed),
                _ => (-1, -1)
            };
            return new LocalStatMaterial(entry.item_id, entry.num, stat, cost, values.Item1, values.Item2);
        }).ToArray();
        return new LocalArguments(request.user_unit_id, StatMaterials: entries);
    }
}
