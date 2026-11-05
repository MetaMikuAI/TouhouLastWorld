using Applet;
using Il2CppInterop.Runtime;
using static TouhouLastWorld.Patches.ActivePanels;
using PacketCallback = Il2CppSystem.Action<int, string>;
namespace TouhouLastWorld.Patches;

internal static class NetworkErrorRecovery
{
    internal static void Handle(string api, LocalReply reply, ResponsePacket response, PacketCallback action)
    {
        if (api == "tower_battle_start")
        {
            var panel = FindActivePanel<TowerFormationBehaviour>();
            if (panel != null) panel.m_isPlayingBattleDo = false;
            SceneSystem.This()?.IndicatorActive(false);
            PentopSystem.This()?.EnableEvent();
            PopupSystem.This()?.CreateMessage(reply.Error);
            return;
        }
        if (api is "battle_start" or "roguelite_reroll" or "batch_search" or "multiple_spirits_weekly_battle_end" or "multiple_spirits_weekly_battle_start" or "arena_battle_end" or "arena_battle_start" or "event_point_gacha_do" or "tower_battle_end" or "series_battle_end" or "series_battle_start" or "gacha_point_exchange" or "mission_end" or "battle_pass_mission_end" or "battle_pass_receive_reward" or "get_series_battle_ranking_teaminfo" or "mini_game_top")
        {
            if (api == "battle_start" && SceneSystem.This() is { } scene && scene.IsScreenOut()
                && FindActivePanel<BattleBehaviour>() != null)
            {
                var destination = FormationArgs.isAnother ? "Assets/East/Scenes/QuestAnotherScene.unity" : "Assets/East/Scenes/QuestScene.unity";
                scene.IndicatorActive(false);
                PentopSystem.This()?.EnableEvent();
                SoundSystem.This()?.UnloadSE();
                BattleBehaviour.nextEpisodeID = 0;
                scene.LoadScene(destination, (Il2CppSystem.Action)(Action)(() => PopupSystem.This()?.CreateMessage(reply.Error)));
                return;
            }
            if (api is "battle_start" or "arena_battle_start" or "multiple_spirits_weekly_battle_start")
            {
                var formation = FindActivePanel<FormationBehaviour>();
                if (formation != null) formation.m_isPlayingBattleDo = false;
            }
            if (api is "mission_end" or "battle_pass_mission_end" or "battle_pass_receive_reward")
            {
                var panel = FindActivePanel<MissionBehavior>();
                if (panel != null) panel.m_rcvflg = false;
            }
            SceneSystem.This()?.IndicatorActive(false);
            PentopSystem.This()?.EnableEvent();
            PopupSystem.This()?.CreateMessage(reply.Error);
            return;
        }
        if (api == "friend_get_history")
        {
            var panel = FindActivePanel<FriendPlayerDetailPanel>();
            if (panel != null)
            {
                panel.m_friendBehavior?.ShowLRPanel();
                panel.m_detailBG?.SetActive(false);
                panel.gameObject.SetActive(false);
            }
            PentopSystem.This()?.EnableEvent();
            PopupSystem.This()?.CreateMessage(reply.Error);
            return;
        }
        if (api is "get_clear_history_playerinfo" or "retry_gacha_fix" or "dispatch_end" or "unit_recycle" or "get_clear_history_teaminfo" or "first_draw_data" or "exchange" or "update_gold_exchange_shop_list" or "votive_picture_exchange" or "gain_free_item" or "get_event_total_point_reward" or "get_event_total_point_reward_all")
        {
            if (api == "dispatch_end" && ErrandBehaviour.This() is { } errand)
            {
                errand.m_isPlayingEndApi = false;
                errand.m_doUpdate = true;
            }
            PentopSystem.This()?.EnableEvent();
            PopupSystem.This()?.CreateMessage(reply.Error);
            return;
        }
        if (api == "user_info_update")
        {
            var profileResponse = response.TryCast<Comic.UserInfoUpdateResponse>();
            if (profileResponse != null) profileResponse.edit_validity = 1;
        }
        // 本地缺失接口明确结束，不发起真实网络请求或无限重试。
        action?.Invoke(reply.Status, reply.Error);
        return;
    }
}
