using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalIntroDefinitions(int[] UnitIds, int[] PictureIds);

public sealed partial class OfflineServer
{
    private LocalReply PrepareFirstDraw(LocalArguments? request)
    {
        if (account is null || string.IsNullOrEmpty(request?.Text) || request.Text != account.PublicId)
            return new(403, null, "Local account mismatch.");
        if (user["_local_first_draw_confirmed"]?.GetValue<bool>() == true)
            return new(409, null, "Initial result already confirmed.");
        var source = user["_local_first_draw_pending"] ?? user["_local_first_draw_candidate"];
        var rules = request.IntroDefinitions;
        if (source is not JsonObject candidate || rules is null
            || candidate["unit_ids"] is not JsonArray { Count: 1 } units
            || candidate["picture_ids"] is not JsonArray pictures || pictures.Count > 9)
            return new(503, null, "Local initial result unavailable.");
        var unit = units[0];
        if (unit?["unit_id"]?.GetValue<int>() is not > 0 || !rules.UnitIds.Contains(unit["unit_id"]!.GetValue<int>())
            || unit["voice_set_id"]?.GetValue<int>() is not > 0 || unit["voice_set_file_id"]?.GetValue<int>() is not > 0
            || pictures.Any(id => id is null || !rules.PictureIds.Contains(id.GetValue<int>())))
            return new(503, null, "Initial result references unavailable definitions.");
        var response = new JsonObject { ["unit_ids"] = units.DeepClone(), ["picture_ids"] = pictures.DeepClone() };
        if (user["_local_first_draw_pending"] is null)
        {
            var updated = user.DeepClone().AsObject();
            updated["_local_first_draw_pending"] = response.DeepClone();
            Save(updated);
        }
        return new(0, response);
    }
}
