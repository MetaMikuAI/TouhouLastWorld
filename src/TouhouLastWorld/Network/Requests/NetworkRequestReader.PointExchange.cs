using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadPointExchange(GachaPointExchangeRequest? request)
    {
        var master = MasterDB.This();
        var row = request == null ? null : master?.GachaPointExchange?.Find(request.exchange_id);
        var current = User.This();
        var time = Applet.GameSystem.This()?.Time;
        if (row == null || current == null || time == null || row.object_value != 1 || row.is_pickup_only != 0) return null;
        LocalIntroPicture? picture = null;
        var capacity = 0;
        if (row.object_type == 9 && master?.Unit?.Find(row.object_id) != null && current.unit != null)
            capacity = master.UnitExpansion?.Find(current.unit.expansion_rank)?.unit_count ?? 0;
        if (row.object_type == 10 && master?.Picture?.Find(row.object_id) != null && current.picture != null)
        {
            capacity = master.PictureExpansion?.Find(current.picture.expansion_rank)?.picture_count ?? 0;
            var template = UserData.UserPicture.CreateUserPicture(row.object_id);
            if (template != null) picture = new(row.object_id, template.parameter1_value, template.parameter2_value,
                template.picture_characteristic_group_master_id);
        }
        return new(PointExchange: new(row.id, row.gacha_point_id, row.need_point, row.exchange_limit_count,
            row.object_type, row.object_id, capacity,
            (string.IsNullOrWhiteSpace(row.start_at) && string.IsNullOrWhiteSpace(row.end_at))
                || time.IsActiveDate(row.start_at, row.end_at), picture));
    }
}
