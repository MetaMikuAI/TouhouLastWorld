namespace TouhouLastWorld;

public sealed record RequestDraft(
    long? EntityId = null,
    int? Value = null,
    long[]? Ids = null,
    string? Text = null,

    LocalProfileUpdate? Profile = null,
    LocalReplayMeasure? ReplayMeasure = null,
    LocalReplayContent? ReplayContent = null,
    LocalTeamUpdate? Team = null,
    LocalHomeUpdate? Home = null,
    LocalEquipmentEntry[]? Equipment = null,
    LocalCostumeEntry[]? Costumes = null,
    LocalRhythmResult? Rhythm = null,
    LocalPagePurchase? Pages = null,
    LocalSeriesTeam[]? SeriesTeams = null,

    LocalFoodPurchase? FoodPurchase = null,
    LocalFoodDistribution? FoodDistribution = null,
    LocalTrainingStart? Training = null,
    LocalTrainingMaterial[]? TrainingMaterials = null,

    LocalPointReset? PointReset = null,
    LocalPointAllocation? PointAllocation = null,
    LocalIdleSet? Idle = null,

    LocalMailRules? MailRules = null,
    LocalBoxRenovation? Renovation = null,
    LocalBoxIncome? GoldIncome = null,
    LocalBoxIncome? SpiritIncome = null,
    LocalPointPurchase? PointPurchase = null,

    LocalRankMaterial? RankMaterial = null,
    LocalRankPromotion? RankPromotion = null,
    LocalAwakening? Awakening = null,
    LocalLimitBreak? LimitBreak = null,
    LocalStatMaterial[]? StatMaterials = null,
    LocalStatUnlock? StatUnlock = null,

    LocalPictureGrowth? PictureGrowth = null,
    LocalDestinyGrowth? DestinyGrowth = null,
    LocalPictureLimitBreak? PictureLimitBreak = null,
    LocalPictureRecycle? PictureRecycle = null,

    LocalItemExchange? ItemExchange = null,
    LocalContactDetails? ContactDetails = null,
    LocalDispatchStart? DispatchStart = null,
    LocalBookReward? BookReward = null,
    LocalCodeExchange? CodeExchange = null,

    LocalPassTask? PassTask = null,
    LocalPassReward[]? PassRewards = null,
    LocalPassGoods? PassGoods = null,
    LocalFreeItem? FreeItem = null,
    LocalEventRewards? EventRewards = null,
    LocalTicketExchange? TicketExchange = null,
    LocalAutomaticRelease? AutomaticRelease = null,
    LocalShopRefresh? ShopRefresh = null,
    LocalGoldExchange? GoldExchange = null,

    LocalIntroDefinitions? IntroDefinitions = null,
    LocalIntroGrant? IntroGrant = null,

    LocalHistoryQuery? HistoryQuery = null,
    LocalHistoryDefinitions? HistoryDefinitions = null,
    LocalUnitRecycle? UnitRecycle = null,
    LocalDispatchSettlement? DispatchSettlement = null,
    LocalRetryFix? RetryFix = null,

    LocalMissionClaim? MissionClaim = null,
    LocalDailyLoginMission? DailyLoginMission = null,
    LocalProgressMission[]? ProgressMissions = null,
    LocalDailyQuestMission? DailyQuestMission = null,

    LocalQuestBattleStart? QuestBattleStart = null,
    LocalQuestBattleEnd? QuestBattleEnd = null,
    LocalDailyQuestDay? DailyQuestDay = null,

    LocalBattlePassClaim? BattlePassClaim = null,
    LocalBattlePassRewards? BattlePassRewards = null,
    LocalBattlePassPeriod? BattlePassPeriod = null,
    LocalBattlePassDaily? BattlePassDaily = null,
    LocalPointExchange? PointExchange = null,

    LocalSeriesStart? SeriesStart = null,
    LocalSeriesEnd? SeriesEnd = null,
    LocalTowerStart? TowerStart = null,
    LocalTowerEnd? TowerEnd = null,

    LocalTreasureDraw? TreasureDraw = null,
    LocalEventDraw? EventDraw = null,

    LocalArenaStart? ArenaStart = null,
    LocalArenaEnd? ArenaEnd = null,

    LocalVersusStart? VersusStart = null,
    LocalVersusEnd? VersusEnd = null,

    LocalBatchSearch? Batch = null,
    LocalReroll? Reroll = null);
