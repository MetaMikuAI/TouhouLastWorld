using System.Text.Json.Nodes;
using System.Text.Json;
namespace TouhouLastWorld;

internal sealed class SeedMigration(PlayerState PlayerState)
{

    internal void Run()
    {
        var updated = PlayerState.user.DeepClone().AsObject();
        var changed = QuestsService.RefundInterruptedQuest(updated);
        var recoveryStart = PlayerState.Clock.GetUtcNow().ToString("O");
        foreach (var unit in updated["unit"]!["unit_list"]!.AsArray())
        {
            if (string.IsNullOrWhiteSpace(unit!["last_motivation_at"]?.GetValue<string>()))
            {
                unit["last_motivation_at"] = recoveryStart;
                changed = true;
            }

            // 旧存档的角色与服装 ID 错置，仅迁移这一已知组合。
            if (unit["unit_id"]!.GetValue<int>() != 100101 || unit["equip_costume_id"]!.GetValue<int>() != 1001)
                continue;
            unit["unit_id"] = 1001;
            unit["equip_costume_id"] = 100101;
            changed = true;
        }

        foreach (var unit in updated["home_unit_info"]!["unit_param"]!.AsArray())
        {
            if (unit!["unit_id"]!.GetValue<int>() != 100101 || unit["costume_id"]!.GetValue<int>() != 1001)
                continue;
            unit["unit_id"] = 1001;
            unit["costume_id"] = 100101;
            changed = true;
        }

        if (string.IsNullOrEmpty(updated["avatar_filename"]?.GetValue<string>()))
        {
            updated["avatar_filename"] = "Icon1001";
            changed = true;
        }

        foreach (var field in new[]
        {
            "phantasm_box",
            "gold_box"
        }

        )
        {
            if (updated[field]!["level"]!.GetValue<int>() != 0)
                continue;
            updated[field]!["level"] = 1;
            changed = true;
        }

        foreach (var team in updated["team"]!["team_list"]!.AsArray())
        {
            foreach (var field in new[]
            {
                "user_unit_id_list",
                "season_enhancement_user_unit_id_list"
            }

            )
            {
                var ids = team![field] as JsonArray;
                if (ids?.Count >= 6)
                    continue;
                team[field] = TeamsService.TeamSlots(ids?.Select(id => id!.GetValue<long>()).ToArray() ?? []);
                changed = true;
            }
        }

        if (!changed)
            return;
        PlayerState.store.BackupBeforeMigration();
        PlayerState.Save(updated);
    }

}
