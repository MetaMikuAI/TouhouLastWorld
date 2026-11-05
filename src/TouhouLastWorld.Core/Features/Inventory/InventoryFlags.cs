using System.Text.Json.Nodes;

namespace TouhouLastWorld;

internal sealed class InventoryFlags(Func<JsonObject> read, Action<JsonObject> commit)
{
    private JsonObject user => read();

    private void Save(JsonObject updated) => commit(updated);
    public LocalReply SetVoiceAutoset(int? value)
    {
        if (value is not (0 or 1))
            return new(400, null, "Autoset flag 0 or 1 required.");
        var updated = user.DeepClone().AsObject();
        updated["unit_voice_autoset"] = value.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit_voice_autoset"] = value.Value } });
    }

    public LocalReply SetProtection(SetProtectionCommand? arguments, string section, string list, string idField)
    {
        if (arguments?.EntityId is not > 0 || arguments.Value is not (0 or 1))
            return new(400, null, "Positive entity ID and protection flag 0 or 1 required.");
        var updated = user.DeepClone().AsObject();
        var unit = updated[section]![list]!.AsArray().FirstOrDefault(item => item![idField]!.GetValue<long>() == arguments.EntityId);
        if (unit is null)
            return new(404, null, "Entity not found.");
        unit["is_protection"] = arguments.Value.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = user[section]!.DeepClone() } });
    }

    public LocalReply ClearNewFlags(ClearNewFlagsCommand? arguments, string section, string list, string idField)
    {
        if (arguments?.Ids is null || arguments.Ids.Any(id => id <= 0))
            return new(400, null, "Positive entity IDs required.");
        var updated = user.DeepClone().AsObject();
        var units = updated[section]![list]!.AsArray();
        var selected = arguments.Ids.Distinct().Select(id => units.FirstOrDefault(item => item![idField]!.GetValue<long>() == id)).ToArray();
        if (selected.Any(item => item is null))
            return new(404, null, "Entity not found.");
        foreach (var unit in selected)
            unit!["is_new"] = 0;
        if (selected.Length > 0)
            Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = user[section]!.DeepClone() } });
    }
}
