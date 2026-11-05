using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadUnitRecycle(UnitRecycleRequest? request)
    {
        if (request == null) return null;
        var ids = request.user_unit_id_list?.ToArray();
        var result = new RequestDraft(Ids: ids);
        var master = MasterDB.This();
        var setting = master?.Setting;
        if (ids is not { Length: > 0 } || setting == null) return result;
        var entries = new List<LocalUnitRecycleEntry>();
        foreach (var id in ids)
        {
            var unit = User.This()?.unit?.Find(id);
            if (unit == null) return result;
            var definition = master?.Unit?.Find(unit.unit_id);
            if (definition == null) return result;
            var row = master?.UnitRecycle?.Find(definition.recycle_id, unit.rare, unit.level_limitbreak);
            if (row == null) return result;
            LocalRecycleGain[] gains = [new(3, 0, row.gold), new(2, 0, row.phantasm_point), new(5, 0, row.piece),
                new(row.object_type, row.object_id, row.object_value)];
            if (gains.Any(gain => gain.Type == 12 && gain.Value > 0 && master?.Item?.Find(gain.Id) == null)) return result;
            entries.Add(new(id, unit.unit_id, unit.rare, unit.level_limitbreak, gains));
        }
        return result with { UnitRecycle = new(entries.ToArray(), setting.GOLD_MAX, setting.PHANTASM_POINT_MAX, setting.ITEM_MAX) };
    }
}
