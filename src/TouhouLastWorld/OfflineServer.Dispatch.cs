using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDispatchMember(long Id, int UnitId, int Level, bool Allowed);
public sealed record LocalDispatchStart(int DispatchId, int Seconds, int Cost, int Capacity,
    int RequiredCount, int RequiredLevel, int QualifiedCount, LocalDispatchMember[] Members);

public sealed partial class OfflineServer
{
    public int DispatchDefinitionId(long id) => user["_local_dispatch_offers"]?.AsArray()
        .FirstOrDefault(entry => entry?["user_dispatch_id"]?.GetValue<long>() == id)?["dispatch_id"]?.GetValue<int>() ?? 0;

    private LocalReply DispatchOverview()
    {
        var offers = user["_local_dispatch_offers"]?.DeepClone().AsArray() ?? new JsonArray();
        foreach (var task in user["dispatch"]!["dispatch_list"]!.AsArray())
        {
            var offer = offers.FirstOrDefault(entry => entry?["user_dispatch_id"]?.GetValue<long>() == task!["user_dispatch_id"]!.GetValue<long>());
            if (offer is null) offers.Add(task!.DeepClone());
            else
            {
                offer["end_time"] = task!["end_time"]!.DeepClone();
                offer["user_unit_id_list"] = task["user_unit_id_list"]!.DeepClone();
            }
        }
        return new(0, new() { ["dispatch_list"] = offers });
    }

    private LocalReply StartDispatch(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Ids is not { Length: > 0 and <= 6 } ids
            || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return new(400, null, "Invalid dispatch members.");
        var rule = request.DispatchStart;
        if (rule is null || rule.DispatchId <= 0 || rule.Seconds <= 0 || rule.Cost < 0 || rule.Capacity <= 0
            || rule.RequiredCount < 0 || rule.RequiredLevel < 0 || rule.QualifiedCount < 0
            || !rule.Members.Select(member => member.Id).Order().SequenceEqual(ids.Order()))
            return new(503, null, "Dispatch definition unavailable.");
        var offer = user["_local_dispatch_offers"]?.AsArray()
            .FirstOrDefault(entry => entry?["user_dispatch_id"]?.GetValue<long>() == request.EntityId);
        if (offer is null || offer["dispatch_id"]?.GetValue<int>() != rule.DispatchId)
            return new(404, null, "Dispatch offer not found.");
        var expiration = offer["appearance_finish_time"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(expiration) && (!DateTimeOffset.TryParse(expiration, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var expires) || expires <= DateTimeOffset.UtcNow))
            return new(409, null, "Dispatch offer expired.");
        var updated = user.DeepClone().AsObject();
        var dispatch = updated["dispatch"]!;
        var tasks = dispatch["dispatch_list"]!.AsArray();
        if (tasks.Count >= rule.Capacity || tasks.Any(task => task!["user_dispatch_id"]!.GetValue<long>() == request.EntityId))
            return new(409, null, "Dispatch capacity exhausted or task already started.");
        var owned = updated["unit"]!["unit_list"]!.AsArray();
        if (rule.Members.Any(member => !member.Allowed || !owned.Any(unit =>
                unit!["user_unit_id"]!.GetValue<long>() == member.Id && unit["unit_id"]!.GetValue<int>() == member.UnitId))
            || ids.Length < rule.RequiredCount || rule.Members.Count(member => member.Level >= rule.RequiredLevel) < rule.QualifiedCount)
            return new(409, null, "Dispatch member requirements not met.");
        var occupied = tasks.SelectMany(task => task!["user_unit_id_list"]!.AsArray()).Select(id => id!.GetValue<long>()).ToHashSet();
        var occupiedTypes = owned.Where(unit => occupied.Contains(unit!["user_unit_id"]!.GetValue<long>()))
            .Select(unit => unit!["unit_id"]!.GetValue<int>()).ToHashSet();
        if (rule.Members.Select(member => member.UnitId).Distinct().Count() != ids.Length
            || rule.Members.Any(member => occupied.Contains(member.Id) || occupiedTypes.Contains(member.UnitId)))
            return new(409, null, "Dispatch member already assigned.");
        var points = updated["phantasm_point"]!["points"]!.GetValue<int>();
        if (points < rule.Cost) return new(409, null, "Insufficient dispatch points.");
        tasks.Add(new JsonObject
        {
            ["user_dispatch_id"] = request.EntityId.Value, ["dispatch_id"] = rule.DispatchId,
            ["end_time"] = DateTime.UtcNow.AddSeconds(rule.Seconds).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["user_unit_id_list"] = new JsonArray(ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray())
        });
        updated["phantasm_point"]!["points"] = points - rule.Cost;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject
        {
            ["dispatch"] = dispatch.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone()
        } });
    }
}
