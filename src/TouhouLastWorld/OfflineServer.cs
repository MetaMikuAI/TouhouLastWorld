using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalReply(int Status, JsonObject? Body, string Error = "");
public sealed record LocalResourcePaths(string Resource, string Master, string AssetBundle, int AppVersion, int ResourceVersion);
public sealed record LocalProfileUpdate(string? PlayerName = null, string? HeroName = null, string? Comment = null);
public sealed record LocalReplayMeasure(int QuestType, int TargetId, int Difficulty, int? WinRate = null, int? ActionNum = null);
public sealed record LocalReplayContent(int TargetId, int Level, string? Rules, string? Content);
public sealed record LocalTeamUpdate(int Number, string? Name, long[]? Units, int Cost, long[]? EnhancedUnits,
    int CostWithFriend, int NormalSlotLimit = 0);
public sealed record LocalHomeEntry(int UnitId, long UserUnitId, int CostumeId, int Motion, int Music);
public sealed record LocalHomeUpdate(LocalHomeEntry[]? Units, int SelectedIndex, int RandomMode);
public sealed record LocalEquipmentEntry(int Slot, long PictureId);
public sealed record LocalCostumeEntry(long UserUnitId, int CostumeId, int UnitId, bool IsDefault);
public sealed record LocalRhythmResult(int MusicId, int Difficulty, int Score, float Rate, int Rank);
public sealed record LocalPagePurchase(int Id, int Count, int MaxPages, int UnitCost);
public sealed record LocalSeriesTeam(int Number, long[]? Units);
public sealed record LocalFoodPurchase(int ItemId, int Count, int PointCost, int OrbCost);
public sealed record LocalFoodDistribution(int ItemId, int FoodId, int Count, int Value, int Capacity, int EffectMinutes);
public sealed record LocalTrainingStart(long UnitId, int Skill, int ItemId, int Slot, int Capacity, int Seconds);
public sealed record LocalTrainingMaterial(int ItemId, int Count, int Seconds);
public sealed record LocalPointReset(long UnitId, int GoldCost, bool FreeReset);
public sealed record LocalPointTarget(long Type, long Step);
public sealed record LocalPointRule(int Type, int Step, int Cost, int RequiredUsed);
public sealed record LocalPointAllocation(long UnitId, LocalPointTarget[]? Targets, LocalPointRule[]? Rules);
public sealed record LocalArguments(long? EntityId = null, int? Value = null, long[]? Ids = null, string? Text = null,
    LocalProfileUpdate? Profile = null, LocalReplayMeasure? ReplayMeasure = null, LocalReplayContent? ReplayContent = null,
    LocalTeamUpdate? Team = null, LocalHomeUpdate? Home = null, LocalEquipmentEntry[]? Equipment = null,
    LocalCostumeEntry[]? Costumes = null, LocalRhythmResult? Rhythm = null, LocalPagePurchase? Pages = null,
    LocalSeriesTeam[]? SeriesTeams = null, LocalFoodPurchase? FoodPurchase = null, LocalFoodDistribution? FoodDistribution = null,
    LocalTrainingStart? Training = null, LocalTrainingMaterial[]? TrainingMaterials = null, LocalPointReset? PointReset = null,
    LocalPointAllocation? PointAllocation = null, LocalIdleSet? Idle = null, LocalMailRules? MailRules = null,
    LocalBoxRenovation? Renovation = null, LocalBoxIncome? GoldIncome = null, LocalBoxIncome? SpiritIncome = null,
    LocalPointPurchase? PointPurchase = null, LocalRankMaterial? RankMaterial = null,
    LocalRankPromotion? RankPromotion = null, LocalAwakening? Awakening = null, LocalLimitBreak? LimitBreak = null,
    LocalStatMaterial[]? StatMaterials = null, LocalStatUnlock? StatUnlock = null, LocalPictureGrowth? PictureGrowth = null,
    LocalDestinyGrowth? DestinyGrowth = null, LocalPictureLimitBreak? PictureLimitBreak = null, LocalPictureRecycle? PictureRecycle = null,
    LocalItemExchange? ItemExchange = null, LocalContactDetails? ContactDetails = null, LocalDispatchStart? DispatchStart = null,
    LocalBookReward? BookReward = null, LocalCodeExchange? CodeExchange = null, LocalPassTask? PassTask = null,
    LocalPassReward[]? PassRewards = null, LocalPassGoods? PassGoods = null, LocalFreeItem? FreeItem = null,
    LocalEventRewards? EventRewards = null, LocalTicketExchange? TicketExchange = null,
    LocalAutomaticRelease? AutomaticRelease = null, LocalShopRefresh? ShopRefresh = null,
    LocalGoldExchange? GoldExchange = null, LocalIntroDefinitions? IntroDefinitions = null,
    LocalIntroGrant? IntroGrant = null, LocalHistoryQuery? HistoryQuery = null,
    LocalHistoryDefinitions? HistoryDefinitions = null, LocalUnitRecycle? UnitRecycle = null,
    LocalDispatchSettlement? DispatchSettlement = null, LocalRetryFix? RetryFix = null, LocalMissionClaim? MissionClaim = null,
    LocalDailyLoginMission? DailyLoginMission = null,
    LocalQuestBattleStart? QuestBattleStart = null, LocalQuestBattleEnd? QuestBattleEnd = null,
    LocalBattlePassClaim? BattlePassClaim = null, LocalBattlePassRewards? BattlePassRewards = null,
    LocalPointExchange? PointExchange = null, LocalSeriesStart? SeriesStart = null, LocalSeriesEnd? SeriesEnd = null, LocalTowerStart? TowerStart = null, LocalTowerEnd? TowerEnd = null, LocalTreasureDraw? TreasureDraw = null, LocalEventDraw? EventDraw = null, LocalArenaStart? ArenaStart = null, LocalArenaEnd? ArenaEnd = null, LocalVersusStart? VersusStart = null, LocalVersusEnd? VersusEnd = null, LocalBatchSearch? Batch = null, LocalReroll? Reroll = null);

public sealed partial class OfflineServer
{
    private readonly string resourceRoot;
    private readonly LocalResourcePaths? resourcePaths;
    internal bool HasResourceOverrides => resourcePaths is not null;
    private string? session;
    private DateTime? lastLoginAt;
    private JsonObject user;
    private readonly string userPath;
    private readonly JsonObject unitTemplate;
    private readonly JsonObject initialUser;
    private readonly string accountPath;
    private LocalAccount? account;
    private sealed record LocalAccount(string DeviceId, string UserId, string PublicId);
    public const string InitialKey = "fwkGnVPHkjpPdC78dYrQM2pRZMYEFEha";

    public OfflineServer(string directory, string seed, string resourceRoot, LocalResourcePaths? resourcePaths = null)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "user.json");
        userPath = path;
        unitTemplate = JsonNode.Parse(seed)!["unit"]!["unit_list"]![0]!.AsObject();
        initialUser = JsonNode.Parse(seed)!.AsObject();
        accountPath = Path.Combine(directory, "account.json");
        this.resourceRoot = resourceRoot;
        this.resourcePaths = resourcePaths;
        user = JsonNode.Parse(File.Exists(path) ? File.ReadAllText(path) : seed) as JsonObject
            ?? throw new InvalidDataException("Local user must be a JSON object.");
        if (!AccountDeleted && File.Exists(accountPath))
            account = JsonSerializer.Deserialize<LocalAccount>(File.ReadAllText(accountPath))
                ?? throw new InvalidDataException("Invalid local account.");
        if (!File.Exists(path))
        {
            File.WriteAllText(path + ".tmp", user.ToJsonString());
            File.Move(path + ".tmp", path);
        }
        if (AccountDeleted) FinishAccountDeletion();
        else MigrateInitialData();
    }

    public LocalReply Dispatch(string api, int? count = null, int? gachaId = null, int? gachaButtonId = null, LocalArguments? arguments = null)
    {
        if (api is not ("game_init" or "version_get" or "user_create" or "login" or "check_user"
            or "battle_start" or "battle_end" or "gacha_do" or "gacha_top" or "home" or "check_conditional_pack"
            or "afk_top" or "unit_protection" or "unit_new_update" or "picture_protection" or "picture_new_update"
            or "mail_top" or "friend/list" or "unit_voice_autoset_update" or "friend/apply_list" or "friend/recommend"
            or "get_shop_list") && !SupportsAdditional(api))
            return new(501, null, $"Offline API not implemented: {api}");
        if (api == "game_init")
        {
            session = Guid.NewGuid().ToString("N");
            lastLoginAt = null;
        }
        if (session is null) return new(401, null, "Offline game_init required.");
        if (api == "check_user" && account is null) return new(401, null, "Offline account required.");
        if (api is not ("game_init" or "version_get" or "user_create" or "login" or "boot") && account is null)
            return new(401, null, "Offline account required.");
        if (api == "gacha_do" && gachaId is not null)
        {
            if (gachaId != 10 || gachaButtonId is not (102 or 103)) return new(501, null, "Only the local validation pool is supported.");
            var expectedCount = gachaButtonId == 102 ? 1 : 10;
            if (count is not (null or 0) && count != expectedCount) return new(400, null, "Gacha count does not match button.");
            count = expectedCount;
        }
        if (api == "gacha_do" && count is not (1 or 10)) return new(400, null, "Local gacha requires count=1 or count=10.");
        if (api == "gacha_do" && LastUnitId >= long.MaxValue - count!.Value)
            return new(409, null, "Unit identifier limit exceeded.");
        if (api == "gacha_do" && user["crystal"]!.GetValue<int>() < count!.Value * 5)
            return new(400, null, "Not enough crystal.");
        if (api == "gacha_do" && user["gold"]!.GetValue<int>() < count!.Value * 1500)
            return new(400, null, "Not enough gold.");
        if (api is "user_create" or "login") EnsureAccount();
        if (api == "login") lastLoginAt = DateTime.Now;
        if (api == "home") RefreshFacilityStock(arguments);
        var operation = api switch
        {
            "unit_protection" => SetProtection(arguments, "unit", "unit_list", "user_unit_id"),
            "picture_protection" => SetProtection(arguments, "picture", "picture_list", "user_picture_id"),
            "unit_new_update" => ClearNewFlags(arguments, "unit", "unit_list", "user_unit_id"),
            "picture_new_update" => ClearNewFlags(arguments, "picture", "picture_list", "user_picture_id"),
            "unit_voice_autoset_update" => SetVoiceAutoset(arguments?.Value),
            "battle_start" => StartQuestBattle(arguments?.QuestBattleStart),
            "battle_end" => EndQuestBattle(arguments?.QuestBattleEnd),
            _ => DispatchAdditional(api, arguments)
        };
        if (operation is { Status: not 0 }) return operation;
        JsonObject body = operation?.Body ?? (api switch
        {
            // IDA: 两个 Set*Server 方法均先执行 Base64 解码再按 UTF-8 读取地址。
            "game_init" => new()
            {
                ["api_server"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("offline://api/")),
                ["resource_server"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(resourceRoot))
            },
            "user_create" => new() { ["device_id"] = account!.DeviceId },
            "check_user" => new(),
            "mail_top" => MailOverview(),
            "friend/list" => new() { ["data"] = new JsonArray(), ["request_tips"] = 0 },
            "friend/apply_list" => new() { ["data"] = new JsonArray() },
            "friend/recommend" => new() { ["data"] = new JsonArray() },
            "get_shop_list" => new() { ["shop_list"] = user["_local_shop_list"]?.DeepClone() ?? new JsonArray(), ["feature_product_shop_list"] = new JsonArray() },
            "check_conditional_pack" => new()
            {
                ["conditional_pack"] = new JsonArray(), ["is_ignore_check"] = 0
            },
            "afk_top" => IdleOverview(),
            "home" => new()
            {
                ["gathering_phantasm_point"] = user[SpiritStock]?.DeepClone() ?? JsonValue.Create(0),
                ["gathering_gold"] = user[GoldStock]?.DeepClone() ?? JsonValue.Create(0), ["unread_mail_num"] = 0,
                ["put_facility_badge"] = 0, ["put_mission_badge"] = 0, ["put_mission_id_list"] = new JsonArray(),
                ["put_dispatch_popup"] = 0, ["unread_talk_id_list"] = new JsonArray(), ["friend_receive_tips"] = 0,
                ["is_pass_card"] = 0, ["avatar_tips"] = 0, ["free_value_pack_tips"] = 0, ["pb_point"] = 0,
                ["put_multiple_spirits_weekly_badge"] = 0, ["put_mini_game_badge"] = 0,
                ["put_beginner_mission_id_list"] = new JsonArray(), ["put_friend_badge"] = 0,
                ["friend_count"] = 0, ["friend_request_count"] = 0, ["put_battle_pass_badge"] = 0,
                ["is_ranking_maintenance"] = 0, ["is_roguelite_free"] = 0
            },
            "gacha_top" => new()
            {
                ["gacha_list"] = GachaButtons(), ["gacha_point_exchange_count_list"] = user["_local_gacha_exchange_counts"]?.DeepClone() ?? new JsonArray(),
                ["gacha_point_list"] = user["_local_gacha_points"]?.DeepClone() ?? new JsonArray(), ["special_gacha_list"] = new JsonArray()
            },
            "gacha_do" => DrawGacha(count!.Value),
            "version_get" => new()
            {
                ["version"] = resourcePaths?.AppVersion ?? 92, ["resource_version"] = resourcePaths?.ResourceVersion ?? 5019,
                ["resource_path"] = resourcePaths?.Resource ?? resourceRoot.TrimEnd('/') + "/resource",
                ["master_path"] = resourcePaths?.Master ?? resourceRoot.TrimEnd('/') + "/master",
                ["assetbundle_path"] = resourcePaths?.AssetBundle ?? resourceRoot.TrimEnd('/') + "/assetbundle/android"
            },
            _ => new()
            {
                ["user"] = user.DeepClone(), ["device_id"] = account!.DeviceId,
                ["is_first_login"] = false, ["is_first_share"] = false,
                ["public_id"] = account.PublicId, ["openid"] = account.UserId, ["user_id"] = account.UserId,
                ["loading_info"] = new JsonArray(), ["retrygacha_loginevent"] = new JsonArray(),
                ["loginbonus"] = null, ["event_loginbonus"] = new JsonArray(),
                ["random_loginbonus"] = new JsonArray(), ["slot_loginbonus"] = new JsonArray(),
                ["information_update_time"] = DateTimeOffset.UtcNow.ToString("O"), ["is_FdOpen"] = 0
            }
        });
        body["session_id"] = session;
        body["encryption_key"] = InitialKey;
        body["response_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (api is "logout" or "delete_account") session = null;
        return new(0, body);
    }

    private LocalReply SetVoiceAutoset(int? value)
    {
        if (value is not (0 or 1)) return new(400, null, "Autoset flag 0 or 1 required.");
        var updated = user.DeepClone().AsObject();
        updated["unit_voice_autoset"] = value.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["unit_voice_autoset"] = value.Value } });
    }

    private LocalReply SetProtection(LocalArguments? arguments, string section, string list, string idField)
    {
        if (arguments?.EntityId is not > 0 || arguments.Value is not (0 or 1))
            return new(400, null, "Positive entity ID and protection flag 0 or 1 required.");
        var updated = user.DeepClone().AsObject();
        var unit = updated[section]![list]!.AsArray()
            .FirstOrDefault(item => item![idField]!.GetValue<long>() == arguments.EntityId);
        if (unit is null) return new(404, null, "Entity not found.");
        unit["is_protection"] = arguments.Value.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = user[section]!.DeepClone() } });
    }

    private LocalReply ClearNewFlags(LocalArguments? arguments, string section, string list, string idField)
    {
        if (arguments?.Ids is null || arguments.Ids.Any(id => id <= 0))
            return new(400, null, "Positive entity IDs required.");
        var updated = user.DeepClone().AsObject();
        var units = updated[section]![list]!.AsArray();
        var selected = arguments.Ids.Distinct().Select(id => units
            .FirstOrDefault(item => item![idField]!.GetValue<long>() == id)).ToArray();
        if (selected.Any(item => item is null)) return new(404, null, "Entity not found.");
        foreach (var unit in selected) unit!["is_new"] = 0;
        if (selected.Length > 0) Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = user[section]!.DeepClone() } });
    }

    private JsonObject DrawGacha(int count)
    {
        // 本地单角色池；消耗对应实机主表常驻按钮 102、103。
        var updated = user.DeepClone().AsObject();
        var units = updated["unit"]!["unit_list"]!.AsArray();
        var unitId = unitTemplate["unit_id"]!.GetValue<int>();
        var nextId = LastUnitId + 1;
        var drops = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var isNew = units.All(unit => unit!["unit_id"]!.GetValue<int>() != unitId) ? 1 : 0;
            var unit = unitTemplate.DeepClone().AsObject();
            unit["user_unit_id"] = nextId + index;
            unit["is_new"] = isNew;
            units.Add(unit);
            drops.Add(new JsonObject
            {
                ["object_type"] = 9, ["object_id"] = unitId,
                ["is_new"] = isNew, ["unique_id"] = nextId + index
            });
        }
        var beforeCrystal = updated["crystal"]!.GetValue<int>();
        updated["crystal"] = beforeCrystal - count * 5;
        updated["gold"] = updated["gold"]!.GetValue<int>() - count * 1500;
        updated["_local_unit_sequence"] = nextId + count - 1;
        Save(updated);
        return new()
        {
            ["gacha_drop_list"] = drops, ["gacha_list"] = GachaButtons(),
            ["gacha_point_list"] = new JsonArray(), ["gacha_directing_no"] = 1,
            ["beforeUserPowder"] = user["fortune_powder"]!.DeepClone(), ["beforeUserOrb"] = beforeCrystal,
            ["user_update"] = new JsonObject { ["gold"] = user["gold"]!.DeepClone(), ["crystal"] = user["crystal"]!.DeepClone(), ["unit"] = user["unit"]!.DeepClone() }
        };
    }

    private long LastUnitId => Math.Max(user["_local_unit_sequence"]?.GetValue<long>() ?? 0,
        user["unit"]!["unit_list"]!.AsArray().Select(unit => unit!["user_unit_id"]!.GetValue<long>()).DefaultIfEmpty().Max());

    private static JsonArray GachaButtons() => new(
        new JsonObject { ["gacha_button_id"] = 102, ["count"] = 0 },
        new JsonObject { ["gacha_button_id"] = 103, ["count"] = 0 });

    private void Save(JsonObject updated)
    {
        File.WriteAllText(userPath + ".tmp", updated.ToJsonString());
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(userPath + ".tmp", userPath, true);
                break;
            }
            catch (Exception error) when (OperatingSystem.IsWindows() && attempt < 4
                && (error is UnauthorizedAccessException || error is IOException && (error.HResult & 0xffff) is 32 or 33))
            {
                System.Threading.Thread.Sleep(25 << attempt);
            }
        }
        user = updated;
    }

    private void MigrateInitialData()
    {
        // 旧存档的角色与服装 ID 错置；仅迁移这一已知组合。
        var updated = user.DeepClone().AsObject();
        var changed = false;
        foreach (var unit in updated["unit"]!["unit_list"]!.AsArray())
        {
            if (unit!["unit_id"]!.GetValue<int>() != 100101 || unit["equip_costume_id"]!.GetValue<int>() != 1001) continue;
            unit["unit_id"] = 1001;
            unit["equip_costume_id"] = 100101;
            changed = true;
        }
        foreach (var unit in updated["home_unit_info"]!["unit_param"]!.AsArray())
        {
            if (unit!["unit_id"]!.GetValue<int>() != 100101 || unit["costume_id"]!.GetValue<int>() != 1001) continue;
            unit["unit_id"] = 1001;
            unit["costume_id"] = 100101;
            changed = true;
        }
        if (string.IsNullOrEmpty(updated["avatar_filename"]?.GetValue<string>()))
        {
            updated["avatar_filename"] = "Icon1001";
            changed = true;
        }
        foreach (var field in new[] { "phantasm_box", "gold_box" })
        {
            if (updated[field]!["level"]!.GetValue<int>() != 0) continue;
            updated[field]!["level"] = 1;
            changed = true;
        }
        foreach (var team in updated["team"]!["team_list"]!.AsArray())
        {
            foreach (var field in new[] { "user_unit_id_list", "season_enhancement_user_unit_id_list" })
            {
                var ids = team![field] as JsonArray;
                if (ids?.Count >= 6) continue;
                team[field] = TeamSlots(ids?.Select(id => id!.GetValue<long>()).ToArray() ?? []);
                changed = true;
            }
        }
        if (!changed) return;
        var backup = userPath + ".before-seed-fix";
        if (!File.Exists(backup)) File.Copy(userPath, backup);
        Save(updated);
    }

    private void EnsureAccount()
    {
        if (account is not null) return;
        if (AccountDeleted)
        {
            FinishAccountDeletion();
            Save(initialUser.DeepClone().AsObject());
            MigrateInitialData();
        }
        var created = new LocalAccount(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
            "LOCAL" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant());
        File.WriteAllText(accountPath + ".tmp", JsonSerializer.Serialize(created));
        File.Move(accountPath + ".tmp", accountPath);
        account = created;
    }

    private bool AccountDeleted => user["_local_account_deleted"]?.GetValue<bool>() == true;

    private LocalReply DeleteAccount()
    {
        Save(new JsonObject { ["_local_account_deleted"] = true });
        account = null;
        FinishAccountDeletion();
        return new(0, new());
    }

    private void FinishAccountDeletion()
    {
        File.Delete(accountPath);
        File.Delete(accountPath + ".tmp");
        File.Delete(userPath + ".before-seed-fix");
    }

    public static string VersionManifest(int appVersion) => new JsonObject
    {
        ["datas"] = new JsonArray(new JsonObject
        {
            ["Channel"] = "", ["Platform"] = "Android", ["AppVersion"] = appVersion,
            ["Url"] = "", ["Title"] = "", ["Msg"] = ""
        })
    }.ToJsonString();
}
