using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalContactDetails(string? Name, string? ZipCode, string? Address, string? Phone);

public sealed partial class OfflineServer
{
    private LocalReply SaveContactDetails(LocalContactDetails? details)
    {
        if (details is null || new[] { details.Name, details.ZipCode, details.Address, details.Phone }
            .Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)))
            return new(400, null, "Invalid local contact details.");
        var updated = user.DeepClone().AsObject();
        updated["_local_personal_info"] = new JsonObject
        {
            ["user_name"] = details.Name, ["zip_code"] = details.ZipCode,
            ["address"] = details.Address, ["phone_number"] = details.Phone
        };
        var state = updated["figure_present"] as JsonObject ?? new JsonObject { ["rest_days"] = 0 };
        state["applied"] = 1;
        if (state.Parent is null) updated["figure_present"] = state;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["figure_present"] = state.DeepClone() } });
    }

    private LocalReply SaveAnniversaryInfo(LocalArguments? request)
    {
        var address = request?.Text?.Trim();
        if (request?.Value != 1 || string.IsNullOrEmpty(address) || !address.Contains('@') || address.Any(char.IsControl))
            return new(400, null, "Invalid local application.");
        var updated = user.DeepClone().AsObject();
        updated["_local_anniversary_info"] = new JsonObject { ["mail_address"] = address };
        var state = updated["anniversary_present"] as JsonObject ?? new JsonObject { ["rest_days"] = 0 };
        state["applied"] = 1;
        if (state.Parent is null) updated["anniversary_present"] = state;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["anniversary_present"] = state.DeepClone() } });
    }
}
