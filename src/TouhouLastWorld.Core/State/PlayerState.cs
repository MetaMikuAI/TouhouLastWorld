using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed class PlayerState
{
    internal PlayerState(IStateStore store, string seed, TimeProvider? clock, Random? random)
    {
        this.store = store;
        Clock = clock ?? TimeProvider.System;
        Random = random ?? System.Random.Shared;
        initialUser = JsonNode.Parse(seed)!.AsObject();
        unitTemplate = initialUser["unit"]!["unit_list"]![0]!.AsObject();
        var saved = store.ReadUser();
        user = JsonNode.Parse(saved ?? seed) as JsonObject
            ?? throw new InvalidDataException("Local user must be a JSON object.");
        if (saved is null) store.WriteUser(user.ToJsonString());
    }

    internal JsonObject user { get; private set; }
    internal readonly IStateStore store;
    internal readonly JsonObject unitTemplate;
    internal readonly JsonObject initialUser;
    internal TimeProvider Clock { get; init; } = TimeProvider.System;
    internal Random Random { get; init; } = Random.Shared;
    private bool inOperation;
    private bool changed;

    internal LocalReply Execute(Func<LocalReply> operation)
    {
        if (inOperation) throw new InvalidOperationException("Nested player operation.");
        var previous = user;
        user = previous.DeepClone().AsObject();
        inOperation = true;
        changed = false;
        try
        {
            var reply = operation();
            if (reply.Status != 0)
            {
                user = previous;
                return reply;
            }
            if (changed) store.WriteUser(user.ToJsonString());
            return reply;
        }
        catch
        {
            user = previous;
            throw;
        }
        finally
        {
            inOperation = false;
            changed = false;
        }
    }

    internal void Save(JsonObject updated)
    {
        if (updated["unit"] is JsonObject)
        {
            var highestUnitLevel = Math.Max(PlayerProgress.HighestUnitLevel(user), PlayerProgress.HighestUnitLevel(updated));
            if (highestUnitLevel > (updated["_local_highest_unit_level"]?.GetValue<int>() ?? 0))
                updated["_local_highest_unit_level"] = highestUnitLevel;
        }

        if (inOperation) changed = true;
        else store.WriteUser(updated.ToJsonString());
        user = updated;
    }
}
