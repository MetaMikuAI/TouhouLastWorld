namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadRankMaterial(UnitRankPromoteItemSetRequest? request)
    {
        if (request == null) return null;
        var result = new LocalArguments(request.user_unit_id, request.slot_number, request.material_user_picture_id_list?.ToArray());
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        if (unit == null || unit.is_rank_max) return result;
        var row = MasterDB.This()?.UnitRankPromote?.Find(unit.unit_id, unit.rank);
        if (row == null) return result;
        var material = request.slot_number switch
        {
            1 => new LocalRankMaterial(row.unit_id, row.rank, row.slot1_object_type, row.slot1_object_id, row.slot1_object_value),
            2 => new LocalRankMaterial(row.unit_id, row.rank, row.slot2_object_type, row.slot2_object_id, row.slot2_object_value),
            3 => new LocalRankMaterial(row.unit_id, row.rank, row.slot3_object_type, row.slot3_object_id, row.slot3_object_value),
            4 => new LocalRankMaterial(row.unit_id, row.rank, row.slot4_object_type, row.slot4_object_id, row.slot4_object_value),
            5 => new LocalRankMaterial(row.unit_id, row.rank, row.slot5_object_type, row.slot5_object_id, row.slot5_object_value),
            6 => new LocalRankMaterial(row.unit_id, row.rank, row.slot6_object_type, row.slot6_object_id, row.slot6_object_value),
            _ => null
        };
        return result with { RankMaterial = material };
    }
}
