using Applet;

namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadVersusStart(VersusBattleStartRequest? request)
    {
        var master = MasterDB.This();
        var record = request == null ? null : master?.MultipleSpiritsWeekly?.Find(request.multiple_spirits_weekly_master_id);
        var time = GameSystem.This()?.Time;
        if (request == null || record == null || time == null || master?.Setting == null
            || request.team_number != VersusBehavior.battleNumber + 70) return null;
        if (!int.TryParse(master.Setting.Find("MULTIPLE_SPIRITS_WEEKLY_RELEASE_EPISODE_ID")?.value, out var episode)) episode = 100200506;
        if (master.Episode?.Find(episode) == null) return null;
        var selected = VersusMatchPanel.activeRuleRecords?.ToArray();
        var rules = new List<LocalVersusRule>();
        if (selected != null)
        {
            foreach (var selection in selected)
            {
                var rule = master.SpecialRules?.Find(selection.id);
                if (rule == null || rule.group_id != record.special_rules_group_id) return null;
                rules.Add(new(rule.id, rule.add_level));
            }
        }
        return new(VersusStart: new(record.id, request.cleared_level, request.final_cleared_level, request.team_number,
            time.IsActiveDate(record.start_at, record.end_at), episode, request.helper_info_dict != null, rules.ToArray()));
    }

    private static LocalArguments? ReadVersusOverview()
    {
        var records = MasterDB.This()?.MultipleSpiritsWeekly?.ToList()?.ToArray();
        var time = GameSystem.This()?.Time;
        return records == null || time == null ? null : new(Ids: records.Where(row => time.IsActiveDate(row.start_at, row.end_at))
            .OrderBy(row => row.display_order).Select(row => (long)row.id).ToArray());
    }
}
