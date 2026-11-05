using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadAutomaticRelease(ReincarnationReleaseRequest? request)
    {
        if (request == null) return null;
        var arguments = new RequestDraft(request.user_unit_id, request.unit_reincarnation_release_master_id);
        var row = MasterDB.This()?.UnitReincarnationRelease?.Find(request.unit_reincarnation_release_master_id);
        var unit = User.This()?.unit?.Find(request.user_unit_id);
        if (row == null || unit == null || request.extended_genealogy_id != 0 || request.material_user_picture_id_list?.Length > 0
            || row.slot1_object_type != 0 || row.slot2_object_type != 0 || row.slot3_object_type != 0 || row.slot4_object_type != 0
            || row.slot1_object_value != 0 || row.slot2_object_value != 0 || row.slot3_object_value != 0 || row.slot4_object_value != 0
            || row.GetReleaseFunctionTable()?.Count != 0) return arguments;
        return arguments with
        {
            AutomaticRelease = new(row.id, row.unit_id, row.need_level, row.need_stage,
            row.check_spec_actualize_max > 0, ReincarnationPrepareDialog.IsFullSpecActualized(unit))
        };
    }
}
