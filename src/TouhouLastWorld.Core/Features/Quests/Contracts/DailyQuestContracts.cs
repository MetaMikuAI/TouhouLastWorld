using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDailyQuestEntry(int QuestId, int StageId, int RequiredLevel, int AttemptLimit);
public sealed record LocalDailyQuestDay(int Date, int[] QuestIds)
{
    internal DateTime? StartAt(int switchingHour) => switchingHour is >= 0 and <= 23 && DateOnly.TryParseExact(Date.ToString(CultureInfo.InvariantCulture), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day.ToDateTime(new TimeOnly(switchingHour, 0)) : null;
}
