namespace TouhouLastWorld;

public sealed class OfflineServer
{
    private readonly PlayerState PlayerState;
    private readonly SessionState SessionState;
    private readonly BattleModesService BattleModesService;
    private readonly DispatchService DispatchService;
    private readonly EconomyService EconomyService;
    private readonly EventsService EventsService;
    private readonly FacilitiesService FacilitiesService;
    private readonly MailService MailService;
    private readonly PicturesService PicturesService;
    private readonly ProfileService ProfileService;
    private readonly SocialService SocialService;
    private readonly TeamsService TeamsService;
    private readonly UnitsService UnitsService;
    private readonly GachaService GachaService;
    private readonly QuestsService QuestsService;
    private readonly ShopService ShopService;
    private readonly MissionsService MissionsService;
    private readonly RuntimeService RuntimeService;
    public OfflineServer(IStateStore store, string seed, string resourceRoot, LocalResourcePaths? resourcePaths = null, TimeProvider? clock = null, Random? random = null)
    {
        PlayerState = new(store, seed, clock, random);
        SessionState = new();
        BattleModesService = new(PlayerState);
        DispatchService = new(PlayerState);
        EconomyService = new(PlayerState);
        EventsService = new(PlayerState);
        FacilitiesService = new(PlayerState);
        MailService = new(PlayerState);
        PicturesService = new(PlayerState);
        ProfileService = new(PlayerState);
        SocialService = new(PlayerState);
        TeamsService = new(PlayerState);
        UnitsService = new(PlayerState);
        GachaService = new(EconomyService, PlayerState, SessionState);
        QuestsService = new(PlayerState, SessionState, TeamsService);
        ShopService = new(EconomyService, PlayerState, SessionState);
        MissionsService = new(EconomyService, PlayerState, QuestsService, SessionState);
        RuntimeService = new(new AccountService(PlayerState, SessionState, new SeedMigration(PlayerState)), SessionState, BattleModesService, DispatchService, EconomyService, EventsService, FacilitiesService, GachaService, MailService, MissionsService, PicturesService, PlayerState, ProfileService, QuestsService, ShopService, SocialService, TeamsService, UnitsService);
        RuntimeService.Initialize(resourceRoot, resourcePaths);
    }

    internal bool HasResourceOverrides => RuntimeService.HasResourceOverrides;

    public const string InitialKey = RuntimeService.InitialKey;
    public LocalReply Dispatch(GameRequest request) => RuntimeService.Dispatch(request);
    public static string VersionManifest(int appVersion) => RuntimeService.VersionManifest(appVersion);
    public int PendingArenaCategory => BattleModesService.PendingArenaCategory;
    public int DailyArenaRewardDay => BattleModesService.DailyArenaRewardDay;
    public int PendingTowerId => BattleModesService.PendingTowerId;

    public int DispatchDefinitionId(long id) => DispatchService.DispatchDefinitionId(id);
    public int ActiveDispatchDefinitionId(long id) => DispatchService.ActiveDispatchDefinitionId(id);
    public int LectureExpansionRank => FacilitiesService.LectureExpansionRank;
    public int LectureFoodExpansionRank => FacilitiesService.LectureFoodExpansionRank;
    public int TrainingExpansionRank => FacilitiesService.TrainingExpansionRank;

    public LocalIdleSet? IdleSelection(int type, int number) => FacilitiesService.IdleSelection(type, number);
    public int IdleStage(int content) => FacilitiesService.IdleStage(content);
    public int[] PendingIntroPictures => GachaService.PendingIntroPictures;

    internal const int DailySweepRewardType = MissionsService.DailySweepRewardType;
    internal const int DailyQuestMissionType = MissionsService.DailyQuestMissionType;
    internal const int NoviceMissionType = MissionsService.NoviceMissionType;
    internal const int UnitLevelMissionType = MissionsService.UnitLevelMissionType;
    internal const int UnitLimitBreakMissionType = MissionsService.UnitLimitBreakMissionType;
    internal const int UnitAwakeningMissionType = MissionsService.UnitAwakeningMissionType;
    internal const int BattleWinMissionType = QuestsService.BattleWinMissionType;
    internal int PendingDailyQuestStage => QuestsService.PendingDailyQuestStage;
    internal int PendingQuestBattleEpisode => QuestsService.PendingQuestBattleEpisode;
    internal int PendingQuestBattleDifficulty => QuestsService.PendingQuestBattleDifficulty;
    internal long[] PendingQuestBattleOwnedUnits => QuestsService.PendingQuestBattleOwnedUnits;

    public int GoldExchangeDefinitionId(int slotId) => ShopService.GoldExchangeDefinitionId(slotId);
    public int FreeItemDefinitionId() => ShopService.FreeItemDefinitionId();
}
