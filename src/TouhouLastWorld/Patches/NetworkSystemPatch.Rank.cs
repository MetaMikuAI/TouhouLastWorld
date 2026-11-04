namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadRankPromotion(UnitRankPromoteRequest? request)
    {
        if (request == null) return null;
        var result = new LocalArguments(request.user_unit_id);
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        if (unit == null || unit.is_rank_max || unit.rank == int.MaxValue) return result;
        var master = MasterDB.This();
        var row = master?.UnitRankPromote?.Find(unit.unit_id, unit.rank);
        var next = master?.UnitRankPromoteGrowth?.Find(unit.rank + 1);
        if (row == null || next == null) return result;
        return result with
        {
            RankPromotion = new(row.unit_id, row.rank, next.rank, next.level_lower_limit, next.consume_gold,
                Enumerable.Range(1, 6).Select(slot => ReadRankSlot(row, slot)!).ToArray())
        };
    }

    private static LocalArguments? ReadRankMaterial(UnitRankPromoteItemSetRequest? request)
    {
        if (request == null) return null;
        var result = new LocalArguments(request.user_unit_id, request.slot_number, request.material_user_picture_id_list?.ToArray());
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        if (unit == null || unit.is_rank_max) return result;
        var row = MasterDB.This()?.UnitRankPromote?.Find(unit.unit_id, unit.rank);
        if (row == null) return result;
        return result with { RankMaterial = ReadRankSlot(row, request.slot_number) };
    }

    private static LocalRankMaterial? ReadRankSlot(UnitRankPromoteRecord row, int slot) => slot switch
        {
            1 => new LocalRankMaterial(row.unit_id, row.rank, row.slot1_object_type, row.slot1_object_id, row.slot1_object_value),
            2 => new LocalRankMaterial(row.unit_id, row.rank, row.slot2_object_type, row.slot2_object_id, row.slot2_object_value),
            3 => new LocalRankMaterial(row.unit_id, row.rank, row.slot3_object_type, row.slot3_object_id, row.slot3_object_value),
            4 => new LocalRankMaterial(row.unit_id, row.rank, row.slot4_object_type, row.slot4_object_id, row.slot4_object_value),
            5 => new LocalRankMaterial(row.unit_id, row.rank, row.slot5_object_type, row.slot5_object_id, row.slot5_object_value),
            6 => new LocalRankMaterial(row.unit_id, row.rank, row.slot6_object_type, row.slot6_object_id, row.slot6_object_value),
            _ => null
        };
}
