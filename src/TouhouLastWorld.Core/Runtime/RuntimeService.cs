using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;

namespace TouhouLastWorld;

internal sealed partial class RuntimeService
{
    internal RuntimeService(AccountService accounts, SessionState sessionState, BattleModesService battleModesService, DispatchService dispatchService, EconomyService economyService, EventsService eventsService, FacilitiesService facilitiesService, GachaService gachaService, MailService mailService, MissionsService missionsService, PicturesService picturesService, PlayerState playerState, ProfileService profileService, QuestsService questsService, ShopService shopService, SocialService socialService, TeamsService teamsService, UnitsService unitsService)
    {
        Accounts = accounts;
        SessionState = sessionState;
        BattleModesService = battleModesService;
        DispatchService = dispatchService;
        EconomyService = economyService;
        EventsService = eventsService;
        FacilitiesService = facilitiesService;
        GachaService = gachaService;
        MailService = mailService;
        MissionsService = missionsService;
        PicturesService = picturesService;
        PlayerState = playerState;
        ProfileService = profileService;
        QuestsService = questsService;
        ShopService = shopService;
        SocialService = socialService;
        TeamsService = teamsService;
        UnitsService = unitsService;
    }

    private readonly AccountService Accounts;
    private readonly SessionState SessionState;
    private readonly BattleModesService BattleModesService;
    private readonly DispatchService DispatchService;
    private readonly EconomyService EconomyService;
    private readonly EventsService EventsService;
    private readonly FacilitiesService FacilitiesService;
    private readonly GachaService GachaService;
    private readonly MailService MailService;
    private readonly MissionsService MissionsService;
    private readonly PicturesService PicturesService;
    private readonly PlayerState PlayerState;
    private readonly ProfileService ProfileService;
    private readonly QuestsService QuestsService;
    private readonly ShopService ShopService;
    private readonly SocialService SocialService;
    private readonly TeamsService TeamsService;
    private readonly UnitsService UnitsService;
    internal string resourceRoot = null!;
    internal LocalResourcePaths? resourcePaths;
    internal bool HasResourceOverrides => resourcePaths is not null;

    internal InventoryFlags flags = null!;
    internal object operationGate = new();
    internal const string InitialKey = "fwkGnVPHkjpPdC78dYrQM2pRZMYEFEha";
    internal void Initialize(string resourceRoot, LocalResourcePaths? resourcePaths = null)
    {
        this.resourceRoot = resourceRoot;
        this.resourcePaths = resourcePaths;
        routes = BuildRoutes();
        flags = new InventoryFlags(() => PlayerState.user, PlayerState.Save);
        var identity = Accounts.AccountDeleted ? null : PlayerState.store.ReadAccount();
        if (identity is not null)
            SessionState.account = JsonSerializer.Deserialize<SessionState.LocalAccount>(identity) ?? throw new InvalidDataException("Invalid local account.");
        if (Accounts.AccountDeleted)
            Accounts.FinishAccountDeletion();
        else
            Accounts.Migrate();
    }

    internal LocalReply Dispatch(GameRequest request)
    {
        lock (operationGate)
        {
            // 账号建立与删除保留身份文件和删除标记的恢复顺序。
            if (request.Api is "login" or "user_create" or "delete_account")
                return DispatchCore(request);
            var session = SessionState.session;
            var login = SessionState.lastLoginAt;
            var team = TeamsService.storyTeamMembers;
            try
            {
                var reply = PlayerState.Execute(() => DispatchCore(request));
                if (reply.Status == 0)
                    return reply;
                SessionState.session = session;
                SessionState.lastLoginAt = login;
                TeamsService.storyTeamMembers = team;
                return reply;
            }
            catch
            {
                SessionState.session = session;
                SessionState.lastLoginAt = login;
                TeamsService.storyTeamMembers = team;
                throw;
            }
        }
    }

    internal LocalReply DispatchCore(GameRequest request)
    {
        var api = request.Api;
        var count = request.Count;
        var gachaId = request.GachaId;
        var gachaButtonId = request.GachaButtonId;
        if (!routes.TryGetValue(api, out var handler))
            return new(501, null, $"Offline API not implemented: {api}");
        if (api == "game_init")
        {
            SessionState.session = Guid.NewGuid().ToString("N");
            SessionState.lastLoginAt = null;
            TeamsService.storyTeamMembers = [];
        }

        if (SessionState.session is null)
            return new(401, null, "Offline game_init required.");
        if (api == "check_user" && SessionState.account is null)
            return new(401, null, "Offline account required.");
        if (api is not ("game_init" or "version_get" or "user_create" or "login" or "boot") && SessionState.account is null)
            return new(401, null, "Offline account required.");
        if (api == "gacha_do" && gachaId is not null)
        {
            if (gachaId != 10 || gachaButtonId is not (102 or 103))
                return new(501, null, "Only the local validation pool is supported.");
            var expectedCount = gachaButtonId == 102 ? 1 : 10;
            if (count is not (null or 0) && count != expectedCount)
                return new(400, null, "Gacha count does not match button.");
            count = expectedCount;
        }

        if (api == "gacha_do" && count is not (1 or 10))
            return new(400, null, "Local gacha requires count=1 or count=10.");
        if (api == "gacha_do" && EconomyService.LastUnitId >= long.MaxValue - count!.Value)
            return new(409, null, "Unit identifier limit exceeded.");
        if (api == "gacha_do" && PlayerState.user["crystal"]!.GetValue<int>() < count!.Value * 5)
            return new(400, null, "Not enough crystal.");
        if (api == "gacha_do" && PlayerState.user["gold"]!.GetValue<int>() < count!.Value * 1500)
            return new(400, null, "Not enough gold.");
        if (api is "user_create" or "login")
            Accounts.EnsureAccount();
        if (api == "login")
            SessionState.lastLoginAt = PlayerState.Clock.GetLocalNow().DateTime;
        var dailyReset = QuestsService.RefreshDailyQuestDay(request.Day);
        if (api == "home")
            FacilitiesService.RefreshFacilityStock(request.FacilityIncome);
        var operation = handler(request.Payload);
        if (operation is { Status: not 0 })
            return operation;
        JsonObject body = operation?.Body ?? (api switch
        {
            // IDA: 两个 Set*Server 方法均先执行 Base64 解码再按 UTF-8 读取地址。
            "game_init" => new()
            {
                ["api_server"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("offline://api/")),
                ["resource_server"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(resourceRoot))
            },
            "user_create" => new()
            {
                ["device_id"] = SessionState.account!.DeviceId
            },
            "check_user" => new(),
            "mail_top" => MailService.MailOverview(),
            "friend/list" => new()
            {
                ["data"] = new JsonArray(),
                ["request_tips"] = 0
            },
            "friend/apply_list" => new()
            {
                ["data"] = new JsonArray()
            },
            "friend/recommend" => new()
            {
                ["data"] = new JsonArray()
            },
            "get_shop_list" => new()
            {
                ["shop_list"] = PlayerState.user["_local_shop_list"]?.DeepClone() ?? new JsonArray(),
                ["feature_product_shop_list"] = new JsonArray()
            },
            "check_conditional_pack" => new()
            {
                ["conditional_pack"] = new JsonArray(),
                ["is_ignore_check"] = 0
            },
            "afk_top" => FacilitiesService.IdleOverview(),
            "home" => new()
            {
                ["gathering_phantasm_point"] = PlayerState.user[FacilitiesService.SpiritStock]?.DeepClone() ?? JsonValue.Create(0),
                ["gathering_gold"] = PlayerState.user[FacilitiesService.GoldStock]?.DeepClone() ?? JsonValue.Create(0),
                ["unread_mail_num"] = 0,
                ["put_facility_badge"] = 0,
                ["put_mission_badge"] = 0,
                ["put_mission_id_list"] = new JsonArray(),
                ["put_dispatch_popup"] = 0,
                ["unread_talk_id_list"] = new JsonArray(),
                ["friend_receive_tips"] = 0,
                ["is_pass_card"] = 0,
                ["avatar_tips"] = 0,
                ["free_value_pack_tips"] = 0,
                ["pb_point"] = 0,
                ["put_multiple_spirits_weekly_badge"] = 0,
                ["put_mini_game_badge"] = 0,
                ["put_beginner_mission_id_list"] = MissionsService.NoviceMissionBadge(request.ProgressMissions),
                ["put_friend_badge"] = 0,
                ["friend_count"] = 0,
                ["friend_request_count"] = 0,
                ["put_battle_pass_badge"] = 0,
                ["is_ranking_maintenance"] = 0,
                ["is_roguelite_free"] = 0
            },
            "gacha_top" => new()
            {
                ["gacha_list"] = GachaService.GachaButtons(),
                ["gacha_point_exchange_count_list"] = PlayerState.user["_local_gacha_exchange_counts"]?.DeepClone() ?? new JsonArray(),
                ["gacha_point_list"] = PlayerState.user["_local_gacha_points"]?.DeepClone() ?? new JsonArray(),
                ["special_gacha_list"] = new JsonArray()
            },
            "gacha_do" => GachaService.DrawGacha(count!.Value),
            "version_get" => new()
            {
                ["version"] = resourcePaths?.AppVersion ?? 92,
                ["resource_version"] = resourcePaths?.ResourceVersion ?? 5019,
                ["resource_path"] = resourcePaths?.Resource ?? resourceRoot.TrimEnd('/') + "/resource",
                ["master_path"] = resourcePaths?.Master ?? resourceRoot.TrimEnd('/') + "/master",
                ["assetbundle_path"] = resourcePaths?.AssetBundle ?? resourceRoot.TrimEnd('/') + "/assetbundle/android"
            },
            _ => new()
            {
                ["user"] = PlayerState.user.DeepClone(),
                ["device_id"] = SessionState.account!.DeviceId,
                ["is_first_login"] = false,
                ["is_first_share"] = false,
                ["public_id"] = SessionState.account.PublicId,
                ["openid"] = SessionState.account.UserId,
                ["user_id"] = SessionState.account.UserId,
                ["loading_info"] = new JsonArray(),
                ["retrygacha_loginevent"] = new JsonArray(),
                ["loginbonus"] = null,
                ["event_loginbonus"] = new JsonArray(),
                ["random_loginbonus"] = new JsonArray(),
                ["slot_loginbonus"] = new JsonArray(),
                ["information_update_time"] = PlayerState.Clock.GetUtcNow().ToString("O"),
                ["is_FdOpen"] = 0
            }
        });
        if ((api == "home" || api == "battle_start" && dailyReset) && PlayerState.user[QuestsService.DailyQuestList] is JsonArray dailyQuests)
        {
            body["user_update"] ??= new JsonObject();
            body["user_update"]![QuestsService.DailyQuestList] = dailyQuests.DeepClone();
        }

        body["session_id"] = SessionState.session;
        body["encryption_key"] = InitialKey;
        body["response_time"] = PlayerState.Clock.GetUtcNow().ToUnixTimeSeconds();
        if (api is "logout" or "delete_account")
            SessionState.session = null;
        return new(0, body);
    }

    internal static string VersionManifest(int appVersion) => new JsonObject
    {
        ["datas"] = new JsonArray(new JsonObject { ["Channel"] = "", ["Platform"] = "Android", ["AppVersion"] = appVersion, ["Url"] = "", ["Title"] = "", ["Msg"] = "" })
    }.ToJsonString();
    private Dictionary<string, Func<IApiInput?, LocalReply?>> routes = null!;
}