using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private LocalReply UpdateSeriesTeams(LocalSeriesTeam[]? requests)
    {
        if (requests is null || requests.Length > 5 || requests.Select(team => team.Number).Distinct().Count() != requests.Length)
            return new(400, null, "Distinct series team slots required.");
        if (user["series_battle_status"]?["progress"]?.GetValue<int>() > 0)
            return new(409, null, "Series already in progress.");
        var owned = user["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        var updated = user.DeepClone().AsObject();
        var teams = updated["team"]!["team_list"]!.AsArray();
        foreach (var request in requests)
        {
            if (request.Number is < 80 or > 84 || request.Units is null || request.Units.Length > 6 || request.Units.Any(id => id < 0))
                return new(400, null, "Invalid series team fields.");
            var members = request.Units.Where(id => id > 0).ToArray();
            if (members.Distinct().Count() != members.Length) return new(400, null, "Team members must be distinct.");
            if (members.Any(id => !owned.Contains(id))) return new(404, null, "Team member not found.");
            var team = teams.FirstOrDefault(entry => entry!["number"]!.GetValue<int>() == request.Number);
            if (team is null)
            {
                team = new JsonObject { ["number"] = request.Number, ["name"] = "", ["use_cost"] = 0 };
                teams.Add(team);
            }
            team["user_unit_id_list"] = TeamSlots(request.Units);
            team["season_enhancement_user_unit_id_list"] = TeamSlots([]);
        }
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["team"] = updated["team"]!.DeepClone() } });
    }

    private LocalReply UpdateTeam(LocalTeamUpdate? request)
    {
        if (request is null || request.Number <= 0 || request.Cost < 0 || request.CostWithFriend < 0
            || (request.Name is not null && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Any(char.IsControl)))
            || request.Units?.Length > 6 || request.EnhancedUnits?.Length > 6)
            return new(400, null, "Invalid team fields.");
        var updated = user.DeepClone().AsObject();
        var section = updated["team"]!.AsObject();
        var team = section["team_list"]!.AsArray().FirstOrDefault(entry => entry!["number"]!.GetValue<int>() == request.Number);
        if (team is null) return new(404, null, "Team slot not found.");
        var members = request.Units ?? team["user_unit_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).ToArray();
        var positive = members.Where(id => id > 0).ToArray();
        if (members.Any(id => id < 0) || positive.Distinct().Count() != positive.Length)
            return new(400, null, "Team members must be distinct.");
        var owned = user["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        if (positive.Any(id => !owned.Contains(id))) return new(404, null, "Team member not found.");
        var enhanced = request.EnhancedUnits;
        if (enhanced is not null && enhanced.Where((id, index) => id != 0 && (index >= members.Length || id != members[index])).Any())
            return new(400, null, "Enhanced member must match its team slot.");
        if (request.Name is not null) team["name"] = request.Name;
        if (request.Units is not null)
        {
            team["user_unit_id_list"] = TeamSlots(members);
            team["use_cost"] = request.Cost;
            team["use_cost_with_friend"] = request.CostWithFriend;
            enhanced ??= team["season_enhancement_user_unit_id_list"]!.AsArray()
                .Select((id, index) => index < members.Length && id!.GetValue<long>() == members[index] ? members[index] : 0L).ToArray();
        }
        if (enhanced is not null) team["season_enhancement_user_unit_id_list"] = TeamSlots(enhanced);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["team"] = section.DeepClone() } });
    }

    private static JsonArray TeamSlots(long[] ids) => new(Enumerable.Range(0, 6)
        .Select(index => (JsonNode?)JsonValue.Create(index < ids.Length ? ids[index] : 0L)).ToArray());
}
