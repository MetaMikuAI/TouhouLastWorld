using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadPictureRecycle(PictureRecycleRequest? request)
    {
        if (request == null) return null;
        var ids = request.user_picture_id_list?.ToArray();
        var result = new RequestDraft(Ids: ids);
        var master = MasterDB.This();
        var setting = master?.Setting;
        if (ids is not { Length: > 0 } || setting == null || master?.PictureRecycle == null) return result;
        var entries = new List<LocalPictureRecycleEntry>();
        foreach (var id in ids)
        {
            var picture = User.This()?.picture?.Find(id);
            if (picture == null) return result;
            var status = new PictureStatus(picture.picture_id);
            status.Update(picture);
            var row = master.PictureRecycle.Find(status.GetRecycleId(), picture.spec_actualize, picture.potential_limitbreak);
            if (row == null) return result;
            LocalRecycleGain[] gains =
            [
                new(3, 0, row.gold), new(2, 0, row.phantasm_point), new(5, 0, row.piece),
                new(row.object1_type, row.object1_id, row.object1_value),
                new(row.object2_type, row.object2_id, row.object2_value),
                new(row.object3_type, row.object3_id, row.object3_value)
            ];
            if (gains.Any(gain => gain.Type == 12 && gain.Value > 0 && master.Item?.Find(gain.Id) == null)) return result;
            entries.Add(new(id, picture.picture_id, picture.spec_actualize, picture.potential_limitbreak, gains));
        }
        return result with { PictureRecycle = new(entries.ToArray(), setting.GOLD_MAX, setting.PHANTASM_POINT_MAX, setting.ITEM_MAX) };
    }

    private static RequestDraft? ReadPictureLimitBreak(PicturePotentialLimitbreakRequest? request)
    {
        if (request == null) return null;
        var ids = request.material_user_picture_id_list?.ToArray();
        var result = new RequestDraft(request.user_picture_id, Ids: ids);
        var picture = User.This()?.picture?.Find(request.user_picture_id);
        var table = MasterDB.This()?.PicturePotentialLimitbreak;
        if (picture == null || table == null || ids is not { Length: > 0 } || picture.potential_limitbreak < 0) return result;
        var rare = new PictureStatus(picture.picture_id).GetRare();
        var last = table.Last(rare);
        if (last == null || (long)picture.potential_limitbreak + ids.Length > last.potential_limitbreak) return result;
        var costs = new int[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            var next = table.Find(rare, picture.potential_limitbreak + index + 1);
            if (next == null) return result;
            costs[index] = next.consume_gold;
        }
        return result with { PictureLimitBreak = new(picture.picture_id, picture.potential_limitbreak, last.potential_limitbreak, costs) };
    }
}
