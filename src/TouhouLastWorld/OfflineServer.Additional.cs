using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed partial class OfflineServer
{
    private static bool SupportsAdditional(string api) => api is "gold_box_renovation_start" or "gold_box_renovation_end" or "gather_phantasm_point" or "gather_gold" or "friend/receive" or "friend/give" or "delete_account" or "get_tower_unit_info" or "tower_top" or "tutorial_update" or "logout" or "set_age"
        or "unit_level_limitbreak" or "unit_rare_awaken" or "unit_rank_promote" or "unit_rank_promote_item_set" or "phantasm_point_buy" or "phantasm_box_renovation_start" or "phantasm_box_renovation_end" or "friend/search" or "user_info_update" or "get_profile_setting" or "update_profile_setting"
        or "unit_recycle" or "picture_recycle" or "picture_potential_limitbreak" or "unit_destiny_value_up" or "picture_spec_actualize_chemicals" or "picture_spec_actualize" or "limitbreak_strong_fragrantwood" or "unit_extra_spec_actualize" or "unit_spec_actualize" or "update_profile_setting2" or "get_auto_replay_info" or "update_auto_replay_info"
        or "pass_card/buy_goods" or "pass_card/user_orb_add_level" or "pass_card/collect_reward" or "pass_card/collect_exp" or "get_exchange_code_reward" or "picture_book/get_reward" or "dispatch_bulk_end" or "dispatch_end" or "dispatch_start" or "personal_info_update" or "anniversary_info_update" or "item_exchange" or "auto_replay_start" or "get_multiple_spirits_weekly_replay_info" or "update_multiple_spirits_weekly_replay_info"
        or "get_clear_history_playerinfo" or "get_clear_history_teaminfo" or "tutorial_first_gacha_confirm" or "tutorial_first_gacha_do" or "retry_gacha_fix" or "first_draw_save" or "first_draw_data" or "exchange" or "update_gold_exchange_shop_list" or "unit_reincarnation_release" or "votive_picture_exchange" or "multiple_spirits_weekly_top" or "multiple_spirits_weekly_battle_start" or "arena_battle_end" or "arena_battle_start" or "event_point_gacha_do" or "fudai/fudai_draw" or "fudai/fudai_info" or "get_event_total_point_reward_all" or "get_event_total_point_reward" or "gain_free_item" or "pass_card/show_reward" or "pass_card/get_level_gift" or "get_series_battle_replay_info" or "update_series_battle_replay_info"
        or "gacha_point_exchange" or "mini_game_top" or "get_series_battle_ranking_teaminfo" or "battle_pass_receive_reward" or "battle_pass_mission_end" or "mission_end" or "tower_unit_recovery" or "friend_get_history" or "get_replay_info" or "update_replay_info" or "achievement_new_update" or "team_update"
        or "friend_setting_helper" or "home_select_unit_update" or "home_bgm_update" or "unit_bgm_update"
        or "favorite_unit_update" or "update_home_motion_mode" or "read_unit_talk" or "mail_delete"
        or "user_twitter_info_update" or "user_google_info_delete" or "user_apple_info_delete" or "user_twitter_info_delete" or "lecture_room_unit_trust_up" or "unit_voice_update" or "transcribe_kpi" or "lecture_room_unit_remove"
        or "lecture_room_update" or "lecture_room_unit_set" or "lecture_room_unit_expansion"
        or "lecture_room_food_expansion" or "training_room_expansion" or "training_room_update"
        or "training_room_cancel" or "training_room_start" or "unit_equip_picture_reset" or "unit_equip_picture"
        or "unit_equip_costume" or "dispatch_cancel" or "rhythm_game_start" or "rhythm_game_end"
        or "achievement_select" or "roguelite_deck_save" or "roguelite_deck_call" or "roguelite_top"
        or "tower_battle_end" or "tower_battle_start" or "series_battle_end" or "series_battle_start" or "comic_read_end" or "comic_get_pages" or "series_battle_reset" or "series_battle_team_update"
        or "lecture_room_food_addition" or "lecture_room_food_distribution" or "training_room_shortened_finish_time"
        or "avatar/preview" or "avatar/update" or "friend/refuse_apply" or "friend/accept_apply" or "friend/apply"
        or "friend/info" or "friend_delete" or "friend_request_cancel" or "friend_request" or "friend_approve" or "reset_point_allocation" or "allocate_points"
        or "mail_receive" or "friend_helper_select" or "get_quest_cleared_team_info" or "afk_receive_drop" or "afk_complete_upgrade" or "afk_set" or "episode_end" or "event_top" or "daily_arena_top" or "weekly_arena_top" or "weekly_arena_battle_score_reset" || QueryTemplates.ContainsKey(api);

    private LocalReply? DispatchAdditional(string api, LocalArguments? arguments) => api switch
    {
        "friend/give" => GiveContactGift(arguments),
        "item_exchange" => ExchangeItems(arguments),
        "pass_card/buy_goods" => BuyPassGoods(arguments),
        "pass_card/user_orb_add_level" => BuyPassLevels(arguments),
        "pass_card/get_level_gift" => BuyPassLevels(arguments, gift: true),
        "pass_card/show_reward" => ShowPassRewards(arguments),
        "gain_free_item" => GainFreeItem(arguments),
        "get_event_total_point_reward" => ClaimEventPointReward(arguments),
        "get_event_total_point_reward_all" => ClaimEventPointReward(arguments, all: true),
        "multiple_spirits_weekly_top" => VersusOverview(arguments?.Ids),
        "multiple_spirits_weekly_battle_start" => StartVersus(arguments?.VersusStart),
        "arena_battle_end" => EndArena(arguments?.ArenaEnd),
        "arena_battle_start" => StartArena(arguments?.ArenaStart),
        "event_point_gacha_do" => DrawEventPoints(arguments),
        "fudai/fudai_draw" => DrawTreasure(arguments),
        "fudai/fudai_info" => TreasureOverview(arguments),
        "votive_picture_exchange" => ExchangeTicket(arguments),
        "unit_reincarnation_release" => ReleaseAutomaticNode(arguments),
        "update_gold_exchange_shop_list" => RefreshExchangeShop(arguments),
        "exchange" => ExchangeGoldGoods(arguments),
        "first_draw_data" => PrepareFirstDraw(arguments),
        "first_draw_save" => ConfirmFirstDraw(arguments),
        "retry_gacha_fix" => FixRetryDraw(arguments),
        "gacha_point_exchange" => ExchangeDrawPoints(arguments?.PointExchange),
        "tutorial_first_gacha_do" => TutorialInventory(),
        "tutorial_first_gacha_confirm" => TutorialInventory(restore: true),
        "get_clear_history_teaminfo" => HistoryTeam(arguments),
        "get_clear_history_playerinfo" => HistoryPlayer(arguments),
        "friend_get_history" => HistoryPlayer(arguments, summaryOnly: true),
        "tower_unit_recovery" => RecoverTowerUnits(arguments),
        "mission_end" => ReceiveMission(arguments?.MissionClaim),
        "mission_top" => MissionOverview(),
        "battle_pass_mission_end" => ReceiveBattlePassMission(arguments?.BattlePassClaim),
        "battle_pass_top" => BattlePassOverview(),
        "battle_pass_receive_reward" => ReceiveBattlePassRewards(arguments?.BattlePassRewards),
        "get_series_battle_ranking_teaminfo" => RankingHistoryTeam(arguments),
        "mini_game_top" => MiniGameOverview(arguments),
        "pass_card/collect_exp" => CollectPassExperience(arguments),
        "pass_card/collect_reward" => CollectPassRewards(arguments),
        "pass_card/show_task" or "pass_card/show_shop" => PassOverview(),
        "get_exchange_code_reward" => ExchangeLocalCode(arguments),
        "picture_book/get_reward" => ReceiveBookReward(arguments),
        "picture_book/get_user_picture_book_list" => new(0, new() { ["user_completed_info"] = user["_local_picture_book"]?.DeepClone() ?? new JsonArray() }),
        "dispatch_start" => StartDispatch(arguments),
        "dispatch_end" => EndDispatch(arguments),
        "dispatch_bulk_end" => EndDispatchBatch(),
        "get_dispatch_list" => DispatchOverview(),
        "anniversary_info_update" => SaveAnniversaryInfo(arguments),
        "personal_info_update" => SaveContactDetails(arguments?.ContactDetails),
        "personal_info_get" => new(0, user["_local_personal_info"]?.DeepClone().AsObject() ?? QueryTemplates["personal_info_get"].DeepClone().AsObject()),
        "anniversary_info_get" => new(0, user["_local_anniversary_info"]?.DeepClone().AsObject() ?? new JsonObject { ["mail_address"] = "" }),
        "auto_replay_start" => AutoReplayUnavailable(arguments),
        "user_twitter_info_delete" => DeleteLocalTwitterRelation(arguments?.Text),
        "user_twitter_info_update" => UpdateLocalTwitterRelation(arguments?.Text),
        "user_apple_info_delete" => ClearLocalRelation("apple_relation"),
        "user_google_info_delete" => ClearLocalRelation("google_relation"),
        "lecture_room_unit_trust_up" => ReceiveLectureTrust(arguments),
        "picture_recycle" => RecyclePictures(arguments),
        "unit_recycle" => RecycleUnits(arguments),
        "picture_potential_limitbreak" => LimitBreakPicture(arguments),
        "unit_destiny_value_up" => IncreaseDestiny(arguments),
        "unit_rank_promote_item_set" => SetRankMaterial(arguments),
        "unit_rank_promote" => PromoteRank(arguments),
        "unit_rare_awaken" => AwakenUnit(arguments),
        "unit_level_limitbreak" => LimitBreakUnit(arguments),
        "unit_spec_actualize" => StrengthenUnit(arguments),
        "unit_extra_spec_actualize" => StrengthenUnit(arguments, true),
        "limitbreak_strong_fragrantwood" => UnlockExtraStat(arguments),
        "picture_spec_actualize" or "picture_spec_actualize_chemicals" => StrengthenPicture(arguments),
        "phantasm_point_buy" => BuyPoints(arguments?.PointPurchase),
        "gather_gold" => GatherGold(arguments),
        "gold_box_renovation_end" => CompleteBoxRenovation("gold_box", arguments),
        "phantasm_box_renovation_end" => CompleteBoxRenovation("phantasm_box", arguments),
        "phantasm_box_renovation_start" => StartBoxRenovation("phantasm_box", arguments?.Renovation),
        "gold_box_renovation_start" => StartBoxRenovation("gold_box", arguments?.Renovation),
        "gather_phantasm_point" => GatherPoints(arguments),
        "friend/receive" => ReceiveContactGift(arguments),
        "delete_account" => DeleteAccount(),
        "tower_top" => TowerOverview(),
        "get_tower_unit_info" => TowerUnits(),
        "mail_receive" => ReceiveMail(arguments),
        "friend_helper_select" => HelperCandidates(arguments?.Value),
        "get_quest_cleared_team_info" => ClearedTeam(arguments?.ReplayMeasure),
        "afk_set" => SetIdleSlot(arguments?.Idle),
        "afk_complete_upgrade" => CompleteIdleSlot(arguments),
        "afk_receive_drop" => ReceiveIdleDrop(),
        "episode_end" => CompleteEpisode(arguments),
        "event_top" => EventOverview(),
        "daily_arena_top" => DailyOverview(),
        "weekly_arena_top" => WeeklyOverview(),
        "weekly_arena_battle_score_reset" => ResetWeeklyScore(arguments?.Value),
        "friend_request" => SendContactRequest(arguments),
        "friend_get_candidate_list" => new(0, new() { ["helper_list"] = user["_local_friend_candidates"]?.DeepClone() ?? new JsonArray() }),
        "friend_approve" => ApproveContactRequest(arguments),
        "friend_request_cancel" => CancelContactRequest(arguments),
        "friend_delete" => DeleteContact(arguments?.Text),
        "reset_point_allocation" => ResetPoints(arguments?.PointReset),
        "allocate_points" => AllocatePoints(arguments?.PointAllocation),
        "friend/apply" => ApplyContact(arguments),
        "friend/recommend" => new(0, new() { ["data"] = user["_local_friend_recommendations"]?.DeepClone() ?? new JsonArray() }),
        "friend/info" => ContactInfo(arguments?.Text),
        "friend/refuse_apply" => RefuseContactApplications(arguments),
        "friend/accept_apply" => AcceptContactApplication(arguments),
        "friend/apply_list" => new(0, new() { ["data"] = user["_local_friend_applications"]?.DeepClone() ?? new JsonArray() }),
        "friend/list" => new(0, new()
        {
            ["data"] = user["_local_friends"]?.DeepClone() ?? new JsonArray(),
            ["request_tips"] = user["_local_friend_applications"] is JsonArray { Count: > 0 } ? 1 : 0
        }),
        "avatar/list" => new(0, new() { ["avatar_list"] = user["HeadListItemList"]?.DeepClone() ?? new JsonArray() }),
        "avatar/preview" => PreviewAvatar(arguments?.Value),
        "avatar/update" => SelectAvatar(arguments?.Value),
        "training_room_start" => StartTraining(arguments?.Training),
        "training_room_shortened_finish_time" => ShortenTraining(arguments),
        "lecture_room_food_distribution" => DistributeFood(arguments?.FoodDistribution),
        "lecture_room_food_addition" => PurchaseFood(arguments?.FoodPurchase),
        "series_battle_team_update" => UpdateSeriesTeams(arguments?.SeriesTeams),
        "series_battle_reset" => ResetSeries(),
        "tower_battle_start" => StartTower(arguments?.TowerStart),
        "tower_battle_end" => EndTower(arguments?.TowerEnd),
        "series_battle_start" => StartSeries(arguments?.SeriesStart),
        "series_battle_end" => EndSeries(arguments?.SeriesEnd),
        "comic_get_pages" => AcquirePages(arguments?.Pages),
        "comic_read_end" => ReadPages(arguments),
        "roguelite_top" => DeckOverview(arguments?.Value),
        "roguelite_deck_save" => SaveDeck(arguments),
        "roguelite_deck_call" => CallDeck(arguments?.Value),
        "rhythm_game_start" => StartRhythm(arguments),
        "achievement_select" => SelectAchievement(arguments?.Value),
        "achievement_top" => AchievementOverview(),
        "rhythm_game_end" => EndRhythm(arguments?.Rhythm),
        "rhythm_game_top" => new(0, new() { ["clear_info"] = user["_local_rhythm_scores"]?.DeepClone() ?? new JsonArray() }),
        "tutorial_update" => UpdateProgress(arguments?.Value),
        "read_unit_talk" => MarkTalkRead(arguments?.Value),
        "mail_delete" => DeleteReadMail(),
        "unit_voice_update" => SetVoice(arguments),
        "lecture_room_unit_remove" => RemoveLectureUnit(arguments?.EntityId),
        "lecture_room_unit_set" => AddLectureUnit(arguments),
        "lecture_room_unit_expansion" => ExpandFacility(arguments, "lecture_room", "unit"),
        "lecture_room_food_expansion" => ExpandFacility(arguments, "lecture_room", "food"),
        "training_room_expansion" => ExpandFacility(arguments, "training_room"),
        "training_room_cancel" => CancelTraining(arguments?.Value),
        "dispatch_cancel" => CancelDispatch(arguments?.EntityId),
        "unit_equip_picture_reset" => ResetEquipment(arguments?.EntityId),
        "unit_equip_picture" => SetEquipment(arguments),
        "unit_equip_costume" => SetCostumes(arguments?.Costumes),
        "training_room_update" => new(0, new()
        {
            ["user_update"] = new JsonObject { ["training_room"] = user["training_room"]!.DeepClone() }
        }),
        "lecture_room_update" => new(0, new()
        {
            ["spend_info"] = new JsonObject { ["passage_time"] = 0, ["food"] = 0, ["exp"] = 0 },
            ["user_update"] = new JsonObject { ["lecture_room"] = user["lecture_room"]!.DeepClone() }
        }),
        "transcribe_kpi" => arguments?.Value is 1 or 2 ? new(0, new())
            : new(400, null, "Unknown local telemetry event."),
        "favorite_unit_update" => UpdateHome(arguments?.Home),
        "home_select_unit_update" => SelectHomeUnit(arguments?.Value),
        "unit_bgm_update" => SetUnitPreference(arguments, "select_bgm"),
        "update_home_motion_mode" => SetUnitPreference(arguments, "home_motion"),
        "home_bgm_update" => arguments?.Value is >= 0 ? SetUserInteger("select_bgm", arguments.Value.Value)
            : new(400, null, "Nonnegative music selection required."),
        "team_update" => UpdateTeam(arguments?.Team),
        "friend_setting_helper" => SetHelper(arguments),
        "friend_top" => ContactOverview(),
        "achievement_new_update" => ClearAchievementMarkers(arguments?.Ids),
        "get_replay_info" => QuestReplay(arguments, false),
        "update_replay_info" => QuestReplay(arguments, true),
        "get_series_battle_replay_info" => GetSeriesReplays(arguments),
        "update_series_battle_replay_info" => SaveSeriesReplay(arguments),
        "get_auto_replay_info" => ReplayMeasurement(arguments?.ReplayMeasure, false),
        "get_multiple_spirits_weekly_replay_info" => GetStoredReplay(arguments?.EntityId),
        "update_multiple_spirits_weekly_replay_info" => SaveReplay(arguments?.ReplayContent),
        "update_auto_replay_info" => ReplayMeasurement(arguments?.ReplayMeasure, true),
        "update_profile_setting" => SaveProfileLayout(arguments?.Text),
        "update_profile_setting2" => new(0, new()
        {
            ["text_value"] = arguments?.Text is null || arguments.Text.Any(char.IsControl) ? 1 : 0
        }),
        "get_profile_setting" => new(0, new()
        {
            ["profile_setting"] = user["_local_profile_setting"]?.GetValue<string>() ?? "",
            ["achievement_id_list"] = new JsonArray()
        }),
        "user_info_update" => UpdateProfile(arguments?.Profile),
        "friend/search" => new(2000, null, "No matching local contact."),
        "set_age" => arguments?.Value is >= 1 and <= 3
            ? SetUserInteger("age_limit_type", arguments.Value.Value)
            : new(400, null, "Age category must be 1, 2 or 3."),
        "logout" => arguments?.Text == account!.UserId
            ? new(0, new()) : new(403, null, "Local identity does not match."),
        _ => QueryTemplates.TryGetValue(api, out var body) ? new(0, body.DeepClone().AsObject()) : null
    };

    private LocalReply HelperCandidates(int? capacity)
    {
        if (capacity is null or <= 0) return new(503, null, "Contact capacity unavailable.");
        return new(0, new()
        {
            ["helper_list"] = new JsonArray(), ["daily_fp_gain_limit_flag"] = 0,
            ["is_friend_max"] = ((user["_local_friend_players"] as JsonArray)?.Count ?? 0) >= capacity ? 1 : 0,
            ["friend_request_count"] = (user["_local_friend_requests"] as JsonArray)?.Count ?? 0
        });
    }

    private LocalReply CompleteEpisode(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue)
            return new(400, null, "Positive episode ID required.");
        if (request.Value is null) return new(503, null, "Episode master data unavailable.");
        if (request.Value != 1) return new(404, null, "Story episode not found.");
        var updated = user.DeepClone().AsObject();
        var episodes = updated["quest_playdata"] as JsonArray ?? new JsonArray();
        var episode = episodes.FirstOrDefault(entry => entry?["episode_id"]?.GetValue<int>() == request.EntityId) as JsonObject;
        if (episode is null)
        {
            episode = new JsonObject { ["episode_id"] = (int)request.EntityId.Value };
            foreach (var difficulty in new[] { "normal", "hard", "lunatic" })
                episode[difficulty] = new JsonObject { ["status"] = -2, ["battle_challenge"] = new JsonArray() };
            episodes.Add(episode);
        }
        episode["scenario_status"] = Math.Max(1, episode["scenario_status"]?.GetValue<int>() ?? 0);
        if (episodes.Parent is null) updated["quest_playdata"] = episodes;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["quest_playdata"] = episodes.DeepClone() } });
    }

    private LocalReply EventOverview()
    {
        if (user["_local_event_overview"] is not JsonObject overview)
            return new(503, null, "Local event snapshot unavailable.");
        var body = new JsonObject();
        foreach (var field in new[] { "event_point_gacha_list", "event_special_mission", "event_total_point_reward" })
        {
            if (overview[field] is not JsonArray entries)
                return new(503, null, "Local event snapshot incomplete.");
            body[field] = entries.DeepClone();
        }
        return new(0, body);
    }

    private LocalReply DailyOverview()
    {
        if (user["_local_daily_arena"] is not JsonObject overview
            || overview["custom_battle_id"]?.GetValue<int>() is not > 0
            || overview["enemy_info"] is not JsonArray { Count: > 0 } waves)
            return new(503, null, "Local daily configuration unavailable.");
        if (waves.Any(wave => wave is not JsonObject || wave["enemy1"] is not JsonObject
            || wave["enemy2"] is not JsonObject || wave["enemy3"] is not JsonObject))
            return new(503, null, "Local daily enemy configuration incomplete.");
        var body = new JsonObject { ["enemy_info"] = waves.DeepClone() };
        foreach (var field in new[] { "custom_battle_id", "clear_diffculty", "reward_day_count", "enemy_level", "last_clear_difficulty" })
            body[field] = overview[field]?.DeepClone() ?? JsonValue.Create(0);
        return new(0, body);
    }

    private LocalReply WeeklyOverview()
    {
        if (user["_local_weekly_arena"] is not JsonObject overview
            || overview["custom_battle_id"]?.GetValue<int>() is not > 0
            || overview["weekly_battle_data"] is not JsonObject battles)
            return new(503, null, "Local weekly configuration unavailable.");
        for (var number = 1; number <= 4; number++)
        {
            if (battles[$"battle_number{number}"] is not JsonObject battle
                || battle["buff_role_type"] is not JsonArray || battle["wave_enemy_info"] is not JsonArray
                || battle["unit_list"] is not JsonArray || battle["user_unit_list"] is not JsonArray)
                return new(503, null, "Local weekly battle configuration incomplete.");
        }
        return new(0, new()
        {
            ["custom_battle_id"] = overview["custom_battle_id"]!.DeepClone(),
            ["weekly_battle_data"] = battles.DeepClone(),
            ["total_battle_score"] = overview["total_battle_score"]?.DeepClone() ?? JsonValue.Create(0)
        });
    }

    private LocalReply ResetWeeklyScore(int? number)
    {
        if (number is null or < 1 or > 4) return new(400, null, "Weekly battle number must be 1 to 4.");
        var updated = user.DeepClone().AsObject();
        var overview = updated["_local_weekly_arena"] as JsonObject;
        var battles = overview?["weekly_battle_data"] as JsonObject;
        if (battles?[$"battle_number{number}"] is not JsonObject battle)
            return new(404, null, "Local weekly battle not found.");
        battle["total_score"] = 0;
        battle["unit_list"] = new JsonArray();
        battle["user_unit_list"] = new JsonArray();
        Save(updated);
        return new(0, new());
    }

    private LocalReply ApplyContact(LocalArguments? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text)) return new(400, null, "Contact ID required.");
        if (request.Value is null or <= 0) return new(503, null, "Contact capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var friends = updated["_local_friends"] as JsonArray ?? new JsonArray();
        if (friends.Count >= request.Value) return new(20007, null, "Local contact list is full.");
        if (friends.Any(entry => entry?["public_id"]?.GetValue<string>() == request.Text))
            return new(409, null, "Local contact already exists.");
        var candidate = (updated["_local_friend_recommendations"] as JsonArray)?
            .FirstOrDefault(entry => entry?["public_id"]?.GetValue<string>() == request.Text);
        if (candidate is null) return new(404, null, "Local candidate not found.");
        var outgoing = updated["_local_friend_outgoing"] as JsonArray ?? new JsonArray();
        if (outgoing.Any(entry => entry?["public_id"]?.GetValue<string>() == request.Text))
            return new(409, null, "Local application already exists.");
        outgoing.Add(candidate.DeepClone());
        if (outgoing.Parent is null) updated["_local_friend_outgoing"] = outgoing;
        Save(updated);
        return new(0, new());
    }

    private LocalReply SendContactRequest(LocalArguments? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text)) return new(400, null, "Player ID required.");
        if (request.Value is null or <= 0 || request.EntityId is null or <= 0)
            return new(503, null, "Contact and request capacities unavailable.");
        var updated = user.DeepClone().AsObject();
        var outgoing = updated["_local_friend_requests"] as JsonArray ?? new JsonArray();
        var friends = updated["_local_friend_players"] as JsonArray ?? new JsonArray();
        var candidate = (updated["_local_friend_candidates"] as JsonArray)?
            .FirstOrDefault(entry => entry?["player_id"]?.GetValue<string>() == request.Text);
        if (friends.Count >= request.EntityId)
            return new(0, new() { ["result"] = 4, ["friend_request_list"] = outgoing.DeepClone() });
        if (candidate is null || outgoing.Count >= request.Value
            || outgoing.Any(entry => entry?["player_id"]?.GetValue<string>() == request.Text)
            || friends.Any(entry => entry?["player_id"]?.GetValue<string>() == request.Text))
            return new(0, new() { ["result"] = 1, ["friend_request_list"] = outgoing.DeepClone() });
        outgoing.Add(candidate.DeepClone());
        if (outgoing.Parent is null) updated["_local_friend_requests"] = outgoing;
        Save(updated);
        return new(0, new() { ["result"] = 0, ["friend_request_list"] = outgoing.DeepClone() });
    }

    private LocalReply ApproveContactRequest(LocalArguments? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text)) return new(400, null, "Player ID required.");
        if (request.Value is null or <= 0) return new(503, null, "Contact capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var incoming = updated["_local_friend_approval_pending"] as JsonArray;
        var entry = incoming?.FirstOrDefault(value => value?["player_id"]?.GetValue<string>() == request.Text);
        var friends = updated["_local_friend_players"] as JsonArray ?? new JsonArray();
        if (entry is null || friends.Count >= request.Value
            || friends.Any(value => value?["player_id"]?.GetValue<string>() == request.Text))
            return new(0, new() { ["result"] = 1, ["friend_list"] = friends.DeepClone() });
        friends.Add(entry.DeepClone());
        incoming!.Remove(entry);
        if (friends.Parent is null) updated["_local_friend_players"] = friends;
        Save(updated);
        return new(0, new() { ["result"] = 0, ["friend_list"] = friends.DeepClone() });
    }

    private LocalReply CancelContactRequest(LocalArguments? request)
    {
        if (request?.Value is not (0 or 1) || (request.Value == 0 && string.IsNullOrWhiteSpace(request.Text)))
            return new(400, null, "Player ID and valid cancellation mode required.");
        var updated = user.DeepClone().AsObject();
        var incoming = updated["_local_friend_approval_pending"] as JsonArray;
        var outgoing = updated["_local_friend_requests"] as JsonArray;
        if (request.Value == 1)
            incoming?.Clear();
        else
        {
            var received = incoming?.FirstOrDefault(entry => entry?["player_id"]?.GetValue<string>() == request.Text);
            var sent = outgoing?.FirstOrDefault(entry => entry?["player_id"]?.GetValue<string>() == request.Text);
            if (received is null && sent is null) return new(0, new() { ["result"] = 1 });
            if (received is not null) incoming!.Remove(received);
            if (sent is not null) outgoing!.Remove(sent);
        }
        Save(updated);
        return new(0, new() { ["result"] = 0 });
    }

    private LocalReply DeleteContact(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return new(400, null, "Player ID required.");
        var updated = user.DeepClone().AsObject();
        var entries = updated["_local_friend_players"] as JsonArray ?? new JsonArray();
        var entry = entries.FirstOrDefault(value => value?["player_id"]?.GetValue<string>() == id);
        if (entry is null) return new(0, new() { ["result"] = 1, ["friend_list"] = entries.DeepClone() });
        entries.Remove(entry);
        Save(updated);
        return new(0, new() { ["result"] = 0, ["friend_list"] = entries.DeepClone() });
    }

    private LocalReply ContactInfo(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return new(400, null, "Contact ID required.");
        var friend = (user["_local_friends"] as JsonArray)?.FirstOrDefault(entry => entry?["public_id"]?.GetValue<string>() == id);
        if (friend is null) return new(404, null, "Local contact not found.");
        return new(0, new()
        {
            ["unit_get_count"] = friend["unit_get_count"]?.GetValue<int>() ?? 0,
            ["quest_complete_count"] = friend["quest_complete_count"]?.GetValue<int>() ?? 0,
            ["dispatch_complete_count"] = friend["dispatch_complete_count"]?.GetValue<int>() ?? 0
        });
    }

    private LocalReply AcceptContactApplication(LocalArguments? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text)) return new(400, null, "Contact ID required.");
        if (request.Value is null or <= 0) return new(503, null, "Contact capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var applications = updated["_local_friend_applications"] as JsonArray;
        var entry = applications?.FirstOrDefault(value => value?["public_id"]?.GetValue<string>() == request.Text);
        if (entry is null) return new(404, null, "Local contact application not found.");
        var friends = updated["_local_friends"] as JsonArray ?? new JsonArray();
        if (friends.Any(value => value?["public_id"]?.GetValue<string>() == request.Text))
            return new(409, null, "Local contact already exists.");
        if (friends.Count >= request.Value) return new(20007, null, "Local contact list is full.");
        var friend = entry.DeepClone().AsObject();
        friend["profile"] = "";
        friend["intimacy"] = 0;
        friends.Add(friend);
        applications!.Remove(entry);
        if (friends.Parent is null) updated["_local_friends"] = friends;
        Save(updated);
        return new(0, new());
    }

    private LocalReply RefuseContactApplications(LocalArguments? request)
    {
        if (request?.Value is not (0 or 1) || (request.Value == 0 && string.IsNullOrWhiteSpace(request.Text)))
            return new(400, null, "Contact ID or all selection required.");
        var updated = user.DeepClone().AsObject();
        var entries = updated["_local_friend_applications"] as JsonArray ?? new JsonArray();
        if (request.Value == 1)
            entries.Clear();
        else
        {
            var entry = entries.FirstOrDefault(value => value?["public_id"]?.GetValue<string>() == request.Text);
            if (entry is null) return new(404, null, "Local contact application not found.");
            entries.Remove(entry);
        }
        if (entries.Parent is null) updated["_local_friend_applications"] = entries;
        Save(updated);
        return new(0, new());
    }

    private LocalReply SelectAvatar(int? id)
    {
        if (id is null or <= 0) return new(400, null, "Positive avatar ID required.");
        var entry = user["HeadListItemList"]!.AsArray().FirstOrDefault(value => value!["avatar_id"]!.GetValue<int>() == id);
        if (entry is null) return new(404, null, "Avatar entry not found.");
        if (entry["is_clear"]?.GetValue<bool>() != true) return new(403, null, "Avatar is locked.");
        var filename = entry["filename"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(filename)) return new(409, null, "Avatar resource unavailable.");
        var updated = user.DeepClone().AsObject();
        updated["avatar_id"] = id.Value;
        updated["avatar_filename"] = filename;
        updated["avatar_title"] = entry["title"]?.GetValue<string>() ?? "";
        updated["avatar_desc"] = entry["desc"]?.GetValue<string>() ?? "";
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject
            {
                ["avatar_id"] = id.Value, ["avatar_filename"] = filename,
                ["avatar_title"] = updated["avatar_title"]!.DeepClone(), ["avatar_desc"] = updated["avatar_desc"]!.DeepClone()
            }
        });
    }

    private LocalReply PreviewAvatar(int? id)
    {
        if (id is null or <= 0) return new(400, null, "Positive avatar ID required.");
        var updated = user.DeepClone().AsObject();
        var entries = updated["HeadListItemList"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["avatar_id"]!.GetValue<int>() == id);
        if (entry is null) return new(404, null, "Avatar entry not found.");
        entry["is_preview"] = true;
        var unread = entries.Any(value => value!["is_clear"]!.GetValue<bool>() && !value["is_preview"]!.GetValue<bool>());
        updated["isShowRedPoint"] = unread;
        Save(updated);
        return new(0, new() { ["avatar_tips"] = unread ? 1 : 0 });
    }

    private LocalReply ResetSeries()
    {
        var updated = user.DeepClone().AsObject();
        var state = updated["series_battle_status"]!.AsObject();
        foreach (var field in new[] { "episode_id", "difficulty", "progress", "stage_group_master_id", "manage_id", "value1" })
            state[field] = 0;
        state["use_replays"] = new JsonArray();
        updated.Remove("_local_series_pending");
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["series_battle_status"] = state.DeepClone() } });
    }

    private LocalReply AchievementOverview()
    {
        var body = QueryTemplates["achievement_top"].DeepClone().AsObject();
        body["achievement_list"] = user["_local_achievements"]?.DeepClone() ?? new JsonArray();
        return new(0, body);
    }

    private LocalReply SelectAchievement(int? id)
    {
        if (id is null or < 0) return new(400, null, "Nonnegative achievement ID required.");
        if (id != 0 && !(user["_local_achievements"] as JsonArray ?? new JsonArray()).Any(entry =>
                entry?["achievement_id"]?.GetValue<int>() == id && entry["is_acquired"]?.GetValue<int>() == 1))
            return new(403, null, "Achievement not acquired.");
        var updated = user.DeepClone().AsObject();
        updated["achievement"]!["selected_achievement_id"] = id.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["achievement"] = updated["achievement"]!.DeepClone() } });
    }

    private LocalReply SetVoice(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value is null or < 0)
            return new(400, null, "Positive unit ID and nonnegative voice selection required.");
        var updated = user.DeepClone().AsObject();
        var album = updated["album"]!.AsObject();
        var entries = album["unit_list"]!.AsArray();
        var entry = entries.FirstOrDefault(value => value!["unit_id"]!.GetValue<int>() == request.EntityId);
        if (entry is null)
        {
            if (!updated["unit"]!["unit_list"]!.AsArray().Any(value => value!["unit_id"]!.GetValue<int>() == request.EntityId))
                return new(404, null, "Unit not found in local collection.");
            entry = new JsonObject
            {
                ["unit_id"] = (int)request.EntityId.Value, ["talk_rank"] = 0,
                ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray()
            };
            entries.Add(entry);
        }
        entry["voice_set_id"] = request.Value.Value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["album"] = album.DeepClone() } });
    }

    private JsonObject MailOverview() => new()
    {
        ["mail"] = user["_local_mail"]?.DeepClone() ?? new JsonArray()
    };

    private LocalReply DeleteReadMail()
    {
        var updated = user.DeepClone().AsObject();
        if (updated["_local_mail"] is JsonArray mail)
        {
            for (var index = mail.Count - 1; index >= 0; index--)
                if (mail[index]?["open_type"]?.GetValue<int>() > 0) mail.RemoveAt(index);
            Save(updated);
        }
        return new(0, MailOverview());
    }

    private LocalReply MarkTalkRead(int? id)
    {
        if (id is null or <= 0) return new(400, null, "Positive talk ID required.");
        var updated = user.DeepClone().AsObject();
        var read = updated["_local_read_talk_ids"] as JsonArray ?? new JsonArray();
        if (read.Any(entry => entry!.GetValue<int>() == id.Value)) return new(0, new());
        if (read.Parent is null) updated["_local_read_talk_ids"] = read;
        read.Add(id.Value);
        Save(updated);
        return new(0, new());
    }

    private LocalReply ContactOverview()
    {
        var body = QueryTemplates["friend_top"].DeepClone().AsObject();
        body["friend_list"] = user["_local_friend_players"]?.DeepClone() ?? new JsonArray();
        body["friend_request_list"] = user["_local_friend_requests"]?.DeepClone() ?? new JsonArray();
        body["friend_approval_pending_list"] = user["_local_friend_approval_pending"]?.DeepClone() ?? new JsonArray();
        body["helper_user_unit_id1"] = user["helper_user_unit_id1"]!.DeepClone();
        return new(0, body);
    }

    private LocalReply SetHelper(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 || request.Value is null or < 0)
            return new(400, null, "Positive helper ID and nonnegative power required.");
        if (!user["unit"]!["unit_list"]!.AsArray().Any(unit => unit!["user_unit_id"]!.GetValue<long>() == request.EntityId))
            return new(404, null, "Helper unit not found.");
        var updated = user.DeepClone().AsObject();
        updated["helper_user_unit_id1"] = request.EntityId.Value;
        updated["_local_helper_power"] = request.Value.Value;
        Save(updated);
        return new(0, new()
        {
            ["helper_user_unit_id1"] = request.EntityId.Value,
            ["user_update"] = new JsonObject { ["helper_user_unit_id1"] = request.EntityId.Value }
        });
    }

    private LocalReply ClearAchievementMarkers(long[]? ids)
    {
        if (ids is null || ids.Any(id => id <= 0 || id > int.MaxValue))
            return new(400, null, "Positive achievement IDs required.");
        var updated = user.DeepClone().AsObject();
        var achievement = updated["achievement"]!.AsObject();
        var recent = achievement["recently_received_achievement_id_list"]!.AsArray();
        var marked = ids.ToHashSet();
        for (var index = recent.Count - 1; index >= 0; index--)
            if (marked.Contains(recent[index]!.GetValue<long>())) recent.RemoveAt(index);
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["achievement"] = achievement.DeepClone() } });
    }

    private LocalReply UpdateProfile(LocalProfileUpdate? profile)
    {
        if (profile is null || (profile.PlayerName is null && profile.HeroName is null && profile.Comment is null)
            || (profile.PlayerName is not null && string.IsNullOrWhiteSpace(profile.PlayerName))
            || (profile.HeroName is not null && string.IsNullOrWhiteSpace(profile.HeroName))
            || new[] { profile.PlayerName, profile.HeroName, profile.Comment }.Any(text => text?.Any(char.IsControl) == true))
            return new(1000, null, "Invalid local profile text.");
        var delta = new JsonObject();
        if (profile.PlayerName is not null) delta["player_name"] = profile.PlayerName;
        if (profile.HeroName is not null) delta["hero_name"] = profile.HeroName;
        if (profile.Comment is not null) delta["comment"] = profile.Comment;
        var updated = user.DeepClone().AsObject();
        foreach (var (field, value) in delta) updated[field] = value!.DeepClone();
        Save(updated);
        return new(0, new() { ["edit_validity"] = 0, ["user_update"] = delta });
    }

    private LocalReply SetUserInteger(string field, int value)
    {
        var updated = user.DeepClone().AsObject();
        updated[field] = value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [field] = value } });
    }

    private LocalReply UpdateProgress(int? progress)
    {
        if (progress is null or < 0) return new(400, null, "Nonnegative progress required.");
        var updated = user.DeepClone().AsObject();
        var value = Math.Max(updated["progress_value"]!.GetValue<int>(), progress.Value);
        updated["progress_value"] = value;
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { ["progress_value"] = value } });
    }
}
