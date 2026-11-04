# TouhouLastWorld

业务实现：包含范围内的校验、状态变更或持久化。最小响应：仅提供基础结构或固定数据。未实现返回 501。分类不代表实机验证。

当前清单覆盖 236 个直接继承请求基类的请求类型，接口名未确认时不作推定。

| 请求类型 | 接口 | 实现 | 范围 |
| --- | --- | --- | --- |
| AchievementNewUpdateRequest | achievement_new_update | 业务实现 | 批量清除指定新标记并保存；重复提交幂等，不授予成就 |
| AchievementSelectRequest | achievement_select | 业务实现 | 选择本地已获得条目，零值取消；持久化并返回状态更新，不授予条目 |
| AchievementTopRequest | achievement_top | 最小响应 | 返回本地条目，初始为空；计数为零，不含条件判定或授予 |
| AddLevelRequest | pass_card/user_orb_add_level | 业务实现 | 每级扣除 20，校验余额与 80 级上限，保留经验并保存等级；不自动领取奖励 |
| AppleSwitchDoRequest | 未确认 | 未实现 | — |
| AppleSwitchUserInfoGetRequest | 未确认 | 未实现 | — |
| ArenaBattleEndRequest | 未确认 | 未实现 | — |
| ArenaBattleStartRequest | 未确认 | 未实现 | — |
| AutoReplayEndRequest | 未确认 | 未实现 | — |
| AutoReplayStartRequest | auto_replay_start | 最小响应 | 无运行任务时返回原生“未启动”状态，不扣费；已有任务拒绝覆盖；后台执行与奖励结算尚未实现 |
| BatchSearchRequest | 未确认 | 未实现 | — |
| BattleEndRequest | battle_end | 最小响应 | 固定奖励，不含完整结算 |
| BattlePassMissionEndRequest | battle_pass_mission_end | 业务实现 | 单个或批量领取本地已完成任务经验，按主表累计阈值升级并保存领取记录；仅纯经验任务，任务进度生成、附加奖励与周期重置未实现 |
| BattlePassReceiveRewardRequest | 未确认 | 未实现 | — |
| BattlePassTopRequest | battle_pass_top | 最小响应 | 返回本地任务及领取等级快照，默认空；不生成任务或奖励 |
| BattleReplayInfoRequest | get_replay_info / update_replay_info | 业务实现 | 两种派生请求按任务类型、目标和难度读写原始重放，空内容清除；不验证战斗合法性 |
| BattleStartRequest | battle_start | 最小响应 | 空响应 |
| BootRequest | boot | 最小响应 | 初始化后确认启动；无需建号，不收集广告标识，不变更账号 |
| BuyGoodsRequest | pass_card/buy_goods | 业务实现 | 按配置扣币购买一份，校验限购和库存并保存；支持金币、晶体、普通物品 |
| CheckConditionalPackRequest | check_conditional_pack | 最小响应 | 空列表 |
| CheckUserRequest | check_user | 业务实现 | 本地身份存在性检查 |
| CheckVersionRequest | offline://version/check | 最小响应 | 原始文本入口返回当前安装版本；渠道未匹配时使用首条记录，不提供升级下载 |
| ClearHistoryPlayerInfoRequest | get_clear_history_playerinfo | 业务实现 | 按编号读取本地资料快照，校验角色、服装、称号及统计字段；缺失时报错，累计统计生成未实现 |
| ClearHistoryRequest | get_clear_history | 最小响应 | 无最佳记录，排行及最近记录为空；不生成战绩 |
| ClearHistoryTeamInfoRequest | get_clear_history_teaminfo | 业务实现 | 按完整查询条件读取本地历史队伍快照，校验角色、服装和装备定义；缺失或不完整时报错，快照生成未实现 |
| CollectExpRequest | pass_card/collect_exp | 业务实现 | 按配置领取已完成任务经验，更新等级、次数与领奖提示并保存；任务进度生成未实现 |
| CollectRewardRequest | pass_card/collect_reward | 业务实现 | 单项及批量发放等级奖励，校验等级、权益、库存并保存领取标记；支持金币、晶体、普通物品 |
| Comic.UserInfoUpdateRequest | user_info_update | 业务实现 | 单独或同时更新两个名称；基础文本校验、原子保存，不含远端文本审核 |
| ComicGetPagesRequest | comic_get_pages | 业务实现 | 按主数据单价扣点并增加可读页数，校验余额与总页数，统一持久化；暂不校验发布期 |
| ComicReadEndRequest | comic_read_end | 业务实现 | 保存最大已读页数，拒绝未持有条目和超出可读范围的页数；不发放奖励 |
| DailyArenaTopRequest | daily_arena_top | 最小响应 | 读取本地敌方配置、难度和领取次数；缺少波次或槽位返回 503，不生成配置、不刷新日期或结算奖励，不验证资源完整性 |
| DeleteAccountRequest | delete_account | 业务实现 | 删除本地身份、存档内容及模块旧备份，会话失效；中断后继续清理，重新建号使用新身份，不操作外部账号 |
| DispatchBulkEndRequest | dispatch_bulk_end | 最小响应 | 无已完成任务时返回空结算；存在到期任务时保留任务并报未支持，奖励结算未实现 |
| DispatchCancelRequest | dispatch_cancel | 业务实现 | 删除本地任务并返回客户端删除标记；不发放奖励，本地不退款；不存在的任务返回未找到 |
| DispatchEndRequest | dispatch_end | 业务实现 | 结算已到期任务的本地配置结果，校验成员、记录及主表奖励种类，发放金币/晶体/物品并移除任务；整批回滚，随机结果、经验和活动结算未实现 |
| DispatchStartRequest | dispatch_start | 业务实现 | 启动已配置的本地候选任务，校验成员、等级、职业、容量、有效期并扣点计时；候选生成与奖励结算未实现 |
| EpisodeEndRequest | episode_end | 业务实现 | 校验主数据剧情类型，持久化 scenario_status 并同步进度；重复完成不新增记录，保留战斗状态；不校验开放条件、不结算奖励或推进教程 |
| EventAnniversaryInfoGetAPIRequest | anniversary_info_get | 最小响应 | 返回未填写邮箱；不提交报名或发放奖励 |
| EventAnniversaryInfoUpdateAPIRequest | anniversary_info_update | 业务实现 | 本地保存邮箱及申请状态，读取可回显；不提交外部活动 |
| EventPersonalInfoGetAPIRequest | personal_info_get | 最小响应 | 返回未填写联系信息；不提交报名或发放奖励 |
| EventPersonalInfoUpdateAPIRequest | personal_info_update | 业务实现 | 本地保存联系资料及申请状态，读取可回显；不提交外部活动 |
| EventPointGachaDoAPIRequest | 未确认 | 未实现 | — |
| EventTopRequest | event_top | 最小响应 | 读取本地抽取、任务、累计积分快照；缺失返回 503，不推进任务、不结算奖励、不验证当前主数据覆盖范围 |
| ExchangeCodeRequest | get_exchange_code_reward | 业务实现 | 兑换本地配置的代码，校验账号、同组及重复领取，原子发放金币、晶体、物品；不识别外部代码 |
| ExchangeRequest | exchange | 业务实现 | 仅金币商店：按商品配置扣费、发放金币/晶体/物品并扣库存，校验有效期与容量；其他商店及奖励类型未实现 |
| FirstGachaDrawRequest | first_draw_data | 业务实现 | 校验本地身份和配置中的初始结果，保存待确认快照并重复返回，不直接入库；仅固定本地候选，随机池未实现 |
| FirstGachaSaveRequest | first_draw_save | 业务实现 | 确认待定结果，校验容量后一次保存角色、装备和图鉴，重试不重复发放；角色仅支持本地种子模板 |
| FriendAcceptRequest | friend/accept_apply | 业务实现 | 按主数据容量将本地申请转为关系并保存；亲密度初始为零，不同步远端或校验对方容量 |
| FriendApplyListRequest | friend/apply_list | 业务实现 | 返回已保存的本地申请；不从远端获取 |
| FriendApplyRequest | friend/apply | 业务实现 | 按 public_id 向本地推荐玩家发出并保存申请；校验关系容量、重复和已有关系，不校验远端容量或发送远端请求 |
| FriendApproveRequest | friend_approve | 业务实现 | 按 player_id 接受本地申请并保存关系；校验容量、重复和申请存在性，失败 result=1；不校验远端状态 |
| FriendDeleteRequest | friend_delete | 业务实现 | 按 player_id 删除旧版本地关系并返回剩余列表；不存在时 result=1，不同步远端或新版 public_id 关系 |
| FriendGetCandidateListRequest | friend_get_candidate_list | 最小响应 | 读取本地候选列表，默认空；不含候选匹配或远端查询 |
| FriendGetHelperDetailRequest | friend_get_helper_detail | 最小响应 | 无可用援助；返回零编号触发未选中分支，不生成对象或装备 |
| FriendGetHistoryRequest | friend_get_history | 业务实现 | 读取本地资料快照中的展示对象、累计计数和奖杯，校验资源定义；缺失时报错并关闭详情，累计统计生成未实现 |
| FriendGetRequest | friend/receive | 业务实现 | 领取本地收件记录中的明确金币数量，更新标记并防重复；每日 UTC 最多 30 份，校验金币容量，不生成收件或奖励数量 |
| FriendGiveRequest | friend/give | 业务实现 | 单个或批量清除本地可赠送标记并保存待投递记录；重复调用不重复入队，不投递远端、不生成奖励或每日重置 |
| FriendInfoRequest | friend/info | 最小响应 | 已有关系返回本地三项累计计数，缺失值为零；不代表远端统计，未知关系返回未找到 |
| FriendListRequest | friend/list | 业务实现 | 返回本地关系；申请提示按本地未处理申请计算，不同步远端 |
| FriendPlayerSearchRequest | friend_player_search | 最小响应 | 本地无远端用户目录，返回未找到；不返回虚构资料 |
| FriendRecommendRequest | friend/recommend | 最小响应 | 读取本地推荐列表，默认空；不含匹配逻辑或远端查询 |
| FriendRefuseRequest | friend/refuse_apply | 业务实现 | 删除单条或全部本地申请并保存；列表与提示同步读取，不联系远端 |
| FriendRequestCancelRequest | friend_request_cancel | 业务实现 | 按 player_id 取消本地申请，全部拒绝仅清空收到的申请；目标不存在时 result=1，不同步远端 |
| FriendRequestRequest | friend_request | 业务实现 | 向本地候选玩家发出申请并持久化；校验申请与关系容量、重复和已有关系，不发送远端请求或校验对方容量 |
| FriendSearchRequest | friend/search | 最小响应 | 本地无远端用户目录，返回客户端无匹配状态 2000 |
| FriendSettingHelperRequest | friend_setting_helper | 业务实现 | 校验助战成员归属，保存选择及上报战力，总览同步回读；不重新计算战力 |
| FriendTopRequest | friend_top | 最小响应 | 读取旧版本地关系、收发申请与助战编号；不生成远端数据 |
| GachaDoRequest | gacha_do | 业务实现 | 仅固定本地池：扣费、入库、保存 |
| GachaPointExchangeRequest | 未确认 | 未实现 | — |
| GachaRetryFixRequest | retry_gacha_fix | 业务实现 | 确认已有待定角色，校验有效期及容量后入库、记录图鉴和确认标记，重试返回原编号；仅本地角色模板，候选生成及其他对象未实现 |
| GachaTopRequest | gacha_top | 最小响应 | 固定本地列表 |
| GainFreeItemRequest | gain_free_item | 业务实现 | 领取本地商店唯一免费商品，按模块 UTC 日期限每日一次并保存；支持金币、晶体、物品，未配置或配置不明确时拒绝领取 |
| GameInitRequest | game_init | 业务实现 | 本地会话与入口 |
| GatherGoldRequest | gather_gold | 业务实现 | 收取本地已保存库存，按当前等级容量入账，超出部分保留；主页同步余量，不计算时间产出 |
| GatherPhantasmPointRequest | gather_phantasm_point | 业务实现 | 收取本地已保存点数库存，按当前等级容量入账，超出部分保留；同步主页余量，不计算时间产出 |
| GetAutoReplayInfoRequest | get_auto_replay_info | 业务实现 | 按目标读取已保存测量值；无记录返回零，不重算战斗 |
| GetDispatchListRequest | get_dispatch_list | 业务实现 | 合并本地候选与已启动任务；未配置候选时为空，不生成候选 |
| GetEventTotalPointRewardAllRequest | get_event_total_point_reward_all | 业务实现 | 连续领取积分已达标的固定奖励，一次保存库存与进度；容量不足或含未支持奖励时整批不变，随机奖励未实现 |
| GetEventTotalPointRewardRequest | get_event_total_point_reward | 业务实现 | 按本地累计积分领取当前档固定奖励并推进、保存进度；支持金币、晶体、物品，随机奖励和积分生成未实现 |
| GetHistoryRequest | get_history | 最小响应 | 零值历史摘要；不计算累计统计 |
| GetProfileSettingAPIRequest | get_profile_setting | 业务实现 | 读取本地布局，首次返回空字符串使用客户端默认值；成就列表为空 |
| GetQuestClearedTeamInfoRequest | get_quest_cleared_team_info | 最小响应 | 按类型、目标和难度读取已存队伍；缺省空队伍，不自动记录通关 |
| GetRaidGaugeRequest | get_raid_gauge | 最小响应 | 进度列表为空，客户端使用零累计值；不统计跨用户进度 |
| GetShrineOfficeShopListRequest | get_shrine_office_shop_list | 最小响应 | 空目录；不含兑换或购买 |
| GetSwitchCodeRequest | 未确认 | 未实现 | — |
| GetVersusReplayInfoRequest | get_multiple_spirits_weekly_replay_info | 业务实现 | 按目标读取本地重放；无记录返回空内容，客户端关闭重放模式 |
| GGTapAdRequest | get_tap_ad | 最小响应 | 返回空归因字段与 first_login=0，跳过调用方首次激活上传；不读取或保存设备标识 |
| GGTapAdSecRequest | tap_sec_day | 最小响应 | 返回 is_sec_day=0 与空日期，跳过调用方次日上传；不记录设备标识或计算留存 |
| GoldBoxRenovationEndRequest | gold_box_renovation_end | 业务实现 | 校验已保存升级时间及下一等级配置，到期升一级并清除计时，重复结算不升级；不调整时间产出 |
| GoldBoxRenovationStartRequest | gold_box_renovation_start | 业务实现 | 按下一等级配置校验等级、费用与时长，一次保存扣款及结束时间，拒绝重复开工；不调整时间产出 |
| GoogleSwitchDoRequest | 未确认 | 未实现 | — |
| GoogleSwitchUserInfoGetRequest | 未确认 | 未实现 | — |
| HomeBGMUpdateRequest | home_bgm_update | 业务实现 | 保存非负音乐编号，零值恢复默认；不校验资源可用性，不解锁音乐 |
| HomeCustomSelectUnitUpdateRequest | home_select_unit_update | 业务实现 | 校验已有首页列表索引并保存当前选择，保留其他设置 |
| HomeCustomSetUpdateRequest | favorite_unit_update | 业务实现 | 保存 1–6 项首页配置，校验成员归属、索引及随机模式；外观、动作及音乐仅保存编号，不校验素材可用性 |
| HomeRequest | home | 最小响应 | 空摘要 |
| IdleSearchCompleteUpgradeRequest | afk_complete_upgrade | 业务实现 | 到期后提升对应成长值一级并清槽；重验上限与开始快照，防重复领取；无附加奖励 |
| IdleSearchReceiveDropRequest | afk_receive_drop | 最小响应 | 仅空掉落响应；已有待领奖励返回 503，不生成、发放或清除奖励 |
| IdleSearchSetRequest | afk_set | 业务实现 | 按主数据时长和成长限制保存、取消槽位；校验归属与占用；不结算成长或奖励 |
| IdleSearchTopRequest | afk_top | 最小响应 | 返回已存槽位与阶段；无阶段推进或奖励 |
| ItemExchangeRequest | item_exchange | 业务实现 | 普通物品按配置比例扣料、增加库存并保存；校验容量；其他类型及非单件产出配置尚不支持 |
| LectureRoomFoodAdditionRequest | lecture_room_food_addition | 业务实现 | 按主数据价格扣除体力与货币，增加物品库存并统一保存；货币优先使用免费余额，不自动投放或结算成长 |
| LectureRoomFoodDistributionRequest | lecture_room_food_distribution | 业务实现 | 扣库存、增加储量并保存分钟级效果期限；本地规则为储量截至容量、同类效果续时；暂不结算投放前的时间消耗与成长 |
| LectureRoomFoodExpansionRequest | lecture_room_food_expansion | 业务实现 | 按下一级主数据费用更新食物容量等级；优先扣免费余额，不赠送或消耗食物 |
| LectureRoomUnitExpansionRequest | lecture_room_unit_expansion | 业务实现 | 按下一级主数据费用扣款并保存等级；本地规则优先扣免费余额，余额不足不变更 |
| LectureRoomUnitRemoveRequest | lecture_room_unit_remove | 业务实现 | 移出本地成员并清除占用标记；重复移出幂等，不结算计时收益 |
| LectureRoomUnitSetRequest | lecture_room_unit_set | 业务实现 | 校验持有与主数据容量，保存成员和占用标记；不推进计时收益 |
| LectureRoomUnitTrustUpRequest | lecture_room_unit_trust_up | 业务实现 | 结算已保存的待领取信赖值并清除领取标记；校验归属、入驻和上限；尚不自动生成待领取值 |
| LectureRoomUpdateRequest | lecture_room_update | 最小响应 | 返回已保存设施状态及零结算；未实现时间推进、食物消耗与经验增长 |
| LevelGiftRequest | pass_card/get_level_gift | 业务实现 | 固定扣除 150，增加 20 级且不超过 80；保存购买标记，禁止重复购买；需已有本地进度及 VIP 资格 |
| LoginRequest | login | 业务实现 | 本地身份与存档读取，不含奖励 |
| LogoutRequest | logout | 业务实现 | 校验本地身份并结束会话；保留身份与存档 |
| MailDeleteRequest | mail_delete | 业务实现 | 删除本地 open_type > 0 的已读邮件并持久化；保留未读邮件，不发放附件 |
| MailReceiveRequest | mail_receive | 业务实现 | 无期限邮件的金币、结晶、道具入库并标记；按主数据限额整批校验、防重领；限期、特殊奖励或超额整批保留 |
| MailTopRequest | mail_top | 业务实现 | 读取本地邮件列表；默认空，不生成邮件或附件 |
| MiniGameRequest | 未确认 | 未实现 | — |
| MissionEndRequest | mission_end | 业务实现 | 单个或批量领取本地已完成任务，按主表发放固定金币/晶体/物品并保存领取记录；整批回滚，随机奖励、任务进度生成与周期重置未实现 |
| MissionTopRequest | mission_top | 最小响应 | 返回本地未完成、待领取列表及对应分类，默认空；任务进度生成未实现 |
| MultipleSpiritsWeeklyTopRequest | multiple_spirits_weekly_top | 最小响应 | 空周进度列表；不生成通关记录、奖励或排名 |
| MusicLikeRequest | 未确认 | 未实现 | — |
| MusicShowRequest | music_list | 最小响应 | 空收藏与解锁列表；不含解锁规则 |
| NewsRequest | 未确认 | 未实现 | — |
| PaymentEndCnRequest | 未确认 | 未实现 | — |
| PaymentEndNewRequest | 未确认 | 未实现 | — |
| PaymentEndRequest | 未确认 | 未实现 | — |
| PaymentResumeRequest | 未确认 | 未实现 | — |
| PaymentStartRequest | 未确认 | 未实现 | — |
| PhantasmBoxRenovationEndRequest | phantasm_box_renovation_end | 业务实现 | 校验已保存升级时间及下一等级配置，到期升一级并清除计时，重复结算不升级；不调整时间产出 |
| PhantasmBoxRenovationStartRequest | phantasm_box_renovation_start | 业务实现 | 按下一等级配置校验等级、金币费用与时长，一次保存扣款及结束时间，拒绝重复开工；不调整时间产出 |
| PhantasmPointBuyRequest | phantasm_point_buy | 业务实现 | 按次数价格及等级公式购买，优先扣免费额度，一次保存余额、点数和次数；容量不足整笔拒绝，不重置每日次数 |
| PictureBookReciveRequest | picture_book/get_reward | 业务实现 | 按已完成记录发放配置中的金币、晶体或物品，保存领奖标记；其他奖励类型及进度生成未实现 |
| PictureBookRequest | picture_book/get_user_picture_book_list | 业务实现 | 读取本地待领取与已领奖记录；不生成进度 |
| PictureFrameRequest | avatar/list | 业务实现 | 返回已保存条目及已读状态；无条目时返回空列表，不自动解锁 |
| PictureNewUpdateRequest | picture_new_update | 业务实现 | 批量清除新标记；校验完整列表后保存 |
| PicturePotentialLimitbreakRequest | picture_potential_limitbreak | 业务实现 | 消耗同类装备并逐级扣金币；校验保护、培养占用和上限，解绑材料装备槽并返回删除编号 |
| PictureProtectionRequest | picture_protection | 业务实现 | 保护标记校验、更新与保存 |
| PictureRecycleRequest | picture_recycle | 业务实现 | 按当前成长配置回收并解绑装备；结算金币、点数、碎片和普通物品，容量不足或其他奖励类型整批拒绝 |
| PictureSpecActualizeChemicalRequest | picture_spec_actualize_chemicals | 业务实现 | 按类型、稀有度和下一等级配置消耗特殊材料；校验归属与上限后保存强化等级，不扣金币 |
| PictureSpecActualizeRequest | picture_spec_actualize | 业务实现 | 按类型、稀有度和下一等级配置消耗物品与金币；校验归属、等级和上限后保存强化等级 |
| PopupAdWindowRequest | popup | 最小响应 | 确认弹窗打开通知，恢复调用方交互；不上传、不记录、不改变资产 |
| previewAvatarAPIAsyncRequest | avatar/preview | 业务实现 | 保存指定条目的已读状态，按剩余未读且已解锁条目返回提示；列表读取本地条目，不自动解锁 |
| ProfileUserInfoUpdateRequest | user_info_update | 业务实现 | 三种派生请求更新名称或简介，基础文本校验、原子保存；不含远端文本审核 |
| QuestHelperSelectRequest | friend_helper_select | 最小响应 | 空候选列表，支持不使用援助；返回本地容量及申请数；无积分与远端匹配 |
| ReadUnitTalkRequest | read_unit_talk | 业务实现 | 保存正数对话 ID 的已读标记，重复提交幂等；不校验内容存在性，不生成解锁或奖励 |
| ReincarnationReleaseRequest | unit_reincarnation_release | 业务实现 | 仅保存无消耗、无附带功能的自动解锁节点，校验等级、阶段与强化条件；付费节点、附带功能及扩展谱系未实现 |
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
| ShowRewardRequest | pass_card/show_reward | 业务实现 | 查询已保存的等级、资格及领奖记录；校验当前等级和下一档奖励配置，不创建进度或发奖 |
| ShowShopRequest | pass_card/show_shop | 业务实现 | 读取本地摘要与目录，未配置时为空；不含订单处理 |
| ShowTaskRequest | pass_card/show_task | 业务实现 | 读取本地任务和领取状态，未配置时为空；不生成进度 |
| SweepRequest | 未确认 | 未实现 | — |
| SwitchDoRequest | 未确认 | 未实现 | — |
| SwitchUserInfoGetRequest | 未确认 | 未实现 | — |
| TeamUpdateRequest | team_update | 业务实现 | 更新已有编队的名称、六个成员位及强化位，校验归属和重复；成本采用客户端值，不扩充槽位 |
| TowerBattleEndRequest | 未确认 | 未实现 | — |
| TowerBattleStartRequest | 未确认 | 未实现 | — |
| TowerTopRequest | tower_top | 最小响应 | 读取已保存楼层与进入标记；敌方续战信息为空、恢复次数为零，不推进楼层或生成战斗 |
| TowerUnitInfoRequest | get_tower_unit_info | 最小响应 | 返回本地单位续战 CSV，校验列数、数值、归属及重复 ID；缺省为空，不生成或恢复状态 |
| TowerUnitRecoveryRequest | tower_unit_recovery | 业务实现 | 按本地剩余次数扣费并清空累计损伤，一并保存；余额不足、次数耗尽或无损伤时不扣费，次数每日重置未实现 |
| TrainingRoomCancelRequest | training_room_cancel | 业务实现 | 按槽位删除并保存训练任务；本地不退款、不发放技能收益，空槽位返回未找到 |
| TrainingRoomExpansionRequest | training_room_expansion | 业务实现 | 按下一级主数据费用扣款并保存训练容量等级；本地优先扣免费余额，不改变训练任务 |
| TrainingRoomShortenedFinishTimeRequest | training_room_shortened_finish_time | 业务实现 | 按主数据秒数批量扣材料并缩短期限；本地最多缩至当前时间，超额不退，不结算成长；拒绝已到期任务 |
| TrainingRoomStartRequest | training_room_start | 业务实现 | 校验容量、成员归属与占用，扣一本材料并保存秒级训练期限；暂不校验技能解锁、等级上限或结算成长 |
| TrainingRoomUpdateRequest | training_room_update | 最小响应 | 返回已保存训练任务与等级；不推进训练时间或结算技能成长 |
| TranscribeKpiRequest | transcribe_kpi | 最小响应 | 接受类型 1、2 的统计事件；不上传、不记录、不变更资产 |
| TreasureGachaRequest | 未确认 | 未实现 | — |
| TreasureInfoRequest | fudai/fudai_info | 业务实现 | 查询与活动配置匹配的本地状态及已保存奖励信息；不生成奖励码，缺少快照时拒绝返回 |
| TutorialFirstGachaConfirmRequest | tutorial_first_gacha_confirm | 最小响应 | 已确认初始库存时回传用户状态，恢复请求构造时清空的客户端数据；不新增奖励、不推进教学 |
| TutorialFirstGachaDoRequest | tutorial_first_gacha_do | 最小响应 | 仅返回已确认初始库存的本地用户快照，不额外抽取或发放；尚未确认时拒绝 |
| TutorialUpdateRequest | tutorial_update | 业务实现 | 校验并保存单调进度；不含阶段奖励 |
| UnitAllocatePointsRequest | allocate_points | 业务实现 | 按目标等级逐级校验主数据费用与提交前已用点数门槛，统一扣点并保存；拒绝降级和重复类型，不重算历史点数来源 |
| UnitBGMUpdateRequest | unit_bgm_update | 业务实现 | 校验成员归属，保存非负音乐编号；不校验资源可用性，不解锁音乐 |
| UnitDestinyValueUpRequest | unit_destiny_value_up | 业务实现 | 三种物品按配置混合扣除并提升属性值，上限截断；校验归属和培养占用；单位材料尚不支持 |
| UnitEquipCostumeRequest | unit_equip_costume | 业务实现 | 批量切换默认、已持有或当前外观，校验主数据归属；不新增持有，未接入特殊进阶解锁 |
| UnitEquipPictureRequest | unit_equip_picture | 业务实现 | 局部更新 1–5 槽位与反向引用；校验持有、重复和占用，0 表示卸下；未校验槽位解锁 |
| UnitEquipPictureResetRequest | unit_equip_picture_reset | 业务实现 | 清空持有实例的 5 个装备槽与装备反向引用；保留装备、保护状态及其他实例配置 |
| UnitExtraSpecActualizeRequest | unit_extra_spec_actualize | 业务实现 | 独立保存六项额外属性、按一比一消耗对应材料；使用当前解锁上限及生命系数，整批校验后保存 |
| UnitLevelLimitbreakRequest | unit_level_limitbreak | 业务实现 | 通用物品材料路径：按配置校验等级与数量，扣除金币和物品、提升突破阶数并增加命运值；暂不支持同名单位材料 |
| UnitLimitbreakStrongFragrantWoodRequest | limitbreak_strong_fragrantwood | 业务实现 | 按下一阶配置消耗解锁材料并保存单项上限阶数；检查功能开关、当前阶数和属性上限，属性值不变 |
| UnitNewUpdateRequest | unit_new_update | 业务实现 | 批量清除新标记；校验完整列表后保存 |
| UnitProtectionRequest | unit_protection | 业务实现 | 保护标记校验、更新与保存 |
| UnitRankPromoteItemSetRequest | unit_rank_promote_item_set | 业务实现 | 按当前阶数和槽位配置扣除物品或金币、保存材料槽；校验归属和重复填充，暂不支持装备实例材料 |
| UnitRankPromoteRequest | unit_rank_promote | 业务实现 | 校验归属、阶数与等级，扣除升阶金币及未填槽位的物品或金币，一次保存升阶并重置六槽；已填槽位不重复扣费，暂不支持未填的装备实例材料 |
| UnitRareAwakenRequest | unit_rare_awaken | 业务实现 | 按下一阶配置消耗碎片并保存觉醒阶数；校验归属、当前阶数及同类挂机互斥，属性仍由客户端配置计算 |
| UnitRecycleRequest | unit_recycle | 业务实现 | 按主表回收未保护、未占用角色，汇总金币、点数、碎片及物品，卸下装备并保存；拒绝超容量整批操作，保留至少一个角色及图鉴 |
| UnitResetPointAllocationRequest | reset_point_allocation | 业务实现 | 清空九类分配并返还已用点数；首次或主数据免费期不扣款，其余扣金币；本地无分配时不收费，不重算历史点数来源 |
| UnitSpecActualizeRequest | unit_spec_actualize | 业务实现 | 按六项基础材料类型和转生消耗系数增加属性；校验库存、整除数量与当前上限，批量一次保存 |
| UnitVoiceAutosetUpdateRequest | unit_voice_autoset_update | 业务实现 | 开关校验与保存；不含自动分配内容 |
| UnitVoiceUpdateRequest | unit_voice_update | 业务实现 | 保存已收录或持有条目的语音偏好；不校验语音资源与解锁条件 |
| UpdateAutoReplayInfoRequest | update_auto_replay_info | 业务实现 | 按任务类型、目标及难度保存客户端测量值；非负校验，不重算战斗 |
| UpdateGoldExchangeShopListRequest | update_gold_exchange_shop_list | 业务实现 | 按刷新次数扣费并切换至已配置的本地商品批次，保存商品与次数；随机生成及每日次数重置未实现 |
| UpdateHomeMotionModeRequest | update_home_motion_mode | 业务实现 | 校验成员归属并保存非负动作模式；不校验动作资源或解锁条件 |
| UpdateProfileSetting2APIRequest | update_profile_setting2 | 业务实现 | 本地文本检查，拒绝缺失值和控制字符；允许清空，保存由布局接口完成，不含远端审核 |
| UpdateProfileSettingAPIRequest | update_profile_setting | 业务实现 | 校验布局 JSON 字段类型并原样保存；不校验素材所有权，不授予素材 |
| UpdateVersusReplayInfoRequest | update_multiple_spirits_weekly_replay_info | 业务实现 | 校验目标及非负等级，原样保存规则和重放；空内容删除，不验证战斗合法性 |
| UpdateVoiceVersionRequest | update_voice_version | 最小响应 | 接收无参数通知并返回空响应；不修改资源版本或触发下载 |
| UserAppleInfoDeleteRequest | user_apple_info_delete | 业务实现 | 清除并保存本地 Apple 关联标记；不验证令牌、不撤销外部授权 |
| UserAppleInfoUpdateRequest | 未确认 | 未实现 | — |
| UserCreateRequest | user_create | 业务实现 | 本地身份创建、重复调用保留存档 |
| UserGoogleInfoDeleteRequest | user_google_info_delete | 业务实现 | 清除并保存本地 Google 关联标记；不验证令牌、不撤销外部授权 |
| UserGoogleInfoUpdateRequest | 未确认 | 未实现 | — |
| UserTwitterInfoDeleteRequest | user_twitter_info_delete | 业务实现 | 清除当前本地存档的关联标记与已保存编号；校验已有编号，不撤销外部平台授权 |
| UserTwitterInfoUpdateRequest | 未确认 | 未实现 | — |
| VersionGetRequest | version_get | 最小响应 | 固定版本与资源路径 |
| VersusBattleEndRequest | 未确认 | 未实现 | — |
| VersusBattleStartRequest | 未确认 | 未实现 | — |
| VotivePictureExchangeRequest | votive_picture_exchange | 业务实现 | 按配置消耗材料兑换装备，校验有效期与容量，创建独立对象并更新图鉴；角色等其他对象未实现 |
| WeeklyArenaBattleScoreResetRequest | weekly_arena_battle_score_reset | 业务实现 | 清零本地场次总分与参战编号；保留累计最高分、难度、分项与敌方配置，缺少场次返回错误；不生成周配置 |
| WeeklyArenaTopRequest | weekly_arena_top | 最小响应 | 读取本地四场配置与累计最高分；缺少配置返回 503，不生成敌方配置、不轮换周期、不验证资源完整性 |
