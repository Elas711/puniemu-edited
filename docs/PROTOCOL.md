# Server protocol (from the Wibble Wobble 2.2.6 client)

The 2.2.6 Android client (archive.org: `YOKAIWATCHWibbleWobbleV2.2.6Apkpure.com`) is not packed and keeps its
C++ symbol names, unlike the 4.177.0 Puni Puni client. For each server call it has a `Protocol<Name>` class
(`doParamSet`: the request fields it sends besides the common ones) and `Response<Name>` parsers (the JSON keys
it reads). `Tools/extract_protocols.py` pulls them out of `lib/x86/libSGF.so`. Puni Puni 4.177.0 has more
calls and fields, but the shared ones work the same way.

Every request also carries the common fields (`appVer`, `deviceID`, `level5UserID`, `mstVersionVer`, `osType`, `userID`, `ywpToken`).

## `ageConfirm.nhn` (implemented)

* request: `age`
* response parts:
  * `AgeConfirm`
  * `EventPurchaseMaster`: ywp_mst_event
  * `UserPurchaseEvent`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `applyYoukaiMedalConfirm.nhn`

* request: `youkaiId`, `name1`, `name2`, `postalCode`, `address1`, `address2`, `address3`, `address4`, `tel`
* response parts:
  * `ApplyYoukaiMedalConfirm`; NullCheckError: paramName; ValueCheckError: paramName
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `applyYoukaiMedalExecute.nhn`

* request: `youkaiId`, `name1`, `name2`, `postalCode`, `address1`, `address2`, `address3`, `address4`, `tel`
* response parts:
  * `ApplyYoukaiMedalExecute`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `applyYoukaiMedalPostalCode.nhn`

* request: `name1`, `name2`, `postalCode`
* response parts:
  * `ApplyYoukaiMedalPostalCode`: responseCode, nullCheckErrorList, valueCheckErrorList, postalCodeResponseCode, address1, address2, address3; NullCheckError: paramName; ValueCheckError: paramName
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `applyYoukaiMedalStateCheck.nhn`

* request: `youkaiId`
* response parts:
  * `ApplyYoukaiMedalStateCheck`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `buyHitodama.nhn` (implemented)

* request: `goodsId`
* response parts:
  * `BuyHitodama`; After: hitodama, freeHitodama; Before: hitodama, freeHitodama
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `buyItem.nhn` (implemented)

* request: `goodsId`, `cnt`
* response parts:
  * `UserItemList`: ywp_user_item
  * `UserShopItemRemainCntList`: ywp_user_shop_item_remain_cnt
  * `BuyItem`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `checkTempDeliveried.nhn`

* request: (common fields only)
* response parts:
  * `CheckTempDeliveried`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `conflate.nhn` (implemented)

* request: `conflateId`
* response parts:
  * `Conflate`
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserItemList`: ywp_user_item
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `createUser.nhn` (implemented)

* request: `playerName`, `sexType`, `iconId`
* response parts:
  * `UserTutorialList`: ywp_user_tutorial_list
  * `CreateUserTabooWord`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `createWatch.nhn`

* request: `watchId`
* response parts:
  * `UserWatchList`: ywp_user_watch
  * `UserItemList`: ywp_user_item
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `deckEdit.nhn` (implemented)

* request: `youkaiId`
* response parts:
  * `UserYoukaiDeckList`: ywp_user_youkai_deck
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `deleteAllUser.nhn`

* request: `gdkeys`, `sno`, `ticket`
* response parts:
  * `DeleteAllUser`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `deleteUser.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `DeleteUser`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `driveContinue.nhn`

* request: `requestId`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `driveEnd.nhn`

* request: `requestId`, `score`, `resultFlg`, `suspendFlg`, `scoreLog`, `comboMax`

## `driveReset.nhn`

* request: (common fields only)
* response parts:
  * `UserEvent`: ywp_user_event
  * `EventMaster`: ywp_mst_event
  * `EventTutorialMaster`: ywp_mst_event_tutorial
  * `UserDriveProgress`: ywp_user_drive_progress
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `driveStart.nhn`

* request: (common fields only)
* response parts:
  * `DriveStart`: requestId, drivePointId, userYoukaiList, themeList, dropRewardList, buffValue, dummyYoukaiId, dummyEnemyId, scoreLogSendFlg, addHPByWatchEffect; DropReward: rewardType, rewardId, rewardCnt
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `equipWatch.nhn`

* request: `watchId`
* response parts:
  * `UserWatchList`: ywp_user_watch
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `eventPointTrade.nhn`

* request: `tradeCode`, `eventId`, `eventNo`, `tradeType`
* response parts:
  * `EventPointTrade`
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserShopItemUnlockList`: ywp_user_shop_item_unlock
  * `UserEventPointTrade`: ywp_user_event_point_trade
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `evolveYoukai.nhn` (implemented)

* request: `youkaiId`
* response parts:
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserYoukaiDeckList`: ywp_user_youkai_deck
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `findRobTarget.nhn`

* request: `treasureId`
* response parts:
  * `FindRobTarget`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friend.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `Friend`
  * `UserStageRank`: ywp_user_stage_rank
  * `UserFriendStage`: ywp_user_friend_stage
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendDelete.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `Friend`
  * `UserFriendStage`: ywp_user_friend_stage
  * `UserStageRank`: ywp_user_stage_rank
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendList.nhn`

* request: (common fields only)
* response parts:
  * `Friend`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendRaidBoss.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `UserEvent`: ywp_user_event
  * `UserRaidBossSelf`: ywp_user_raid_boss
  * `EventMaster`: ywp_mst_event
  * `RaidBossMaster`: ywp_mst_raid_boss
  * `FriendRaidBoss`; UserFriendRaidBoss: raidSeq, userId, raidId, lv, status, endTime, remainSec, hp, maxHp, helpFlg, playerName, youkaiId, iconId, titleId
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendRequest.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `Friend`
  * `FriendRequest`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendRequestAccept.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `FriendRequestAccept`
  * `Friend`
  * `UserStageRank`: ywp_user_stage_rank
  * `UserFriendStage`: ywp_user_friend_stage
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendRequestDelete.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `Friend`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `friendSearch.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `FriendSearch`; Friend: userId, characterId, playerName, titleId, iconId, youkaiId, lastPlayDt, lastPlayDtSentence
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gacha.nhn` (implemented)

* request: `gachaId`
* response parts:
  * `Gacha`: capsuleColor, rarityType, prizeType, item, ymoney, effectType, rouletteEffect, convertItemInfo, icon; RouletteEffect: rouletteEffectType, feverChargeType, color1Cnt, color2Cnt, color3Cnt, color4Cnt, color4NormalCnt; ConvertItemInfo: originalPrizeType, originalPrizeId, skillMaxYoukaiId; Icon: iconId; Item: isLimitOver, itemId, cnt; Ymoney: addYmoney, beforeYmoney, afterYmoney
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserItemList`: ywp_user_item
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserHistTotal`: ywp_user_hist_total
  * `UserGacha`: ywp_user_gacha
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserEvent`: ywp_user_event
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameContinue.nhn` (implemented)

* request: `requestId`, `stageId`, `battleType`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameContinueEvent.nhn`

* request: `requestId`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameContinueScoreAttack.nhn`

* request: `requestId`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameEnd.nhn` (implemented)

* request: `requestId`, `stageId`, `score`, `comboMax`, `eventPoint`, `cheatFlg`, `battleType`, `scoreLog`, `suspendFlg`

## `gameEndEvent.nhn`

* request: `requestId`, `score`, `endOrderNo`, `cheatFlg`, `scoreLog`

## `gameEndWeekly.nhn`

* request: `requestId`, `endOrderNo`, `comboMax`, `score`, `scoreLog`
* response parts:
  * `GameEndWeekly`; ScoreRankReward: rankin, topConditionRank, underConditionRank, rewardList
  * `EventMaster`: ywp_mst_event
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserMapList`: ywp_user_map
  * `UserHistTotal`: ywp_user_hist_total
  * `UserHistYoukaiTotalList`: ywp_user_hist_youkai_total
  * `UserHistPuzzleDailyList`: ywp_user_hist_puzzle_daily
  * `UserHistPuzzleWeeklyList`: ywp_user_hist_puzzle_weekly
  * `UserHistYoukaiDailyList`: ywp_user_hist_youkai_daily
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserRankAll`: ywp_user_all_rank
  * `UserRankFriend`: ywp_user_friend_rank
  * `UserRankFriendStar`: ywp_user_friend_star_rank
  * `UserRankSelf`; UserRankSelf: rank, score, remainSec
  * `DailyEventMapMaster`: ywp_mst_map
  * `UserEvent`: ywp_user_event
  * `CodeTeamEvent`
  * `UserStealProgress`
  * `UserScoreAttackReward`: ywp_user_score_attack_reward
  * `EventPlayerMessage`: eventPlayerMessage
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameRetire.nhn` (implemented)

* request: `requestId`, `stageId`, `score`, `comboMax`, `battleType`, `scoreLog`
* response parts:
  * `GameRetire`
  * `EventMaster`: ywp_mst_event
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserHistTotal`: ywp_user_hist_total
  * `UserHistYoukaiTotalList`: ywp_user_hist_youkai_total
  * `UserHistPuzzleDailyList`: ywp_user_hist_puzzle_daily
  * `UserHistPuzzleWeeklyList`: ywp_user_hist_puzzle_weekly
  * `UserHistYoukaiDailyList`: ywp_user_hist_youkai_daily
  * `UserEvent`: ywp_user_event
  * `UserStage`: ywp_user_stage
  * `CodeTeamEvent`
  * `UserStealProgress`
  * `UserScoreAttackReward`: ywp_user_score_attack_reward
  * `EventPlayerMessage`: eventPlayerMessage
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `UserEventProgressList`: ywp_user_event_progress
  * `DailyEventMapMaster`: ywp_mst_map
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameStart.nhn` (implemented)

* request: `stageId`, `retryFlg`, `battleType`, `battleCode`
* response parts:
  * `GameStart`: userYoukaiList, enemyYoukaiList, firstClearItemFlg, requestId, stageType, eventFlg, battleType, chanceAddRateEventBlock, eventStatus, eventPointMaterial, scoreLogSendFlg, themeList, themeScoreCoef, addHPByWatchEffect, buffValue
  * `EventMaster`: ywp_mst_event
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserEvent`: ywp_user_event
  * `UserItemList`: ywp_user_item
  * `UserEventProgressList`: ywp_user_event_progress
  * `TournamentYoukaiBossMaster`: ywp_mst_tournament_boss
  * `TournamentYoukaiBossGimmickMaster`: ywp_mst_tournament_boss_gimmick
  * `TournamentYoukaiBossBlockMaster`: ywp_mst_tournament_boss_block
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameStartEvent.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `GameStartEvent`: requestId, userYoukaiList, enemyYoukaiOrderList, addHPByWatchEffect, chanceTimeValue, continueInfoList, eventTargetIdList, responseCode, scoreLogSendFlg; ContinueInfo: continueNum, needMoney, recoveryValue; EventTargetId: eventTargetId; EnemyYoukaiOrder: enemyYoukaiList, addScore, addTime
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameStartWeekly.nhn`

* request: `weekSeq`
* response parts:
  * `GameStartWeekly`: requestId, freePlayFlg, responseCode, comboEnableSize, comboEnableSec, sizeCorrectKeyA, sizeCorrectSubKeyA, comboCorrectKey, sizeCorrectKeyB, addScoreCorrectKey, feverCorrectKey, damageSwitchSize, userYoukaiList, enemyYoukaiOrderList, itemValue, continueInfoList, targetIdList, scoreLogSendFlg, addHPByWatchEffect; EnemyYoukaiOrder: enemyYoukaiList, addScore, addTime; ContinueInfo: continueNum, needMoney, recoveryValue, addTime; TargetId: targetId
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `ScoreAttackMaster`: ywp_mst_score_attack
  * `BigBossMaster`: ywp_mst_big_boss
  * `BigBossEffectMaster`: ywp_mst_big_boss_effect
  * `ScoreAttackYoukaiAssistMaster`: ywp_mst_score_attack_youkai_assist
  * `ScoreAttackBreedMaster`: ywp_mst_score_attack_breed
  * `ScoreAttackBonusBlockLotMaster`: ywp_mst_bonus_block_lot
  * `UserItemList`: ywp_user_item
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `gameUseItem.nhn` (implemented)

* request: `requestId`, `enemyId`, `itemId`
* response parts:
  * `GameUseItem`
  * `UserItemList`: ywp_user_item
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getDownloadNo.nhn`

* request: `eventType`
* response parts:
  * `GetDownloadNo`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getEventMaster.nhn`

* request: `tableNames`
* response parts:
  * `GetEventMaster`
  * `EventMaster`: ywp_mst_event
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getEventRanking.nhn`

* request: `eventId`, `eventNo`, `rankTarget`, `rankType`, `rowStart`, `rowCnt`
* response parts:
  * `GetEventRanking`: ywp_user_all_rank_event, ywp_user_friend_rank_event, ywp_user_self_rank_event
  * `EventRankingRewardMaster`: ywp_mst_event_ranking_reward
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `UserSummaryRank`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getGdkeyAccounts.nhn` (implemented)

* request: `gdkeys`
* response parts:
  * `GetGdkeyAccounts`; UdKeyPlayer: userId, gdkey, playerName, playStartDate, lastUpdateDate, iconId, youkaiId, titleId
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getL5IdStatus.nhn`

* request: (common fields only)
* response parts:
  * `GetL5IdStatus`: L5IdStatus, rewardList, beforeCode, afterCode, maxCode, statusPoint, alertMsg; L5IdStatus: code, name, point; Reward: rewardCode
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getLimitHitodama.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `LimitHitodama`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getMaster.nhn` (implemented)

* request: `tableNames`

## `getMission.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `DailyEventMissionMaster`: ywp_mst_mission
  * `UserMission`: ywp_user_mission
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getMissionAlreadyReward.nhn`

* request: (common fields only)
* response parts:
  * `GetMissionAlreadyReward`: ywp_user_mission
  * `DailyEventMissionMaster`: ywp_mst_mission
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getPresentBox.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `GetPresentBox`: ywp_user_present_box_list
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getRanking.nhn` (implemented)

* request: `rankType`, `rowStart`, `rowCnt`
* response parts:
  * `GetRanking`: ywp_user_self_rank, scoreAttackRewardData; ScoreAttackRewardData: completeScore, nextScore, rewardMoney
  * `UserScoreAttackReward`: ywp_user_score_attack_reward
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `getTournamentMessage.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `GetTournamentMessage`: ywp_user_tournament_message; MessageInfo: messageType, speakerName, speakerResName, messageDt, readFlg, message; OneLineMessage: iconType, iconId, body
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `giveUpTournament.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `DailyEventMapMaster`: ywp_mst_map
  * `UserEvent`: ywp_user_event
  * `EventMaster`: ywp_mst_event
  * `UserEventProgressList`: ywp_user_event_progress
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `init.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `Init`: dispNoticeFlg, isEnableFriendInvite, isEnableSerialCode, isEnableL5ID, l5idUrl, imgServer, apkey, isEnableYoukaiMedal, isEnableYoukaiLand, masterReacquisitionHour, threeKingdomTeamEventButtonHiddenFlg, isEnablePurchase
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `initGacha.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `InitGacha`
  * `UserGacha`: ywp_user_gacha
  * `GachaMaster`: ywp_mst_gacha
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `initRaidBoss.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `UserEvent`: ywp_user_event
  * `UserRaidBossSelf`: ywp_user_raid_boss
  * `EventMaster`: ywp_mst_event
  * `UserRaidBossAttack`; UserRaidBossAttack: raidSeq, userId, attackCnt, maxDamage, totDamage, playerName, youkaiId, iconId, titleId
  * `RaidBossMaster`: ywp_mst_raid_boss
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `initScoreAttack.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `InitScoreAttack`
  * `ScoreAttackMaster`: ywp_mst_score_attack
  * `ScoreAttackItemMaster`: ywp_mst_score_attack_item
  * `ScoreAttackRewardMaster`: ywp_mst_score_attack_reward
  * `BigBossMaster`: ywp_mst_big_boss
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `initTournament.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `UserEvent`: ywp_user_event
  * `EventMaster`: ywp_mst_event
  * `UserEventProgressList`: ywp_user_event_progress
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `initWatch.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `UserWatchList`: ywp_user_watch
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `itemDelivery.nhn`

* request: `isDelivery`, `sno`
* response parts:
  * `ItemDelivery`
  * `GetL5IdStatus`: L5IdStatus, rewardList, beforeCode, afterCode, maxCode, statusPoint, alertMsg; L5IdStatus: code, name, point; Reward: rewardCode
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `levelLockOff.nhn` (implemented)

* request: `youkaiId`
* response parts:
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `login.nhn` (implemented)

* request: `gdkeyValue`, `udkeyValue`, `signNonce`, `osVersion`, `deviceName`, `modelName`, `level`, `state`, `technology`, `voltage`

## `loginStamp.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `LoginStamp`; LoginStamp: isStep, loginDayCnt, stampId, userId, stampMaxCnt, description, mainResName, titleResName, headerResName, footerResName, cautionResName; MstLoginStamp: stampId, startDt, endDt, description, mainResName, titleResName, headerResName, footerResName, cautionResName; MstLoginStampReward: stampId, rewardDayCnt, rewardItemType, rewardItemId, rewardItemCnt
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `map.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `UserMapList`: ywp_user_map
  * `EventMaster`: ywp_mst_event
  * `DailyEventMapMaster`: ywp_mst_map
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `mapUnLock.nhn` (implemented)

* request: `mapId`, `unLockType`, `youkaiId`
* response parts:
  * `EventMaster`: ywp_mst_event
  * `DailyEventMapMaster`: ywp_mst_map
  * `UserMapList`: ywp_user_map
  * `UserStage`: ywp_user_stage
  * `UserStageRank`: ywp_user_stage_rank
  * `UserFriendStage`: ywp_user_friend_stage
  * `UserEvent`: ywp_user_event
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `mapUnLockRequest.nhn`

* request: `mapId`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `mapWarp.nhn` (implemented)

* request: `mapId`
* response parts:
  * `MapWarp`: responseCodeEventEffect, responseObjectEventEffect
  * `EventMaster`: ywp_mst_event
  * `DailyEventMapMaster`: ywp_mst_map
  * `UserMapList`: ywp_user_map
  * `UserStage`: ywp_user_stage
  * `UserStageRank`: ywp_user_stage_rank
  * `UserFriendStage`: ywp_user_friend_stage
  * `VerMapMobMaster`; MapMob_info: version, data, tableData
  * `UserEvent`: ywp_user_event
  * `UserSummaryRank`
  * `SummaryMaster`: ywp_mst_summary
  * `CodeTeamEvent`
  * `UserStealProgress`
  * `UserScoreAttackReward`: ywp_user_score_attack_reward
  * `EventTutorialMaster`: ywp_mst_event_tutorial
  * `EventTutorialMessageMaster`: ywp_mst_event_tutorial_message
  * `EventPlayerMessage`: eventPlayerMessage
  * `UserRaidBossSelf`: ywp_user_raid_boss
  * `RaidBossMaster`: ywp_mst_raid_boss
  * `UserEventTutorial`: ywp_user_event_tutorial
  * `UserTutorialList`: ywp_user_tutorial_list
  * `UserEventRankingReward`: ywp_user_event_ranking_reward
  * `UserEventProgressList`: ywp_user_event_progress
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `mapWarpTeamEvent.nhn`

* request: `eventId`, `eventNo`, `debugDt`
* response parts:
  * `SummaryMaster`: ywp_mst_summary
  * `MapWarpTeamEvent`: responseCode, mapId, eventId, eventNo, teamEventDispNum, recommendSummaryId, rewardCategList, teamEventFinalFlg; RewardCateg: rewardCateg, rewardList
  * `MapWarp`: responseCodeEventEffect, responseObjectEventEffect
  * `EventMaster`: ywp_mst_event
  * `DailyEventMapMaster`: ywp_mst_map
  * `UserMapList`: ywp_user_map
  * `UserStage`: ywp_user_stage
  * `UserStageRank`: ywp_user_stage_rank
  * `UserFriendStage`: ywp_user_friend_stage
  * `VerMapMobMaster`; MapMob_info: version, data, tableData
  * `UserEvent`: ywp_user_event
  * `UserSummaryRank`
  * `CodeTeamEvent`
  * `UserStealProgress`
  * `GetEventRanking`: ywp_user_all_rank_event, ywp_user_friend_rank_event, ywp_user_self_rank_event
  * `EventTutorialMaster`: ywp_mst_event_tutorial
  * `EventTutorialMessageMaster`: ywp_mst_event_tutorial_message
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `medalPointTrade.nhn`

* request: `tradeCode`
* response parts:
  * `MedalPointTrade`
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserShopItemUnlockList`: ywp_user_shop_item_unlock
  * `UserMedalPointTrade`: ywp_user_medal_point_trade
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `missionReward.nhn` (implemented)

* request: `missionId`, `seqNo`
* response parts:
  * `DailyEventMissionMaster`: ywp_mst_mission
  * `UserMission`: ywp_user_mission
  * `UserYoukaiList`: ywp_user_youkai
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserShopItemUnlockList`: ywp_user_shop_item_unlock
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `MissionReward`; Item: isLimitOver, itemId, cnt, isCashed, exchgYmoney
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `missionRewardAll.nhn`

* request: (common fields only)
* response parts:
  * `DailyEventMissionMaster`: ywp_mst_mission
  * `UserMission`: ywp_user_mission
  * `UserYoukaiList`: ywp_user_youkai
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserShopItemUnlockList`: ywp_user_shop_item_unlock
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `MissionRewardAll`; Recieve: limitOver, itemType, itemId, itemCnt
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `onedariFriendList.nhn`

* request: (common fields only)
* response parts:
  * `Friend`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `onedariHitodama.nhn`

* request: (common fields only)
* response parts:
  * `Friend`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `presentHitodama.nhn`

* request: `eventId`, `eventNo`, `rankTarget`
* response parts:
  * `UserFriendStage`: ywp_user_friend_stage
  * `UserEventRankFriend`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `prevGachaData.nhn`

* request: (common fields only)
* response parts:
  * `Gacha`: capsuleColor, rarityType, prizeType, item, ymoney, effectType, rouletteEffect, convertItemInfo, icon; RouletteEffect: rouletteEffectType, feverChargeType, color1Cnt, color2Cnt, color3Cnt, color4Cnt, color4NormalCnt; ConvertItemInfo: originalPrizeType, originalPrizeId, skillMaxYoukaiId; Icon: iconId; Item: isLimitOver, itemId, cnt; Ymoney: addYmoney, beforeYmoney, afterYmoney
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill

## `publishInviteCode.nhn`

* request: (common fields only)
* response parts:
  * `PublishInviteCode`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `purchaseConfirm.nhn`

* request: `payCoinId`
* response parts:
  * `PurchaseConfirm`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `receivePresentBox.nhn`

* request: `seqs`
* response parts:
  * `ReceivePresentBox`; Youkai: youkaiId, getFlg, exchgYmoney, skill
  * `GetPresentBox`: ywp_user_present_box_list
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserItemList`: ywp_user_item
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserMapList`: ywp_user_map
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `recvEventReward.nhn`

* request: `eventId`, `eventNo`, `rewardNo`
* response parts:
  * `RecvEventReward`; Item: isLimitOver, itemId, cnt
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserTutorialList`: ywp_user_tutorial_list
  * `EventMaster`: ywp_mst_event
  * `EventRankingRewardMaster`: ywp_mst_event_ranking_reward
  * `UserEvent`: ywp_user_event
  * `UserYoukaiList`: ywp_user_youkai
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `recvEventRewardAll.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `RecvEventRewardAll`
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserTutorialList`: ywp_user_tutorial_list
  * `EventMaster`: ywp_mst_event
  * `EventRankingRewardMaster`: ywp_mst_event_ranking_reward
  * `UserEvent`: ywp_user_event
  * `UserYoukaiList`: ywp_user_youkai
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `releaseYoukai.nhn` (implemented)

* request: `youkaiId`
* response parts:
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserYoukaiLegendReleaseHistory`: ywp_user_youkai_legend_release_history
  * `UserHistTotal`: ywp_user_hist_total
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `resultEventRanking.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `ResultEventRanking`; EventRankingResult: rankTarget, difficulty, rank, score, rewardList
  * `UserPresentBox`: ywp_user_present_box
  * `UserEventRankingReward`: ywp_user_event_ranking_reward
  * `EventRankingRewardMaster`: ywp_mst_event_ranking_reward
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `resultScoreAttack.nhn`

* request: (common fields only)
* response parts:
  * `ResultScoreAttack`: rankAllGrade, rankFriendGrade, score, friendCnt, rankAllRewardList, rankFriendRewardList, clearScoreRewardList; RewardCommon: rewardType, rewardId, rewardCnt
  * `UserScoreAttackReward`: ywp_user_score_attack_reward
  * `ScoreAttackRewardMaster`: ywp_mst_score_attack_reward
  * `UserHistTotal`: ywp_user_hist_total
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `robTreasure.nhn`

* request: `treasureId`, `totalPower`, `execMode`
* response parts:
  * `RobTreasure`; TargetDeck: youkaiId, power, specialEffect, compatibilityEffect; SelfDeck: youkaiId, power, specialEffect, compatibilityEffect; RobResult: treasureId, winLose, selfDeck, targetDeck
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserHistTotal`: ywp_user_hist_total
  * `UserItemList`: ywp_user_item
  * `UserTreasureSeriesList`: ywp_user_treasure_series
  * `UserTreasureList`: ywp_user_treasure
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `selectTeam.nhn`

* request: `eventId`, `eventNo`, `summaryId`
* response parts:
  * `SelectTeam`: responseCode, mapId, responseCodeTeamEvent
  * `EventMaster`: ywp_mst_event
  * `DailyEventMapMaster`: ywp_mst_map
  * `UserMapList`: ywp_user_map
  * `UserStage`: ywp_user_stage
  * `UserStageRank`: ywp_user_stage_rank
  * `UserFriendStage`: ywp_user_friend_stage
  * `VerMapMobMaster`; MapMob_info: version, data, tableData
  * `UserEvent`: ywp_user_event
  * `UserSummaryRank`
  * `SummaryMaster`: ywp_mst_summary
  * `UserStealProgress`
  * `EventPlayerMessage`: eventPlayerMessage
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `serialConfirm.nhn` (implemented)

* request: `eventCode`, `serialCode`
* response parts:
  * `SerialConfirm`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `serialDist.nhn`

* request: `eventCode`, `serialCode`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `shootEnd.nhn`

* request: `requestId`, `score`, `cheatFlg`, `youkaiId`
* response parts:
  * `ShootEnd`; UserShootResultData: score, scoreUpdateFlg, exp, money, prevEnemyHp, enemyHp, enemyMaxHp, naturePoint, point, hpDeadLineFlg, damage, lv, killCnt, remainSec, endTime
  * `EventMaster`: ywp_mst_event
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserMapList`: ywp_user_map
  * `UserStage`: ywp_user_stage
  * `UserItemList`: ywp_user_item
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserShopItemUnlockList`: ywp_user_shop_item_unlock
  * `UserTutorialList`: ywp_user_tutorial_list
  * `UserEventTutorial`: ywp_user_event_tutorial
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserEvent`: ywp_user_event
  * `UserRaidBossSelf`: ywp_user_raid_boss
  * `CodeTeamEvent`
  * `RaidBossMaster`: ywp_mst_raid_boss
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `shootStart.nhn`

* request: `raidId`, `raidSeq`, `helpFlg`
* response parts:
  * `ShootStart`: requestId, userYoukaiList, enemyYoukaiList, shootCourseList, helpFlg, scoreLogSendFlg, addHPByWatchEffect, buffValue, shootAngularMargin, shootReshootSpeed, shootMinSpeed, shootMaxSpeed, shootAdditionalSpeedRatio, shootPuniPullPower, shootGimmickTrailSpeed, shootDebuffMinSpeed, shootPuniSizeToScaleRatioCoef, shootSizeUpSeLevel, shootSizeUpEffectLevel, shootDefaultElasticStrength, shootDefaultPressureStrength, shootSizeUpElasticStrengthCoef, shootSizeUpPressureStrengthCoef, shootBoundElasticStrength, shootBoundPressureStrength, shootBoundStrengthCoef, shootDamageCoef, shootScoreCoef, shootSpeedAdjustCoef, shootPuniSizeCoef, shootGradeCoef, responseCode; ShootCourse: itemType, itemId, itemCnt, itemIdx, mstGameShoot
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `WatchVerticalFrameMaster`: ywp_mst_watch_vertical_frame
  * `GameShootGimmickMaster`: ywp_mst_game_shoot_gimmick_size
  * `UserRaidBossSelf`: ywp_user_raid_boss
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `stealEnd.nhn`

* request: (common fields only)
* response parts:
  * `StealEnd`: responseCode, stealResultType, userStealResultData, teamStealResultData, beforeUserYoukaiList, userYoukaiResultList, userItemResultList, responseCodeTeamEvent; UserItemResult: itemType, itemId, itemCnt, newFlg; TeamStealResultData: mySummaryId, myAddValue, doubleUpId, noRateAddValue, myTotalValue, targetSummaryId, targetAddValue, targetTotalValue; UserStealResultData: rewardYoukaiId, exp, power, addEventValue, noRateAddEventValue, prevRank, rank, eventValue, prevEventValue, eventValueUpdateFlg, isMaxItemFlg
  * `Youkai`; Youkai: youkaiId, getType, exchgYmoney, legendYoukaiId, skill
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserMapList`: ywp_user_map
  * `UserStage`: ywp_user_stage
  * `UserFriendStage`: ywp_user_friend_stage
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserMenuFuncList`: ywp_user_menufunc
  * `UserItemList`: ywp_user_item
  * `UserTutorialList`: ywp_user_tutorial_list
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `UserEventRankAll`
  * `UserEventRankFriend`
  * `UserEventRankSelf`: ywp_user_self_rank_event; UserEventRankSelf: rank, score, remainSec
  * `UserSummaryRank`
  * `UserStealProgress`
  * `UserScoreAttackReward`: ywp_user_score_attack_reward
  * `UserHistTotal`: ywp_user_hist_total
  * `StealDoubleUpMaster`: ywp_mst_steal_double_up
  * `EventPlayerMessage`: eventPlayerMessage
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `stealStart.nhn`

* request: `stealPower`, `stealSec`, `youkaiId`
* response parts:
  * `StealStart`
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `UserStealProgress`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `stealStop.nhn`

* request: (common fields only)
* response parts:
  * `StealStop`
  * `EventMaster`: ywp_mst_event
  * `UserEvent`: ywp_user_event
  * `UserStealProgress`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `storeReview.nhn`

* request: (common fields only)
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `updateEventTutorialFlg.nhn` (implemented)

* request: `eventId`, `eventNo`, `tutorialId`
* response parts:
  * `UserEventTutorial`: ywp_user_event_tutorial
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `updateProfile.nhn` (implemented)

* request: `iconId`, `titleId`
* response parts:
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `updateTeamResultReadFlg.nhn`

* request: `eventId`, `eventNo`
* response parts:
  * `UpdateTeamResultReadFlg`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `updateTutorialFlg.nhn` (implemented)

* request: `tutorialId`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `updateWatchReadFlg.nhn` (implemented)

* request: `watchId`
* response parts:
  * `UserWatchList`: ywp_user_watch
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `useAddition.nhn` (implemented)

* request: `objectType`
* response parts:
  * `UseAddition`
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `useItem.nhn` (implemented)

* request: `itemId`, `youkaiId`
* response parts:
  * `UseItem`; YoukaiSkillExp: youkaiId, before, after; Hitodama: before, after
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserItemList`: ywp_user_item
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `userInfoRefresh.nhn` (implemented)

* request: (common fields only)
* response parts:
  * `UserInfoRefresh`
  * `UserPurchaseEvent`
  * `UserItemList`: ywp_user_item
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `userStageRank.nhn`

* request: `stageId`
* response parts:
  * `UserStageRank`: ywp_user_stage_rank
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `writeAppLog.nhn`

* request: `logKind`, `logText`
* response parts:
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `youkaiMedalInit.nhn`

* request: (common fields only)
* response parts:
  * `YoukaiMedalInit`; YoukaiMedal: medalCode, youkaiName, activated, detailImage, medalImage, medalType
  * `UserYoukaiMedalCnt`: ywp_user_youkai_medal_cnt
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master

## `youkaiMedalRegist.nhn`

* request: `medalCode`
* response parts:
  * `YoukaiMedalRegist`
  * `UserYoukaiMedalCnt`: ywp_user_youkai_medal_cnt
  * `UserYoukaiList`: ywp_user_youkai
  * `UserYoukaiSkillList`: ywp_user_youkai_skill
  * `UserDictionaryList`: ywp_user_dictionary
  * `UserItemList`: ywp_user_item
  * `UserPlayerIconList`: ywp_user_player_icon
  * `UserPlayerTitleList`: ywp_user_player_title
  * `UserYoukaiBonusList`: ywp_user_youkai_bonus_effect
  * `AllCommon`: resultType, resultCode, dialogMsg, nextScreenType, webServerIp, storeUrl, mstVersionMaster, token, dialogTitle, serverId, ywp_user_data, isYoukaiLandLinked; UserData: userId, playerName, ymoney, chargeYmoney, freeHitodama, hitodama, hitodamaRecoverSec, todaysRemainSec, nowStageId, characterId, iconId, titleId, moveReason, weeklyFreeFlg, friendMaxCnt, reviewFlg, medalPoint, totMedalPoint, equipWatchId, limitTimeSaleRemainSec
  * `UserIconBudgeList`: ywp_user_icon_budge
  * `MasterVersionMaster`: ywp_mst_version_master
