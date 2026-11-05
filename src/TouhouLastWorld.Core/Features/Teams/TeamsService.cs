using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class TeamsService
{
    internal TeamsService(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    private readonly PlayerState PlayerState;
    internal LocalReply DeckOverview(int? maxNumber)
    {
        if (maxNumber is null or <= 0 or int.MaxValue)
            return new(503, null, "Deck capacity unavailable.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var decks = updated["_local_saved_decks"] as JsonObject ?? new JsonObject();
        if (decks.Parent is null)
            updated["_local_saved_decks"] = decks;
        var saved = new JsonArray();
        var changed = false;
        for (var number = 0; number <= maxNumber.Value; number++)
        {
            var key = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (decks[key] is null)
            {
                decks[key] = new JsonObject
                {
                    ["deck_no"] = number,
                    ["deck_name"] = "",
                    ["deck_list"] = new JsonArray()
                };
                changed = true;
            }

            saved.Add(decks[key]!.DeepClone());
        }

        if (changed)
            PlayerState.Save(updated);
        return new(0, new() { ["progress"] = 0, ["path_list"] = new JsonArray(), ["saved_decks"] = saved, ["map_id"] = 0, ["map_buff_id"] = 0, ["selected_seal_id_list"] = new JsonArray(), ["drawn_seals"] = new JsonArray(), ["play_details"] = "", ["remaining_rerolls"] = 0, ["remaining_draws"] = 0 });
    }

    internal LocalReply CallDeck(int? number)
    {
        if (number is null or < 0)
            return new(400, null, "Nonnegative deck number required.");
        var key = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (PlayerState.user["_local_saved_decks"]?[key] is not JsonObject saved)
            return new(404, null, "Deck not saved.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var active = saved.DeepClone();
        active["deck_no"] = 0;
        updated["_local_saved_decks"]!["0"] = active;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply SaveDeck(SaveDeckCommand? request)
    {
        if (request?.Value is null or < 0 || request.Ids is null || request.Ids.Any(id => id <= 0 || id > int.MaxValue) || request.Text is null || request.Text.Any(char.IsControl))
            return new(400, null, "Invalid deck number, entries or name.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var decks = updated["_local_saved_decks"] as JsonObject ?? new JsonObject();
        if (decks.Parent is null)
            updated["_local_saved_decks"] = decks;
        var entry = new JsonObject
        {
            ["deck_no"] = request.Value.Value,
            ["deck_name"] = request.Text,
            ["deck_list"] = new JsonArray(request.Ids.Select(id => (JsonNode?)JsonValue.Create((int)id)).ToArray())
        };
        decks[request.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)] = entry;
        var active = entry.DeepClone();
        active["deck_no"] = 0;
        decks["0"] = active;
        PlayerState.Save(updated);
        return new(0, new());
    }

    internal LocalReply ClearedTeam(LocalReplayMeasure? query)
    {
        if (query is null || query.QuestType is < 0 or > 2 || query.TargetId <= 0 || query.Difficulty < 0)
            return new(400, null, "Invalid cleared team query.");
        var record = (PlayerState.user["_local_quest_cleared_teams"] as JsonArray)?.FirstOrDefault(entry => entry?["quest_type"]?.GetValue<int>() == query.QuestType && entry["target_id"]?.GetValue<int>() == query.TargetId && entry["difficulty"]?.GetValue<int>() == query.Difficulty);
        if (record is null)
            return new(0, new() { ["quest_cleared_team"] = "", ["use_cost"] = 0, ["season_enhancement_info"] = TeamSlots([]) });
        var encoded = record["quest_cleared_team"]?.GetValue<string>();
        var cost = record["use_cost"]?.GetValue<int>();
        var enhanced = record["season_enhancement_info"] as JsonArray;
        if (encoded is null || cost is null or < 0 || enhanced is null || enhanced.Count != 6 || enhanced.Any(id => id is not JsonValue value || !value.TryGetValue<long>(out var number) || number < 0))
            return new(503, null, "Stored cleared team is incomplete.");
        var fields = encoded.TrimEnd(',').Split(',');
        if (fields.Length > 7 || fields.Skip(1).Any(id => !long.TryParse(id, out var number) || number < 0))
            return new(503, null, "Stored cleared team encoding is invalid.");
        return new(0, new() { ["quest_cleared_team"] = encoded, ["use_cost"] = cost.Value, ["season_enhancement_info"] = enhanced.DeepClone() });
    }

    internal LocalReply UpdateSeriesTeams(LocalSeriesTeam[]? requests)
    {
        if (requests is null || requests.Length > 5 || requests.Select(team => team.Number).Distinct().Count() != requests.Length)
            return new(400, null, "Distinct series team slots required.");
        if (PlayerState.user["series_battle_status"]?["progress"]?.GetValue<int>() > 0)
            return new(409, null, "Series already in progress.");
        var owned = PlayerState.user["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        var updated = PlayerState.user.DeepClone().AsObject();
        var teams = updated["team"]!["team_list"]!.AsArray();
        foreach (var request in requests)
        {
            if (request.Number is < 80 or > 84 || request.Units is null || request.Units.Length > 6 || request.Units.Any(id => id < 0))
                return new(400, null, "Invalid series team fields.");
            var members = request.Units.Where(id => id > 0).ToArray();
            if (members.Distinct().Count() != members.Length)
                return new(400, null, "Team members must be distinct.");
            if (members.Any(id => !owned.Contains(id)))
                return new(404, null, "Team member not found.");
            var team = teams.FirstOrDefault(entry => entry!["number"]!.GetValue<int>() == request.Number);
            if (team is null)
            {
                team = new JsonObject
                {
                    ["number"] = request.Number,
                    ["name"] = "",
                    ["use_cost"] = 0
                };
                teams.Add(team);
            }

            team["user_unit_id_list"] = TeamSlots(request.Units);
            team["season_enhancement_user_unit_id_list"] = TeamSlots([]);
        }

        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["team"] = updated["team"]!.DeepClone() } });
    }

    internal long[] storyTeamMembers = [];
    internal LocalReply UpdateTeam(LocalTeamUpdate? request)
    {
        if (request is null || request.Number < 0 || request.Cost < 0 || request.CostWithFriend < 0 || (request.Name is not null && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Any(char.IsControl))) || request.Units?.Length > 6 || request.EnhancedUnits?.Length > 6)
            return new(400, null, "Invalid team fields.");
        var storyTeam = request.Number == 0;
        if (storyTeam && (request.Name is not null || request.Units is null))
            return new(400, null, "Invalid story team fields.");
        var updated = PlayerState.user.DeepClone().AsObject();
        var section = updated["team"]!.AsObject();
        var teams = section["team_list"]!.AsArray();
        var team = storyTeam ? null : teams.FirstOrDefault(entry => entry!["number"]!.GetValue<int>() == request.Number);
        if (team is null)
        {
            if (request.Number > request.NormalSlotLimit)
                return new(404, null, "Team slot not found.");
            team = new JsonObject
            {
                ["number"] = request.Number,
                ["name"] = "",
                ["use_cost"] = 0,
                ["user_unit_id_list"] = TeamSlots([]),
                ["season_enhancement_user_unit_id_list"] = TeamSlots([])
            };
            if (!storyTeam)
                teams.Add(team);
        }

        var members = request.Units ?? team["user_unit_id_list"]!.AsArray().Select(id => id!.GetValue<long>()).ToArray();
        var positive = members.Where(id => id > 0).ToArray();
        if (members.Any(id => id < 0) || positive.Distinct().Count() != positive.Length)
            return new(400, null, "Team members must be distinct.");
        var owned = PlayerState.user["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).ToHashSet();
        if (positive.Any(id => !owned.Contains(id)))
            return new(404, null, "Team member not found.");
        var enhanced = request.EnhancedUnits;
        if (enhanced is not null && enhanced.Where((id, index) => id != 0 && (index >= members.Length || id != members[index])).Any())
            return new(400, null, "Enhanced member must match its team slot.");
        if (request.Name is not null)
            team["name"] = request.Name;
        if (request.Units is not null)
        {
            team["user_unit_id_list"] = TeamSlots(members);
            team["use_cost"] = request.Cost;
            team["use_cost_with_friend"] = request.CostWithFriend;
            enhanced ??= team["season_enhancement_user_unit_id_list"]!.AsArray().Select((id, index) => index < members.Length && id!.GetValue<long>() == members[index] ? members[index] : 0L).ToArray();
        }

        if (enhanced is not null)
            team["season_enhancement_user_unit_id_list"] = TeamSlots(enhanced);
        // 原生 Team.UpdateNPC 会把零号编队的普通伙伴合入临时剧情槽位。
        if (storyTeam)
        {
            storyTeamMembers = positive;
            return new(0, new() { ["user_update"] = new JsonObject { ["team"] = new JsonObject { ["team_list"] = new JsonArray(team) } } });
        }

        PlayerState.Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["team"] = section.DeepClone() } });
    }

    internal static JsonArray TeamSlots(long[] ids) => new(Enumerable.Range(0, 6).Select(index => (JsonNode?)JsonValue.Create(index < ids.Length ? ids[index] : 0L)).ToArray());
}
