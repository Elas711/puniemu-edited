# Puni Puni 4.177.0 client endpoints

Every `.nhn` endpoint the 4.177.0 Android client (`libSGF.so`) can call, and whether Puniemu answers it.
Puniemu implements **55** of the **164**; **11** fire-and-forget calls get a plain success answer.
Every other unknown request is recorded with its decrypted body (admin panel -> Logs -> Unimplemented requests):
play on your server, then use those samples to implement the endpoint.

Legend: **yes** implemented, *ok* acknowledged (success answer, nothing stored), blank: not implemented yet.

## Events

| Endpoint | Puniemu |
|---|---|
| `appearRaidBoss.nhn` |  |
| `assistEventArea.nhn` |  |
| `chooseVoteCandidate.nhn` |  |
| `eventPointTrade.nhn` |  |
| `friendEventArea.nhn` |  |
| `friendRaidBoss.nhn` |  |
| `gameContinueEvent.nhn` |  |
| `gameEndEvent.nhn` |  |
| `gameStartEvent.nhn` |  |
| `getBingoEventInfo.nhn` |  |
| `getEventCondition.nhn` |  |
| `getEventMaster.nhn` | **yes** |
| `getEventMessage.nhn` |  |
| `getEventPointTrade.nhn` |  |
| `getEventQuest.nhn` |  |
| `getEventRanking.nhn` |  |
| `getTournamentMessage.nhn` |  |
| `giveUpTournament.nhn` |  |
| `initAppearRaidBoss.nhn` |  |
| `initRaidBoss.nhn` |  |
| `initTournament.nhn` |  |
| `mapWarpTeamEvent.nhn` |  |
| `recvAssistEventArea.nhn` |  |
| `recvEventAreaReward.nhn` |  |
| `recvEventReward.nhn` |  |
| `recvEventRewardAll.nhn` |  |
| `resultEventArea.nhn` |  |
| `resultEventRanking.nhn` |  |
| `selectTeam.nhn` |  |
| `updateEventTutorialFlg.nhn` | **yes** |
| `updateTeamResultReadFlg.nhn` | *ok* |
| `useEventAddition.nhn` |  |

## Battles and modes

| Endpoint | Puniemu |
|---|---|
| `driveContinue.nhn` |  |
| `driveEnd.nhn` |  |
| `driveReset.nhn` |  |
| `driveStart.nhn` |  |
| `findRobTarget.nhn` |  |
| `friendMiniGameMap.nhn` |  |
| `gameContinue.nhn` | **yes** |
| `gameContinueScoreAttack.nhn` |  |
| `gameEnd.nhn` | **yes** |
| `gameEndScoreAttack.nhn` | **yes** |
| `gameRetire.nhn` | **yes** |
| `gameStart.nhn` | **yes** |
| `gameStartScoreAttack.nhn` | **yes** |
| `gameUseItem.nhn` | **yes** |
| `getShootMessage.nhn` |  |
| `initMiniGameMap.nhn` |  |
| `initScoreAttack.nhn` | **yes** |
| `initScoreAttackPointTrade.nhn` |  |
| `miniGameEnd.nhn` |  |
| `miniGameStart.nhn` |  |
| `resetNekomataProgress.nhn` |  |
| `resultMiniGameMap.nhn` |  |
| `resultScoreAttack.nhn` |  |
| `robTreasure.nhn` |  |
| `robTreasureEntry.nhn` |  |
| `scoreAttackPointTrade.nhn` |  |
| `shootEnd.nhn` |  |
| `shootStart.nhn` |  |
| `stealEnd.nhn` |  |
| `stealStart.nhn` |  |
| `stealStop.nhn` |  |
| `updateNekomataClearFlg.nhn` | *ok* |
| `updateNekomataInkFlg.nhn` | *ok* |
| `updateTrainingTutorialFlg.nhn` | *ok* |

## Rewards, missions, presents

| Endpoint | Puniemu |
|---|---|
| `applyYoukaiMedalConfirm.nhn` |  |
| `applyYoukaiMedalExecute.nhn` |  |
| `applyYoukaiMedalPostalCode.nhn` |  |
| `applyYoukaiMedalStateCheck.nhn` |  |
| `buyHitodama.nhn` | **yes** |
| `getMission.nhn` | **yes** |
| `getMissionAlreadyReward.nhn` |  |
| `getPresentBox.nhn` | **yes** |
| `medalPointTrade.nhn` |  |
| `missionReward.nhn` | **yes** |
| `missionRewardAll.nhn` | **yes** |
| `onedariFriendList.nhn` |  |
| `onedariHitodama.nhn` |  |
| `presentHitodama.nhn` |  |
| `receivePresentBox.nhn` | **yes** |
| `recvAdsPlayReward.nhn` |  |
| `recvComebackCampaignReward.nhn` |  |
| `recvVipGacha.nhn` |  |
| `recvWinReward.nhn` |  |
| `serialConfirm.nhn` | **yes** |
| `serialDist.nhn` |  |
| `youkaiMedalInit.nhn` |  |

## Crank-a-kai and shop

| Endpoint | Puniemu |
|---|---|
| `buyDeck.nhn` |  |
| `buyItem.nhn` | **yes** |
| `deferAdsPlay.nhn` | *ok* |
| `distVipGacha.nhn` |  |
| `executeContinueGacha.nhn` |  |
| `executeGacha.nhn` | **yes** |
| `executeVipGacha.nhn` |  |
| `getGachaResult.nhn` |  |
| `getPurchaseResult.nhn` |  |
| `initBilling.nhn` | **yes** |
| `initGacha.nhn` | **yes** |
| `purchaseFailure.nhn` | *ok* |
| `purchaseStart.nhn` |  |
| `purchaseSuccess.nhn` |  |
| `userPurchaseItemPage.nhn` |  |

## Friends and social

| Endpoint | Puniemu |
|---|---|
| `friend.nhn` | **yes** |
| `friendDelete.nhn` | **yes** |
| `friendRequest.nhn` | **yes** |
| `friendRequestAccept.nhn` | **yes** |
| `friendRequestDelete.nhn` | **yes** |
| `friendSearch.nhn` | **yes** |
| `initReferral.nhn` |  |
| `mapUnLock.nhn` | **yes** |
| `mapUnLockFriendList.nhn` |  |
| `mapUnLockRequest.nhn` |  |
| `publishInviteCode.nhn` |  |

## Account and misc

| Endpoint | Puniemu |
|---|---|
| `birthdayRegist.nhn` |  |
| `comebackCampaignLogin.nhn` |  |
| `conflate.nhn` | **yes** |
| `createUser.nhn` | **yes** |
| `createWatch.nhn` |  |
| `deckEdit.nhn` | **yes** |
| `deckEditMulti.nhn` |  |
| `deleteAllUser.nhn` |  |
| `deleteUser.nhn` | **yes** |
| `equipWatch.nhn` |  |
| `evolveYoukai.nhn` | **yes** |
| `getCampaignInfo.nhn` |  |
| `getComebackCampaignInfo.nhn` |  |
| `getDownloadNo.nhn` |  |
| `getGdkeyAccounts.nhn` | **yes** |
| `getMaster.nhn` | **yes** |
| `getProductionInfo.nhn` |  |
| `getRanking.nhn` | **yes** |
| `getSurveyInfo.nhn` |  |
| `IdStatus.nhn` |  |
| `initCollectMenu.nhn` | **yes** |
| `initCrystal.nhn` | **yes** |
| `initGoku.nhn` | **yes** |
| `initWatch.nhn` | **yes** |
| `levelLockOff.nhn` | **yes** |
| `login.nhn` | **yes** |
| `loginStamp.nhn` | **yes** |
| `mapWarp.nhn` | **yes** |
| `noticeBannerPage.nhn` |  |
| `releaseYoukai.nhn` | **yes** |
| `rename.nhn` | **yes** |
| `storeReview.nhn` | *ok* |
| `top.nhn` |  |
| `updateCampaignInfo.nhn` | *ok* |
| `updateCollectMenu.nhn` | **yes** |
| `updateCrystalMenu.nhn` | **yes** |
| `updateGokuMenu.nhn` | **yes** |
| `updateGokuStory.nhn` | **yes** |
| `updateMyfave.nhn` | *ok* |
| `updateProductionInfo.nhn` |  |
| `updateProductionMaterialInfo.nhn` |  |
| `updateProfile.nhn` | **yes** |
| `updateTutorialFlg.nhn` | **yes** |
| `updateWatchReadFlg.nhn` | **yes** |
| `useAddition.nhn` | **yes** |
| `useItem.nhn` | **yes** |
| `userInfoRefresh.nhn` | **yes** |
| `userStageRanking.nhn` | **yes** |
| `viewpage.nhn` |  |
| `writeAppLog.nhn` | *ok* |
