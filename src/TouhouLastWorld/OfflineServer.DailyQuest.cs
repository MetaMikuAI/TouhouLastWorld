using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDailyQuestEntry(int QuestId, int StageId, int RequiredLevel, int AttemptLimit);
public sealed record LocalDailyQuestDay(int Date, int[] QuestIds)
{
    internal DateTime? StartAt(int switchingHour) =>
        switchingHour is >= 0 and <= 23
        && DateOnly.TryParseExact(Date.ToString(CultureInfo.InvariantCulture), "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day.ToDateTime(new TimeOnly(switchingHour, 0)) : null;
}

public sealed partial class OfflineServer
{
    private const string DailyQuestDay = "_local_daily_quest_day";
    private const string DailyQuestList = "daily_quest_count_list";

    internal int PendingDailyQuestStage => user[PendingQuestBattle]?["daily_stage_id"]?.GetValue<int>() ?? 0;

    private bool HasDailyQuestWin(LocalDailyQuestDay day) =>
        user[DailyQuestDay]?.GetValue<int>() == day.Date
        && (user[DailyQuestList] as JsonArray)?.Any(quest => quest?["count"]?.GetValue<int>() is > 0
            && day.QuestIds.Contains(quest["daily_quest_id"]!.GetValue<int>())) == true;

    private bool RefreshDailyQuestDay(LocalDailyQuestDay? day)
    {
        if (day is null || day.Date <= (user[DailyQuestDay]?.GetValue<int>() ?? 0)) return false;
        var updated = user.DeepClone().AsObject();
        updated[DailyQuestDay] = day.Date;
        var changed = false;
        if (updated[DailyQuestList] is JsonArray quests && user[DailyQuestDay] is not null)
        {
            var dailyIds = day.QuestIds.ToHashSet();
            foreach (var quest in quests)
            {
                if (!dailyIds.Contains(quest!["daily_quest_id"]!.GetValue<int>()) || quest["count"]?.GetValue<int>() is not > 0) continue;
                quest["count"] = 0;
                changed = true;
            }
        }
        Save(updated);
        return changed;
    }

    private LocalReply? CheckDailyQuest(LocalDailyQuestEntry entry)
    {
        if (entry.QuestId <= 0 || entry.StageId <= 0 || entry.AttemptLimit <= 0 || entry.RequiredLevel < 0
            || user[DailyQuestDay] is null)
            return new(503, null, "Daily quest rules unavailable.");
        if (user["level"]!.GetValue<int>() < entry.RequiredLevel)
            return new(409, null, "Required account level not reached.");
        var used = (user[DailyQuestList] as JsonArray)?.FirstOrDefault(row =>
            row?["daily_quest_id"]?.GetValue<int>() == entry.QuestId)?["count"]?.GetValue<int>() ?? 0;
        return used >= entry.AttemptLimit ? new(409, null, "Daily quest attempt limit reached.") : null;
    }

    private static void UpdateDailyQuestProgress(JsonObject updated, JsonObject pending, int state, JsonArray challenges,
        bool won, JsonObject changes)
    {
        var questId = pending["daily_quest_id"]!.GetValue<int>();
        var stageId = pending["daily_stage_id"]!.GetValue<int>();
        var quests = updated[DailyQuestList] as JsonArray ?? new JsonArray();
        var quest = quests.FirstOrDefault(row => row?["daily_quest_id"]?.GetValue<int>() == questId) as JsonObject;
        if (quest is null)
        {
            quest = new JsonObject { ["daily_quest_id"] = questId, ["count"] = 0, ["cleared_info"] = new JsonArray() };
            quests.Add(quest);
        }
        var stages = quest["cleared_info"] as JsonArray ?? new JsonArray();
        var stage = stages.FirstOrDefault(row => row?["daily_quest_stage_id"]?.GetValue<int>() == stageId) as JsonObject;
        if (stage is null)
        {
            stage = new JsonObject { ["daily_quest_stage_id"] = stageId };
            stages.Add(stage);
        }
        stage["normal"] ??= new JsonObject();
        SetQuestProgress(stage["normal"]!, state, challenges);
        if (stages.Parent is null) quest["cleared_info"] = stages;
        if (won && pending[DailyQuestDay]?.GetValue<int>() == updated[DailyQuestDay]?.GetValue<int>())
            quest["count"] = (quest["count"]?.GetValue<int>() ?? 0) + 1;
        if (quests.Parent is null) updated[DailyQuestList] = quests;
        changes[DailyQuestList] = quests.DeepClone();
    }
}
