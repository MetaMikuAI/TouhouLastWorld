using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static bool SupportsAdditional(string api) => api is "tutorial_update" || QueryTemplates.ContainsKey(api);

    private LocalReply? DispatchAdditional(string api, LocalArguments? arguments) => api switch
    {
        "tutorial_update" => UpdateProgress(arguments?.Value),
        _ => QueryTemplates.TryGetValue(api, out var body) ? new(0, body.DeepClone().AsObject()) : null
    };

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
