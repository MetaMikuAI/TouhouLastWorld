namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadTicketExchange(VotivePictureExchangeRequest? request)
    {
        if (request == null) return null;
        var master = MasterDB.This();
        var row = master?.VotivePictureExchange?.Find(request.votive_picture_exchange_id);
        var ticket = master?.VotivePicture?.Find(request.votive_picture_id);
        var picture = User.This()?.picture;
        var capacity = picture == null ? null : master?.PictureExpansion?.Find(picture.expansion_rank);
        var time = Applet.GameSystem.This()?.Time;
        if (row == null || ticket == null || row.votive_picture_id != ticket.id || row.object_type != ObjectMaster.TYPE_PICTURE
            || master?.Picture?.Find(row.object_id) == null || capacity == null || time == null
            || master.Item?.Find(ticket.item_id) == null) return null;
        var template = UserData.UserPicture.CreateUserPicture(row.object_id);
        if (template == null) return null;
        var active = (string.IsNullOrWhiteSpace(ticket.end_at) || time.IsActiveDate(ticket.start_at, ticket.end_at))
            && (string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at));
        return new LocalArguments(TicketExchange: new(row.id, ticket.id, ticket.item_id, ticket.consume_count,
            request.votive_picture_exchange_count, row.object_id, capacity.picture_count, active,
            template.parameter1_value, template.parameter2_value, template.picture_characteristic_group_master_id));
    }
}
