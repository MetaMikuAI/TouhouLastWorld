using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadTicketExchange(VotivePictureExchangeRequest? request)
    {
        if (request == null) return null;
        var master = MasterDB.This();
        var row = master?.VotivePictureExchange?.Find(request.votive_picture_exchange_id);
        var ticket = master?.VotivePicture?.Find(request.votive_picture_id);
        var current = User.This();
        var time = Applet.GameSystem.This()?.Time;
        if (row == null || ticket == null || row.votive_picture_id != ticket.id || current == null || time == null
            || master?.Item?.Find(ticket.item_id) == null) return null;
        var active = (string.IsNullOrWhiteSpace(ticket.end_at) || time.IsActiveDate(ticket.start_at, ticket.end_at))
            && (string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at));
        if (row.object_type == ObjectMaster.TYPE_UNIT)
        {
            var expansion = current.unit == null ? null : master.UnitExpansion?.Find(current.unit.expansion_rank);
            var costume = ReadDefaultCostume(master, row.object_id);
            if (expansion == null || costume == 0) return null;
            return new RequestDraft(TicketExchange: new(row.id, ticket.id, ticket.item_id, ticket.consume_count,
                request.votive_picture_exchange_count, row.object_id, expansion.unit_count, active,
                ObjectType: row.object_type, CostumeId: costume));
        }
        var capacity = current.picture == null ? null : master.PictureExpansion?.Find(current.picture.expansion_rank);
        if (row.object_type != ObjectMaster.TYPE_PICTURE || master.Picture?.Find(row.object_id) == null || capacity == null)
            return null;
        var template = UserData.UserPicture.CreateUserPicture(row.object_id);
        if (template == null) return null;
        return new RequestDraft(TicketExchange: new(row.id, ticket.id, ticket.item_id, ticket.consume_count,
            request.votive_picture_exchange_count, row.object_id, capacity.picture_count, active,
            template.parameter1_value, template.parameter2_value, template.picture_characteristic_group_master_id));
    }

    private static int ReadDefaultCostume(MasterDB master, int unitId)
    {
        var unit = master.Unit?.Find(unitId);
        var costume = unit == null ? null : master.Costume?.Find(unit.default_costume_id);
        return costume != null && costume.unit_id == unitId ? costume.id : 0;
    }
}
