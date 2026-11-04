using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply CompleteBoxRenovation(string section, LocalArguments? request)
    {
        if (user[section] is not JsonObject box)
            return new(503, null, "Local facility unavailable.");
        var finish = box["renovation_finish_time"]?.GetValue<string>();
        if (string.IsNullOrEmpty(finish)) return new(409, null, "No renovation in progress.");
        if (!DateTimeOffset.TryParse(finish, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var deadline))
            return new(503, null, "Invalid renovation time.");
        if (deadline > DateTimeOffset.UtcNow) return new(409, null, "Renovation is not finished.");
        var level = box["level"]!.GetValue<int>();
        if (level <= 0 || level == int.MaxValue || request?.Value != level || request.EntityId != (long)level + 1)
            return new(503, null, "Next facility level unavailable.");
        var updated = user.DeepClone().AsObject();
        updated[section]!["level"] = level + 1;
        updated[section]!["renovation_finish_time"] = "";
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = user[section]!.DeepClone() } });
    }
}
