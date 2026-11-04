using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalReroll(int Selected, int[] Offered, int Draws, int Rerolls, int[] Definitions);

public sealed partial class OfflineServer
{
    private LocalReply Reroll(LocalReroll? rule)
    {
        if (rule is null) return new(503, null, "Draw definitions unavailable.");
        if (rule.Selected != 0) return new(503, null, "Effect redraw unavailable.");
        var state = user["_local_rogue_draw"];
        if (state?["drawn_seals"] is not JsonArray offered || offered.Count == 0
            || state["remaining_draws"]?.GetValue<int>() is not > 0
            || state["remaining_rerolls"]?.GetValue<int>() is not > 0)
            return new(409, null, "Active draw with remaining rerolls required.");
        if (!offered.Select(id => id!.GetValue<int>()).SequenceEqual(rule.Offered)
            || rule.Draws != state["remaining_draws"]!.GetValue<int>()
            || rule.Rerolls != state["remaining_rerolls"]!.GetValue<int>())
            return new(409, null, "Draw state changed.");
        if (user["_local_rogue_reroll_sequence"] is not JsonArray sequence || sequence.Count == 0
            || sequence[0] is not JsonArray next || next.Count != offered.Count
            || next.Any(id => id is null || id.GetValue<int>() <= 0 || !rule.Definitions.Contains(id.GetValue<int>()))
            || offered.Any(id => id is null || !rule.Definitions.Contains(id.GetValue<int>())))
            return new(503, null, "Prepared draw outcome unavailable.");
        var updated = user.DeepClone().AsObject();
        var result = new JsonObject { ["drawn_seals"] = next.DeepClone(), ["remaining_draws"] = rule.Draws,
            ["remaining_rerolls"] = rule.Rerolls - 1 };
        updated["_local_rogue_draw"] = result.DeepClone();
        updated["_local_rogue_reroll_sequence"]!.AsArray().RemoveAt(0);
        Save(updated);
        return new(0, result);
    }
}
