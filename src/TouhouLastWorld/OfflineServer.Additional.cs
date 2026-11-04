using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static bool SupportsAdditional(string api) => api is "tutorial_update" or "logout" or "set_age" || QueryTemplates.ContainsKey(api);

    private LocalReply? DispatchAdditional(string api, LocalArguments? arguments) => api switch
    {
        "tutorial_update" => UpdateProgress(arguments?.Value),
        "set_age" => arguments?.Value is >= 1 and <= 3
            ? SetUserInteger("age_limit_type", arguments.Value.Value)
            : new(400, null, "Age category must be 1, 2 or 3."),
        "logout" => arguments?.Text == account!.UserId
            ? new(0, new()) : new(403, null, "Local identity does not match."),
        _ => QueryTemplates.TryGetValue(api, out var body) ? new(0, body.DeepClone().AsObject()) : null
    };

    private LocalReply SetUserInteger(string field, int value)
    {
        var updated = user.DeepClone().AsObject();
        updated[field] = value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = value } });
    }

    private LocalReply UpdateProgress(int? progress)
    {
        if (progress is null or < 0) return new(400, null, "Nonnegative progress required.");
        var updated = user.DeepClone().AsObject();
        var value = Math.Max(updated["progress_value"]!.GetValue<int>(), progress.Value);
        updated["progress_value"] = value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["progress_value"] = value } });
    }
}
