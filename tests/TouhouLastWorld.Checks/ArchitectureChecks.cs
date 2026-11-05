using System.Text.Json.Nodes;
using TouhouLastWorld;

internal static class ArchitectureChecks
{
    internal static void Run(string seed)
    {
        var store = new MemoryStore();
        var state = new PlayerState(store, seed, null, null);
        state.Save(new JsonObject { ["gold"] = 10 });
        store.Writes = 0;
        var result = state.Execute(() =>
        {
            state.Save(new JsonObject { ["gold"] = 20 });
            state.Save(new JsonObject { ["gold"] = 30 });
            return new(0, new());
        });
        Check(result.Status == 0 && store.Writes == 1 && JsonNode.Parse(store.User!)!["gold"]!.GetValue<int>() == 30,
            "multiple changes commit once");
        state.Execute(() =>
        {
            state.Save(new JsonObject { ["gold"] = 50 });
            return new(409, null);
        });
        Check(state.user["gold"]!.GetValue<int>() == 30 && store.Writes == 1, "rejected operation rolls back");
        store.FailWrite = true;
        try
        {
            state.Execute(() =>
            {
                state.Save(new JsonObject { ["gold"] = 60 });
                return new(0, new());
            });
            throw new Exception("storage failure was not propagated");
        }
        catch (IOException) { }
        Check(state.user["gold"]!.GetValue<int>() == 30 && store.Writes == 1, "write failure preserves committed state");
        store.FailWrite = false;
        state.Execute(() =>
        {
            state.Save(new JsonObject { ["gold"] = 40 });
            return new(0, new());
        });
        Check(store.Writes == 2 && state.user["gold"]!.GetValue<int>() == 40, "failed transaction can be retried");

        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var server = new OfflineServer(new MemoryStore(), seed, "offline://resources/", clock: new FixedClock(now), random: new Random(17));
        var init = server.Dispatch(new GameRequest("game_init"));
        Check(init.Body!["response_time"]!.GetValue<long>() == now.ToUnixTimeSeconds(), "clock is injected");
        server.Dispatch(new GameRequest("login"));
        Check(server.Dispatch(new GameRequest("unit_protection", new ApiPictureNewUpdateInput([1L]))).Status == 400,
            "route rejects an input belonging to another API");
        Parallel.For(1, 33, id =>
        {
            var reply = server.Dispatch("read_unit_talk", arguments: new(Value: id));
            Check(reply.Status == 0, "parallel requests complete");
        });
        var user = server.Dispatch(new GameRequest("login")).Body!["user"]!;
        Check(user["_local_read_talk_ids"]!.AsArray().Count == 32, "serialized operations retain every update");
        Console.WriteLine("Passed transaction rollback, injected clock and concurrent update checks.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MemoryStore : IStateStore
    {
        internal string? User;
        internal string? Account;
        internal int Writes;
        internal bool FailWrite;
        public string? ReadUser() => User;
        public string? ReadAccount() => Account;
        public void WriteUser(string json)
        {
            if (FailWrite) throw new IOException("Injected write failure.");
            User = json;
            Writes++;
        }
        public void WriteAccount(string json) => Account = json;
        public void BackupBeforeMigration() { }
        public void DeleteAccount() => Account = null;
    }
}
