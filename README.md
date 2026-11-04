# TouhouLastWorld

业务实现：包含范围内的校验、状态变更或持久化。最小响应：仅提供基础结构或固定数据。未实现返回 501。分类不代表实机验证。

当前清单覆盖 236 个直接继承请求基类的请求类型，接口名未确认时不作推定。

| 请求类型 | 接口 | 实现 | 范围 |
| --- | --- | --- | --- |
| AchievementNewUpdateRequest | achievement_new_update | 业务实现 | 批量清除指定新标记并保存；重复提交幂等，不授予成就 |
| AchievementSelectRequest | achievement_select | 业务实现 | 选择本地已获得条目，零值取消；持久化并返回状态更新，不授予条目 |
| AchievementTopRequest | achievement_top | 最小响应 | 返回本地条目，初始为空；计数为零，不含条件判定或授予 |
| AddLevelRequest | 未确认 | 未实现 | — |
| AppleSwitchDoRequest | 未确认 | 未实现 | — |
| AppleSwitchUserInfoGetRequest | 未确认 | 未实现 | — |
| ArenaBattleEndRequest | 未确认 | 未实现 | — |
| ArenaBattleStartRequest | 未确认 | 未实现 | — |
| AutoReplayEndRequest | 未确认 | 未实现 | — |
| AutoReplayStartRequest | 未确认 | 未实现 | — |
| BatchSearchRequest | 未确认 | 未实现 | — |
| BattleEndRequest | battle_end | 最小响应 | 固定奖励，不含完整结算 |
| BattlePassMissionEndRequest | 未确认 | 未实现 | — |
| BattlePassReceiveRewardRequest | 未确认 | 未实现 | — |
| BattlePassTopRequest | battle_pass_top | 最小响应 | 三类任务列表为空、领取等级为 0；不开放活动、不生成任务或奖励 |
| BattleReplayInfoRequest | get_replay_info / update_replay_info | 业务实现 | 两种派生请求按任务类型、目标和难度读写原始重放，空内容清除；不验证战斗合法性 |
| BattleStartRequest | battle_start | 最小响应 | 空响应 |
| BootRequest | 未确认 | 未实现 | — |
| BuyGoodsRequest | 未确认 | 未实现 | — |
| CheckConditionalPackRequest | check_conditional_pack | 最小响应 | 空列表 |
| CheckUserRequest | check_user | 业务实现 | 本地身份存在性检查 |
| CheckVersionRequest | 未确认 | 未实现 | — |
| ClearHistoryPlayerInfoRequest | 未确认 | 未实现 | — |
| ClearHistoryRequest | get_clear_history | 最小响应 | 无最佳记录，排行及最近记录为空；不生成战绩 |
| ClearHistoryTeamInfoRequest | 未确认 | 未实现 | — |
| CollectExpRequest | 未确认 | 未实现 | — |
| CollectRewardRequest | 未确认 | 未实现 | — |
| Comic.UserInfoUpdateRequest | user_info_update | 业务实现 | 单独或同时更新两个名称；基础文本校验、原子保存，不含远端文本审核 |
| ComicGetPagesRequest | comic_get_pages | 业务实现 | 按主数据单价扣点并增加可读页数，校验余额与总页数，统一持久化；暂不校验发布期 |
| ComicReadEndRequest | comic_read_end | 业务实现 | 保存最大已读页数，拒绝未持有条目和超出可读范围的页数；不发放奖励 |
| DailyArenaTopRequest | 未确认 | 未实现 | — |
| DeleteAccountRequest | 未确认 | 未实现 | — |
| DispatchBulkEndRequest | 未确认 | 未实现 | — |
| DispatchCancelRequest | dispatch_cancel | 业务实现 | 删除本地任务并返回客户端删除标记；不发放奖励，本地不退款；不存在的任务返回未找到 |
| DispatchEndRequest | 未确认 | 未实现 | — |
| DispatchStartRequest | 未确认 | 未实现 | — |
| EpisodeEndRequest | 未确认 | 未实现 | — |
| EventAnniversaryInfoGetAPIRequest | anniversary_info_get | 最小响应 | 返回未填写邮箱；不提交报名或发放奖励 |
| EventAnniversaryInfoUpdateAPIRequest | 未确认 | 未实现 | — |
| EventPersonalInfoGetAPIRequest | personal_info_get | 最小响应 | 返回未填写联系信息；不提交报名或发放奖励 |
| EventPersonalInfoUpdateAPIRequest | 未确认 | 未实现 | — |
| EventPointGachaDoAPIRequest | 未确认 | 未实现 | — |
| EventTopRequest | 未确认 | 未实现 | — |
| ExchangeCodeRequest | 未确认 | 未实现 | — |
| ExchangeRequest | 未确认 | 未实现 | — |
| FirstGachaDrawRequest | 未确认 | 未实现 | — |
| FirstGachaSaveRequest | 未确认 | 未实现 | — |
| FriendAcceptRequest | friend/accept_apply | 业务实现 | 按主数据容量将本地申请转为关系并保存；亲密度初始为零，不同步远端或校验对方容量 |
| FriendApplyListRequest | friend/apply_list | 业务实现 | 返回已保存的本地申请；不从远端获取 |
| FriendApplyRequest | 未确认 | 未实现 | — |
| FriendApproveRequest | friend_approve | 业务实现 | 按 player_id 接受本地申请并保存关系；校验容量、重复和申请存在性，失败 result=1；不校验远端状态 |
| FriendDeleteRequest | friend_delete | 业务实现 | 按 player_id 删除旧版本地关系并返回剩余列表；不存在时 result=1，不同步远端或新版 public_id 关系 |
| FriendGetCandidateListRequest | friend_get_candidate_list | 最小响应 | 空候选列表；不含候选匹配 |
| FriendGetHelperDetailRequest | 未确认 | 未实现 | — |
| FriendGetHistoryRequest | 未确认 | 未实现 | — |
| FriendGetRequest | 未确认 | 未实现 | — |
| FriendGiveRequest | 未确认 | 未实现 | — |
| FriendInfoRequest | friend/info | 最小响应 | 已有关系返回本地三项累计计数，缺失值为零；不代表远端统计，未知关系返回未找到 |
| FriendListRequest | friend/list | 业务实现 | 返回本地关系；申请提示按本地未处理申请计算，不同步远端 |
| FriendPlayerSearchRequest | friend_player_search | 最小响应 | 本地无远端用户目录，返回未找到；不返回虚构资料 |
| FriendRecommendRequest | friend/recommend | 最小响应 | 空推荐列表；不含匹配逻辑 |
| FriendRefuseRequest | friend/refuse_apply | 业务实现 | 删除单条或全部本地申请并保存；列表与提示同步读取，不联系远端 |
| FriendRequestCancelRequest | friend_request_cancel | 业务实现 | 按 player_id 取消本地申请，全部拒绝仅清空收到的申请；目标不存在时 result=1，不同步远端 |
| FriendRequestRequest | 未确认 | 未实现 | — |
| FriendSearchRequest | friend/search | 最小响应 | 本地无远端用户目录，返回客户端无匹配状态 2000 |
| FriendSettingHelperRequest | friend_setting_helper | 业务实现 | 校验助战成员归属，保存选择及上报战力，总览同步回读；不重新计算战力 |
| FriendTopRequest | friend_top | 最小响应 | 读取旧版本地关系、收发申请与助战编号；不生成远端数据 |
| GachaDoRequest | gacha_do | 业务实现 | 仅固定本地池：扣费、入库、保存 |
| GachaPointExchangeRequest | 未确认 | 未实现 | — |
| GachaRetryFixRequest | 未确认 | 未实现 | — |
| GachaTopRequest | gacha_top | 最小响应 | 固定本地列表 |
| GainFreeItemRequest | 未确认 | 未实现 | — |
| GameInitRequest | game_init | 业务实现 | 本地会话与入口 |
| GatherGoldRequest | 未确认 | 未实现 | — |
| GatherPhantasmPointRequest | 未确认 | 未实现 | — |
| GetAutoReplayInfoRequest | get_auto_replay_info | 业务实现 | 按目标读取已保存测量值；无记录返回零，不重算战斗 |
| GetDispatchListRequest | get_dispatch_list | 最小响应 | 空派遣列表；不含派遣开始与结算 |
| GetEventTotalPointRewardAllRequest | 未确认 | 未实现 | — |
| GetEventTotalPointRewardRequest | 未确认 | 未实现 | — |
| GetHistoryRequest | get_history | 最小响应 | 零值历史摘要；不计算累计统计 |
| GetProfileSettingAPIRequest | get_profile_setting | 业务实现 | 读取本地布局，首次返回空字符串使用客户端默认值；成就列表为空 |
| GetQuestClearedTeamInfoRequest | 未确认 | 未实现 | — |
| GetRaidGaugeRequest | get_raid_gauge | 最小响应 | 进度列表为空，客户端使用零累计值；不统计跨用户进度 |
| GetShrineOfficeShopListRequest | get_shrine_office_shop_list | 最小响应 | 空目录；不含兑换或购买 |
| GetSwitchCodeRequest | 未确认 | 未实现 | — |
| GetVersusReplayInfoRequest | get_multiple_spirits_weekly_replay_info | 业务实现 | 按目标读取本地重放；无记录返回空内容，客户端关闭重放模式 |
| GGTapAdRequest | get_tap_ad | 最小响应 | 返回空归因字段与 first_login=0，跳过调用方首次激活上传；不读取或保存设备标识 |
| GGTapAdSecRequest | tap_sec_day | 最小响应 | 返回 is_sec_day=0 与空日期，跳过调用方次日上传；不记录设备标识或计算留存 |
| GoldBoxRenovationEndRequest | 未确认 | 未实现 | — |
| GoldBoxRenovationStartRequest | 未确认 | 未实现 | — |
| GoogleSwitchDoRequest | 未确认 | 未实现 | — |
| GoogleSwitchUserInfoGetRequest | 未确认 | 未实现 | — |
| HomeBGMUpdateRequest | home_bgm_update | 业务实现 | 保存非负音乐编号，零值恢复默认；不校验资源可用性，不解锁音乐 |
| HomeCustomSelectUnitUpdateRequest | home_select_unit_update | 业务实现 | 校验已有首页列表索引并保存当前选择，保留其他设置 |
| HomeCustomSetUpdateRequest | favorite_unit_update | 业务实现 | 保存 1–6 项首页配置，校验成员归属、索引及随机模式；外观、动作及音乐仅保存编号，不校验素材可用性 |
| HomeRequest | home | 最小响应 | 空摘要 |
| IdleSearchCompleteUpgradeRequest | 未确认 | 未实现 | — |
| IdleSearchReceiveDropRequest | 未确认 | 未实现 | — |
| IdleSearchSetRequest | 未确认 | 未实现 | — |
| IdleSearchTopRequest | afk_top | 最小响应 | 未启动摘要，无奖励 |
| ItemExchangeRequest | 未确认 | 未实现 | — |
| LectureRoomFoodAdditionRequest | lecture_room_food_addition | 业务实现 | 按主数据价格扣除体力与货币，增加物品库存并统一保存；货币优先使用免费余额，不自动投放或结算成长 |
| LectureRoomFoodDistributionRequest | lecture_room_food_distribution | 业务实现 | 扣库存、增加储量并保存分钟级效果期限；本地规则为储量截至容量、同类效果续时；暂不结算投放前的时间消耗与成长 |
| LectureRoomFoodExpansionRequest | lecture_room_food_expansion | 业务实现 | 按下一级主数据费用更新食物容量等级；优先扣免费余额，不赠送或消耗食物 |
| LectureRoomUnitExpansionRequest | lecture_room_unit_expansion | 业务实现 | 按下一级主数据费用扣款并保存等级；本地规则优先扣免费余额，余额不足不变更 |
| LectureRoomUnitRemoveRequest | lecture_room_unit_remove | 业务实现 | 移出本地成员并清除占用标记；重复移出幂等，不结算计时收益 |
| LectureRoomUnitSetRequest | lecture_room_unit_set | 业务实现 | 校验持有与主数据容量，保存成员和占用标记；不推进计时收益 |
| LectureRoomUnitTrustUpRequest | 未确认 | 未实现 | — |
| LectureRoomUpdateRequest | lecture_room_update | 最小响应 | 返回已保存设施状态及零结算；未实现时间推进、食物消耗与经验增长 |
| LevelGiftRequest | 未确认 | 未实现 | — |
| LoginRequest | login | 业务实现 | 本地身份与存档读取，不含奖励 |
| LogoutRequest | logout | 业务实现 | 校验本地身份并结束会话；保留身份与存档 |
| MailDeleteRequest | mail_delete | 业务实现 | 删除本地 open_type > 0 的已读邮件并持久化；保留未读邮件，不发放附件 |
| MailReceiveRequest | 未确认 | 未实现 | — |
| MailTopRequest | mail_top | 业务实现 | 读取本地邮件列表；默认空，不生成邮件或附件 |
| MiniGameRequest | 未确认 | 未实现 | — |
| MissionEndRequest | 未确认 | 未实现 | — |
| MissionTopRequest | mission_top | 最小响应 | 空分类、未完成与已完成列表；不含奖励 |
| MultipleSpiritsWeeklyTopRequest | multiple_spirits_weekly_top | 最小响应 | 空周进度列表；不生成通关记录、奖励或排名 |
| MusicLikeRequest | 未确认 | 未实现 | — |
| MusicShowRequest | music_list | 最小响应 | 空收藏与解锁列表；不含解锁规则 |
| NewsRequest | 未确认 | 未实现 | — |
| PaymentEndCnRequest | 未确认 | 未实现 | — |
| PaymentEndNewRequest | 未确认 | 未实现 | — |
| PaymentEndRequest | 未确认 | 未实现 | — |
| PaymentResumeRequest | 未确认 | 未实现 | — |
| PaymentStartRequest | 未确认 | 未实现 | — |
| PhantasmBoxRenovationEndRequest | 未确认 | 未实现 | — |
| PhantasmBoxRenovationStartRequest | 未确认 | 未实现 | — |
| PhantasmPointBuyRequest | 未确认 | 未实现 | — |
| PictureBookReciveRequest | 未确认 | 未实现 | — |
| PictureBookRequest | picture_book/get_user_picture_book_list | 最小响应 | 空完成记录；不含进度与奖励 |
| PictureFrameRequest | avatar/list | 业务实现 | 返回已保存条目及已读状态；无条目时返回空列表，不自动解锁 |
| PictureNewUpdateRequest | picture_new_update | 业务实现 | 批量清除新标记；校验完整列表后保存 |
| PicturePotentialLimitbreakRequest | 未确认 | 未实现 | — |
| PictureProtectionRequest | picture_protection | 业务实现 | 保护标记校验、更新与保存 |
| PictureRecycleRequest | 未确认 | 未实现 | — |
| PictureSpecActualizeChemicalRequest | 未确认 | 未实现 | — |
| PictureSpecActualizeRequest | 未确认 | 未实现 | — |
| PopupAdWindowRequest | popup | 最小响应 | 确认弹窗打开通知，恢复调用方交互；不上传、不记录、不改变资产 |
| previewAvatarAPIAsyncRequest | avatar/preview | 业务实现 | 保存指定条目的已读状态，按剩余未读且已解锁条目返回提示；列表读取本地条目，不自动解锁 |
| ProfileUserInfoUpdateRequest | user_info_update | 业务实现 | 三种派生请求更新名称或简介，基础文本校验、原子保存；不含远端文本审核 |
| QuestHelperSelectRequest | 未确认 | 未实现 | — |
| ReadUnitTalkRequest | read_unit_talk | 业务实现 | 保存正数对话 ID 的已读标记，重复提交幂等；不校验内容存在性，不生成解锁或奖励 |
| ReincarnationReleaseRequest | 未确认 | 未实现 | — |
| replaceAvatarRequest | avatar/update | 业务实现 | 仅选择本地已解锁条目，保存编号与资源描述；不授予解锁，不验证资源文件是否存在 |
| RhythmGameEndRequest | rhythm_game_end | 业务实现 | 匹配当前曲目、校验难度 2–4，按曲目和难度分别保存分数、完成率及等级最大值；禁止重复结束，不复算成绩，恢复额度为零且无奖励 |
| RhythmGameStartRequest | rhythm_game_start | 业务实现 | 校验曲目主数据并保存当前曲目；再次开始替换上次记录，不扣除资源 |
| RhythmGameTopRequest | rhythm_game_top | 业务实现 | 返回本地最佳成绩；初始为空，曲目由客户端主数据加载 |
| RogueLiteBattleEndRequest | 未确认 | 未实现 | — |
| RogueLiteBattleStartRequest | 未确认 | 未实现 | — |
| RogueliteDeckCallRequest | roguelite_deck_call | 业务实现 | 将已保存预设复制到当前配置并持久化；不存在的编号返回错误，不改变其他预设 |
| RogueliteDeckSaveRequest | roguelite_deck_save | 业务实现 | 保存编号、名称和有序条目，同时更新当前配置；可保存空配置，暂不校验容量、持有数量或条目主数据 |
| RogueLiteEventEndRequest | 未确认 | 未实现 | — |
| RogueLiteMapEndRequest | 未确认 | 未实现 | — |
| RogueLiteMapStartRequest | 未确认 | 未实现 | — |
| RogueLiteRerollRequest | 未确认 | 未实现 | — |
| RogueLiteTopRequest | roguelite_top | 最小响应 | 返回已保存预设并按主数据补齐空槽；地图未开始、详情为空、抽取额度为零，不生成关卡或奖励 |
| SeriesBattleEndRequest | 未确认 | 未实现 | — |
| SeriesBattleGetReplayInfoRequest | get_series_battle_replay_info | 业务实现 | 按关卡及 1–5 分区读取重放；未请求或无记录的字段为空 |
| SeriesBattleRankingEndRequest | 未确认 | 未实现 | — |
| SeriesBattleRankingGetListRequest | get_series_battle_ranking_list | 最小响应 | 排行和个人排名为空，不生成名次或分数 |
| SeriesBattleRankingGetTeamInfoRequest | 未确认 | 未实现 | — |
| SeriesBattleRankingResetRequest | 未确认 | 未实现 | — |
| SeriesBattleRankingScoreSimulateRequest | 未确认 | 未实现 | — |
| SeriesBattleRankingStartRequest | 未确认 | 未实现 | — |
| SeriesBattleRankingTopRequest | series_battle_ranking_top | 最小响应 | 未开放、未入榜、零成绩；不创建赛程或成绩，界面仍依赖客户端活动主数据 |
| SeriesBattleResetRequest | series_battle_reset | 业务实现 | 将当前流程恢复未开始状态并持久化；保留编队和存储回放，不结算奖励或退款 |
| SeriesBattleStartRequest | 未确认 | 未实现 | — |
| SeriesBattleTeamUpdateRequest | series_battle_team_update | 业务实现 | 批量保存 80–84 号编队，校验持有成员、队内重复与六槽上限；进行中拒绝修改，整批持久化；不复算消耗或跨队限制 |
| SeriesBattleUpdateReplayInfoRequest | update_series_battle_replay_info | 业务实现 | 校验关卡和 1–5 分区，保存原始重放；空内容清除对应分区，不验证战斗合法性 |
| SetAgeRequest | set_age | 业务实现 | 校验并保存 1–3 档位；不涉及支付流程 |
| ShopListRequest | get_shop_list | 最小响应 | 空目录与空推荐列表；不含订单处理 |
| ShopOrderRequest | 未确认 | 未实现 | — |
| ShowRewardRequest | 未确认 | 未实现 | — |
| ShowShopRequest | pass_card/show_shop | 最小响应 | 未开通摘要与空目录；不含订单处理 |
| ShowTaskRequest | pass_card/show_task | 最小响应 | 未开通摘要与空任务；不含权益和奖励 |
| SweepRequest | 未确认 | 未实现 | — |
| SwitchDoRequest | 未确认 | 未实现 | — |
| SwitchUserInfoGetRequest | 未确认 | 未实现 | — |
| TeamUpdateRequest | team_update | 业务实现 | 更新已有编队的名称、六个成员位及强化位，校验归属和重复；成本采用客户端值，不扩充槽位 |
| TowerBattleEndRequest | 未确认 | 未实现 | — |
| TowerBattleStartRequest | 未确认 | 未实现 | — |
| TowerTopRequest | 未确认 | 未实现 | — |
| TowerUnitInfoRequest | 未确认 | 未实现 | — |
| TowerUnitRecoveryRequest | 未确认 | 未实现 | — |
| TrainingRoomCancelRequest | training_room_cancel | 业务实现 | 按槽位删除并保存训练任务；本地不退款、不发放技能收益，空槽位返回未找到 |
| TrainingRoomExpansionRequest | training_room_expansion | 业务实现 | 按下一级主数据费用扣款并保存训练容量等级；本地优先扣免费余额，不改变训练任务 |
| TrainingRoomShortenedFinishTimeRequest | training_room_shortened_finish_time | 业务实现 | 按主数据秒数批量扣材料并缩短期限；本地最多缩至当前时间，超额不退，不结算成长；拒绝已到期任务 |
| TrainingRoomStartRequest | training_room_start | 业务实现 | 校验容量、成员归属与占用，扣一本材料并保存秒级训练期限；暂不校验技能解锁、等级上限或结算成长 |
| TrainingRoomUpdateRequest | training_room_update | 最小响应 | 返回已保存训练任务与等级；不推进训练时间或结算技能成长 |
| TranscribeKpiRequest | transcribe_kpi | 最小响应 | 接受类型 1、2 的统计事件；不上传、不记录、不变更资产 |
| TreasureGachaRequest | 未确认 | 未实现 | — |
| TreasureInfoRequest | 未确认 | 未实现 | — |
| TutorialFirstGachaConfirmRequest | 未确认 | 未实现 | — |
| TutorialFirstGachaDoRequest | 未确认 | 未实现 | — |
| TutorialUpdateRequest | tutorial_update | 业务实现 | 校验并保存单调进度；不含阶段奖励 |
| UnitAllocatePointsRequest | allocate_points | 业务实现 | 按目标等级逐级校验主数据费用与提交前已用点数门槛，统一扣点并保存；拒绝降级和重复类型，不重算历史点数来源 |
| UnitBGMUpdateRequest | unit_bgm_update | 业务实现 | 校验成员归属，保存非负音乐编号；不校验资源可用性，不解锁音乐 |
| UnitDestinyValueUpRequest | 未确认 | 未实现 | — |
| UnitEquipCostumeRequest | unit_equip_costume | 业务实现 | 批量切换默认、已持有或当前外观，校验主数据归属；不新增持有，未接入特殊进阶解锁 |
| UnitEquipPictureRequest | unit_equip_picture | 业务实现 | 局部更新 1–5 槽位与反向引用；校验持有、重复和占用，0 表示卸下；未校验槽位解锁 |
| UnitEquipPictureResetRequest | unit_equip_picture_reset | 业务实现 | 清空持有实例的 5 个装备槽与装备反向引用；保留装备、保护状态及其他实例配置 |
| UnitExtraSpecActualizeRequest | 未确认 | 未实现 | — |
| UnitLevelLimitbreakRequest | 未确认 | 未实现 | — |
| UnitLimitbreakStrongFragrantWoodRequest | 未确认 | 未实现 | — |
| UnitNewUpdateRequest | unit_new_update | 业务实现 | 批量清除新标记；校验完整列表后保存 |
| UnitProtectionRequest | unit_protection | 业务实现 | 保护标记校验、更新与保存 |
| UnitRankPromoteItemSetRequest | 未确认 | 未实现 | — |
| UnitRankPromoteRequest | 未确认 | 未实现 | — |
| UnitRareAwakenRequest | 未确认 | 未实现 | — |
| UnitRecycleRequest | 未确认 | 未实现 | — |
| UnitResetPointAllocationRequest | reset_point_allocation | 业务实现 | 清空九类分配并返还已用点数；首次或主数据免费期不扣款，其余扣金币；本地无分配时不收费，不重算历史点数来源 |
| UnitSpecActualizeRequest | 未确认 | 未实现 | — |
| UnitVoiceAutosetUpdateRequest | unit_voice_autoset_update | 业务实现 | 开关校验与保存；不含自动分配内容 |
| UnitVoiceUpdateRequest | unit_voice_update | 业务实现 | 保存已收录或持有条目的语音偏好；不校验语音资源与解锁条件 |
| UpdateAutoReplayInfoRequest | update_auto_replay_info | 业务实现 | 按任务类型、目标及难度保存客户端测量值；非负校验，不重算战斗 |
| UpdateGoldExchangeShopListRequest | 未确认 | 未实现 | — |
| UpdateHomeMotionModeRequest | update_home_motion_mode | 业务实现 | 校验成员归属并保存非负动作模式；不校验动作资源或解锁条件 |
| UpdateProfileSetting2APIRequest | update_profile_setting2 | 业务实现 | 本地文本检查，拒绝缺失值和控制字符；允许清空，保存由布局接口完成，不含远端审核 |
| UpdateProfileSettingAPIRequest | update_profile_setting | 业务实现 | 校验布局 JSON 字段类型并原样保存；不校验素材所有权，不授予素材 |
| UpdateVersusReplayInfoRequest | update_multiple_spirits_weekly_replay_info | 业务实现 | 校验目标及非负等级，原样保存规则和重放；空内容删除，不验证战斗合法性 |
| UpdateVoiceVersionRequest | update_voice_version | 最小响应 | 接收无参数通知并返回空响应；不修改资源版本或触发下载 |
| UserAppleInfoDeleteRequest | 未确认 | 未实现 | — |
| UserAppleInfoUpdateRequest | 未确认 | 未实现 | — |
| UserCreateRequest | user_create | 业务实现 | 本地身份创建、重复调用保留存档 |
| UserGoogleInfoDeleteRequest | 未确认 | 未实现 | — |
| UserGoogleInfoUpdateRequest | 未确认 | 未实现 | — |
| UserTwitterInfoDeleteRequest | 未确认 | 未实现 | — |
| UserTwitterInfoUpdateRequest | 未确认 | 未实现 | — |
| VersionGetRequest | version_get | 最小响应 | 固定版本与资源路径 |
| VersusBattleEndRequest | 未确认 | 未实现 | — |
| VersusBattleStartRequest | 未确认 | 未实现 | — |
| VotivePictureExchangeRequest | 未确认 | 未实现 | — |
| WeeklyArenaBattleScoreResetRequest | weekly_arena_battle_score_reset | 业务实现 | 清零本地场次总分与参战编号并重算总分；保留难度、分项与敌方配置，缺少场次返回错误；不生成周配置 |
| WeeklyArenaTopRequest | 未确认 | 未实现 | — |
