using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadStatUnlock(UnitLimitbreakStrongFragrantWoodRequest? request)
    {
        if (request == null) return null;
        var result = new RequestDraft(request.user_unit_id, request.target_power_type);
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        var limits = unit?.strong_fragrant_wood_limit_info;
        var master = MasterDB.This();
        if (unit == null || limits == null || master?.Setting == null) return result;
        var current = request.target_power_type switch
        {
            1 => limits.type_1,
            2 => limits.type_2,
            3 => limits.type_3,
            4 => limits.type_4,
            5 => limits.type_5,
            6 => limits.type_6,
            _ => -1
        };
        if (current < 0 || current == int.MaxValue) return result;
        var next = master.UnitSFWLimitbreak?.ToList()?.ToArray().FirstOrDefault(row => row.level_limitbreak == current + 1);
        if (next == null) return result;
        long maximum = request.target_power_type switch
        {
            1 => (long)unit.strong_fragrant_wood_spec_actualize_limit_life_point * master.Setting.LIFE_POINT_POWER_CALC_VALUE,
            2 => unit.strong_fragrant_wood_spec_actualize_limit_yang_attack,
            3 => unit.strong_fragrant_wood_spec_actualize_limit_yang_defense,
            4 => unit.strong_fragrant_wood_spec_actualize_limit_yin_attack,
            5 => unit.strong_fragrant_wood_spec_actualize_limit_yin_defense,
            6 => unit.strong_fragrant_wood_spec_actualize_limit_speed,
            _ => -1
        };
        return result with
        {
            StatUnlock = new(current, next.level_limitbreak, next.consume_strong_censer,
            maximum is >= 0 and <= int.MaxValue ? (int)maximum : -1, master.Setting.STRONG_FRAGRANT_WOOD_LIMITBREAK_LOCK == 0)
        };
    }

    private static RequestDraft? ReadExtraStrengthening(UnitExtraSpecActualizeRequest? request)
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
        return new RequestDraft(request.user_unit_id, StatMaterials: entries);
    }

    private static RequestDraft? ReadStrengthening(UnitSpecActualizeRequest? request)
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
        return new RequestDraft(request.user_unit_id, StatMaterials: entries);
    }
}
