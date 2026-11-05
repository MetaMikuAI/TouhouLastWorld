using System.Text.Json.Nodes;
using TouhouLastWorld;

internal sealed partial class RegressionSuite(string[] args)
{
    private string directory = default!;
    private string seed = default!;
    private global::TouhouLastWorld.OfflineServer server = default!;
    private global::System.Text.Json.Nodes.JsonNode fundedSeed = default!;
    private global::TouhouLastWorld.OfflineServer flags = default!;
    private global::System.Text.Json.Nodes.JsonNode teamSeed = default!;
    private string teamDirectory = default!;
    private global::TouhouLastWorld.OfflineServer teams = default!;
    private global::System.Text.Json.Nodes.JsonObject equipmentSeed = default!;
    private string equipmentDirectory = default!;
    private global::TouhouLastWorld.OfflineServer equipmentReload = default!;
    private global::System.Text.Json.Nodes.JsonObject weeklyOverviewSeed = default!;
    private global::System.Text.Json.Nodes.JsonObject dailyOverviewSeed = default!;
    private string towerUnitCsv = default!;
    private long rankMaterialId = default!;
    private global::TouhouLastWorld.LocalRankMaterial rankMaterialDefinition = default!;
    private global::System.Text.Json.Nodes.JsonNode pictureBreakSeed = default!;
    private string relationDirectory = default!;
    private global::TouhouLastWorld.OfflineServer relationReload = default!;
    private global::System.Text.Json.Nodes.JsonNode introSeed = default!;
    private string introDirectory = default!;
    private string introPublicId = default!;
    private global::TouhouLastWorld.LocalIntroDefinitions introRules = default!;
    private global::TouhouLastWorld.OfflineServer introReload = default!;
    private global::TouhouLastWorld.LocalHistoryDefinitions historyDefinitions = default!;
    private global::System.Text.Json.Nodes.JsonNode arenaEntrySeed = default!;
    private global::TouhouLastWorld.LocalArenaStart arenaEntryRule = default!;
    private global::TouhouLastWorld.OfflineServer arenaEntryReload = default!;
    private global::System.Text.Json.Nodes.JsonNode arenaWeeklySeed = default!;
    private global::TouhouLastWorld.LocalArenaStart arenaWeeklyRule = default!;
    private int assertions;
    void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    JsonObject MailFixture(long id, int type, int amount, int itemId = 0, int grace = 0) => new()
    {
        ["id"] = id,
        ["open_type"] = 0,
        ["receive_grace"] = grace,
        ["present_list"] = new JsonArray(new JsonObject { ["object_type"] = type, ["object_id"] = itemId, ["object_value"] = amount })
    };

    internal void Run()
    {
        Scenario00();
        Scenario01();
        Scenario02();
        Scenario03();
        Scenario04();
        Scenario05();
        Scenario06();
        Scenario07();
        Scenario08();
        Scenario09();
        Scenario10();
        Scenario11();
        Scenario12();
        Scenario13();
        Scenario14();
        Scenario15();
        Scenario16();
        Console.WriteLine($"Passed {assertions} regression assertions in 17 scenario groups.");
    }
}
