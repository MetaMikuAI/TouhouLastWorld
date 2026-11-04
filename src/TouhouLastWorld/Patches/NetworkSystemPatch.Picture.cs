namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadPictureLimitBreak(PicturePotentialLimitbreakRequest? request)
    {
        if (request == null) return null;
        var ids = request.material_user_picture_id_list?.ToArray();
        var result = new LocalArguments(request.user_picture_id, Ids: ids);
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
