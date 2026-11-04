namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadExtraStrengthening(UnitExtraSpecActualizeRequest? request)
    {
        if (request == null) return null;
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        var wood = unit?.strong_fragrant_wood;
        var master = MasterDB.This();
        var lifeFactor = master?.Setting?.LIFE_POINT_POWER_CALC_VALUE ?? 0;
        var entries = request.material_item_list?.ToArray().Select(entry =>
        {
            var item = master?.Item?.Find(entry.item_id);
            var stat = item?.type == 9 ? item.effect_type : 0;
            var lifeLimit = unit == null ? -1L : (long)unit.strong_fragrant_wood_spec_actualize_limit_life_point * lifeFactor;
            var values = unit == null || wood == null ? (-1, -1) : stat switch
            {
                1 => (wood.type_1, lifeFactor > 0 && lifeLimit is >= 0 and <= int.MaxValue ? (int)lifeLimit : -1),
                2 => (wood.type_2, unit.strong_fragrant_wood_spec_actualize_limit_yang_attack),
                3 => (wood.type_3, unit.strong_fragrant_wood_spec_actualize_limit_yang_defense),
                4 => (wood.type_4, unit.strong_fragrant_wood_spec_actualize_limit_yin_attack),
                5 => (wood.type_5, unit.strong_fragrant_wood_spec_actualize_limit_yin_defense),
                6 => (wood.type_6, unit.strong_fragrant_wood_spec_actualize_limit_speed),
                _ => (-1, -1)
            };
            return new LocalStatMaterial(entry.item_id, entry.num, stat, 1, values.Item1, values.Item2);
        }).ToArray();
        return new LocalArguments(request.user_unit_id, StatMaterials: entries);
    }

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
