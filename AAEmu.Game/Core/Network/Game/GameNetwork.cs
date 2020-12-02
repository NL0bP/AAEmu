using System;
using System.Net;

using AAEmu.Commons.Network.Type;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Packets.C2G;
using AAEmu.Game.Core.Packets.Proxy;
using AAEmu.Game.Models;

using NLog;

namespace AAEmu.Game.Core.Network.Game
{
    public class GameNetwork : Singleton<GameNetwork>
    {
        private Server _server;
        private GameProtocolHandler _handler;
        private static Logger _log = LogManager.GetCurrentClassLogger();

        private GameNetwork()
        {
            _handler = new GameProtocolHandler();

            // World
            RegisterPacket(CSOffsets.X2ClientToWorldPacket, 1, typeof(X2ClientToWorldPacket));
            RegisterPacket(CSOffsets.CSCofferInteractionPacket, 1, typeof(CSCofferInteractionPacket));
            //RegisterPacket(CSOffsets.CSRemoveAllDoodadFromCellPacket, 1, typeof(CSRemoveAllDoodadFromCellPacket));
            //RegisterPacket(CSOffsets.CSAddDoodadToCellEndedPacket, 1, typeof(CSAddDoodadToCellEndedPacket));
            //RegisterPacket(CSOffsets.CSRemoveCommonFarmsPacket, 1, typeof(CSRemoveCommonFarmsPacket));
            //RegisterPacket(CSOffsets.CSAddDoodadToCellPacket, 1, typeof(CSAddDoodadToCellPacket));
            //RegisterPacket(CSOffsets.CSPlaceCommonFarmPacket, 1, typeof(CSPlaceCommonFarmPacket));
            //RegisterPacket(CSOffsets.CSSetDoodadTimeAccelPacket, 1, typeof(CSSetDoodadTimeAccelPacket));
            //RegisterPacket(CSOffsets.CSRequestCommonFarmListPacket, 1, typeof(CSRequestCommonFarmListPacket));
            RegisterPacket(CSOffsets.CSChallengeDuelPacket, 1, typeof(CSChallengeDuelPacket));
            RegisterPacket(CSOffsets.CSStartDuelPacket, 1, typeof(CSStartDuelPacket));
            RegisterPacket(CSOffsets.CSSetPingPosPacket, 1, typeof(CSSetPingPosPacket));
            RegisterPacket(CSOffsets.CSChangeMateNamePacket, 1, typeof(CSChangeMateNamePacket));
            //RegisterPacket(CSOffsets.CSSendUserMusicPacket, 1, typeof(CSSendUserMusicPacket));
            RegisterPacket(CSOffsets.CSReplyImprisonOrTrialPacket, 1, typeof(CSReplyImprisonOrTrialPacket));
            RegisterPacket(CSOffsets.CSReplyInviteJuryPacket, 1, typeof(CSReplyInviteJuryPacket));
            RegisterPacket(CSOffsets.CSJurySummonedPacket, 1, typeof(CSJurySummonedPacket));
            RegisterPacket(CSOffsets.CSJuryEndTestimonyPacket, 1, typeof(CSJuryEndTestimonyPacket));
            RegisterPacket(CSOffsets.CSCancelTrialPacket, 1, typeof(CSCancelTrialPacket));
            RegisterPacket(CSOffsets.CSJuryVerdictPacket, 1, typeof(CSJuryVerdictPacket));
            RegisterPacket(CSOffsets.CSReportCrimePacket, 1, typeof(CSReportCrimePacket));
            RegisterPacket(CSOffsets.CSRequestJuryWaitingNumberPacket, 1, typeof(CSRequestJuryWaitingNumberPacket));
            //RegisterPacket(CSOffsets.CSRequestSetBountyMoneyPacket, 1, typeof(CSRequestSetBountyMoneyPacket));
            //RegisterPacket(CSOffsets.CSUpdateBountyPacket, 1, typeof(CSUpdateBountyPacket));
            RegisterPacket(CSOffsets.CSSkillControllerStatePacket, 1, typeof(CSSkillControllerStatePacket));
            RegisterPacket(CSOffsets.CSMountMatePacket, 1, typeof(CSMountMatePacket));
            RegisterPacket(CSOffsets.CSMoveUnitPacket, 1, typeof(CSMoveUnitPacket));
            RegisterPacket(CSOffsets.CSCompletedCinemaPacket, 1, typeof(CSCompletedCinemaPacket));
            //RegisterPacket(CSOffsets.CSCheckDemoModePacket, 1, typeof(CSCheckDemoModePacket));
            //RegisterPacket(CSOffsets.CSResetDemoCharPacket, 1, typeof(CSResetDemoCharPacket));
            RegisterPacket(CSOffsets.CSConsoleCmdUsedPacket, 1, typeof(CSConsoleCmdUsedPacket));
            RegisterPacket(CSOffsets.CSEditorGameModePacket, 1, typeof(CSEditorGameModePacket));
            RegisterPacket(CSOffsets.CSEditorRemoveGimmickPacket, 1, typeof(CSEditorRemoveGimmickPacket));
            RegisterPacket(CSOffsets.CSInteractGimmickPacket, 1, typeof(CSInteractGimmickPacket));
            RegisterPacket(CSOffsets.CSEditorAddGimmickPacket, 1, typeof(CSEditorAddGimmickPacket));
            RegisterPacket(CSOffsets.CSGmCommandPacket, 1, typeof(CSGmCommandPacket));
            RegisterPacket(CSOffsets.CSWorldRayCastingPacket, 1, typeof(CSWorldRayCastingPacket));
            RegisterPacket(CSOffsets.CSListCharacterPacket, 1, typeof(CSListCharacterPacket));
            RegisterPacket(CSOffsets.CSRefreshInCharacterListPacket, 1, typeof(CSRefreshInCharacterListPacket));
            RegisterPacket(CSOffsets.CSDeleteCharacterPacket, 1, typeof(CSDeleteCharacterPacket));
            RegisterPacket(CSOffsets.CSCancelCharacterDeletePacket, 1, typeof(CSCancelCharacterDeletePacket));
            RegisterPacket(CSOffsets.CSSelectCharacterPacket, 1, typeof(CSSelectCharacterPacket));
            RegisterPacket(CSOffsets.CSNotifyInGamePacket, 1, typeof(CSNotifyInGamePacket));
            RegisterPacket(CSOffsets.CSNotifyInGameCompletedPacket, 1, typeof(CSNotifyInGameCompletedPacket));
            RegisterPacket(CSOffsets.CSChangeTargetPacket, 1, typeof(CSChangeTargetPacket));
            RegisterPacket(CSOffsets.CSResurrectCharacterPacket, 1, typeof(CSResurrectCharacterPacket));
            RegisterPacket(CSOffsets.CSCriminalLockedPacket, 1, typeof(CSCriminalLockedPacket));
            RegisterPacket(CSOffsets.CSExpressEmotionPacket, 1, typeof(CSExpressEmotionPacket));
            RegisterPacket(CSOffsets.CSUnhangPacket, 1, typeof(CSUnhangPacket));
            RegisterPacket(CSOffsets.CSChangeAppellationPacket, 1, typeof(CSChangeAppellationPacket));
            RegisterPacket(CSOffsets.CSStartedCinemaPacket, 1, typeof(CSStartedCinemaPacket));
            //RegisterPacket(CSOffsets.CSHSResponsePacket, 1, typeof(CSHSResponsePacket));
            RegisterPacket(CSOffsets.CSBroadcastVisualOptionPacket, 1, typeof(CSBroadcastVisualOptionPacket));
            RegisterPacket(CSOffsets.CSRestrictCheckPacket, 1, typeof(CSRestrictCheckPacket));
            RegisterPacket(CSOffsets.CSICSMenuListPacket, 1, typeof(CSICSMenuListPacket));
            RegisterPacket(CSOffsets.CSICSGoodsListPacket, 1, typeof(CSICSGoodsListPacket));
            RegisterPacket(CSOffsets.CSICSBuyGoodPacket, 1, typeof(CSICSBuyGoodPacket));
            //RegisterPacket(CSOffsets.CSCharacterFindByNamePacket, 1, typeof(CSCharacterFindByNamePacket));
            RegisterPacket(CSOffsets.CSSpawnCharacterPacket, 1, typeof(CSSpawnCharacterPacket));
            RegisterPacket(CSOffsets.CSCreateCharacterPacket, 1, typeof(CSCreateCharacterPacket));
            RegisterPacket(CSOffsets.CSEditCharacterPacket, 1, typeof(CSEditCharacterPacket));
            RegisterPacket(CSOffsets.CSTeleportEndedPacket, 1, typeof(CSTeleportEndedPacket));
            RegisterPacket(CSOffsets.CSNotifySubZonePacket, 1, typeof(CSNotifySubZonePacket));
            RegisterPacket(CSOffsets.CSSaveTutorialPacket, 1, typeof(CSSaveTutorialPacket));
            RegisterPacket(CSOffsets.CSRequestUIDataPacket, 1, typeof(CSRequestUIDataPacket));
            RegisterPacket(CSOffsets.CSSaveUIDataPacket, 1, typeof(CSSaveUIDataPacket));
            RegisterPacket(CSOffsets.CSUpdateDominionTaxRatePacket, 1, typeof(CSUpdateDominionTaxRatePacket));
            RegisterPacket(CSOffsets.CSRequestCharBriefPacket, 1, typeof(CSRequestCharBriefPacket));
            //RegisterPacket(CSOffsets.CSReloadFactionRelationsPacket, 1, typeof(CSReloadFactionRelationsPacket));
            RegisterPacket(CSOffsets.CSCreateExpeditionPacket, 1, typeof(CSCreateExpeditionPacket));
            RegisterPacket(CSOffsets.CSChangeExpeditionSponsorPacket, 1, typeof(CSChangeExpeditionSponsorPacket));
            RegisterPacket(CSOffsets.CSChangeExpeditionRolePolicyPacket, 1, typeof(CSChangeExpeditionRolePolicyPacket));
            RegisterPacket(CSOffsets.CSChangeExpeditionMemberRolePacket, 1, typeof(CSChangeExpeditionMemberRolePacket));
            RegisterPacket(CSOffsets.CSChangeExpeditionOwnerPacket, 1, typeof(CSChangeExpeditionOwnerPacket));
            RegisterPacket(CSOffsets.CSDismissExpeditionPacket, 1, typeof(CSDismissExpeditionPacket));
            RegisterPacket(CSOffsets.CSInviteToExpeditionPacket, 1, typeof(CSInviteToExpeditionPacket));
            RegisterPacket(CSOffsets.CSLeaveExpeditionPacket, 1, typeof(CSLeaveExpeditionPacket));
            RegisterPacket(CSOffsets.CSKickFromExpeditionPacket, 1, typeof(CSKickFromExpeditionPacket));
            RegisterPacket(CSOffsets.CSReplyExpeditionInvitationPacket, 1, typeof(CSReplyExpeditionInvitationPacket));
            RegisterPacket(CSOffsets.CSFamilyInviteMemberPacket, 1, typeof(CSFamilyInviteMemberPacket));
            RegisterPacket(CSOffsets.CSFamilyLeavePacket, 1, typeof(CSFamilyLeavePacket));
            RegisterPacket(CSOffsets.CSFamilyKickPacket, 1, typeof(CSFamilyKickPacket));
            RegisterPacket(CSOffsets.CSFamilyChangeTitlePacket, 1, typeof(CSFamilyChangeTitlePacket));
            RegisterPacket(CSOffsets.CSFamilyChangeOwnerPacket, 1, typeof(CSFamilyChangeOwnerPacket));
            RegisterPacket(CSOffsets.CSFamilyReplyInvitationPacket, 1, typeof(CSFamilyReplyInvitationPacket));
            RegisterPacket(CSOffsets.CSSearchListPacket, 1, typeof(CSSearchListPacket));
            RegisterPacket(CSOffsets.CSAddFriendPacket, 1, typeof(CSAddFriendPacket));
            RegisterPacket(CSOffsets.CSDeleteFriendPacket, 1, typeof(CSDeleteFriendPacket));
            RegisterPacket(CSOffsets.CSCharDetailPacket, 1, typeof(CSCharDetailPacket));
            RegisterPacket(CSOffsets.CSAddBlockedUserPacket, 1, typeof(CSAddBlockedUserPacket));
            RegisterPacket(CSOffsets.CSDeleteBlockedUserPacket, 1, typeof(CSDeleteBlockedUserPacket));
            RegisterPacket(CSOffsets.CSInviteAreaToTeamPacket, 1, typeof(CSInviteAreaToTeamPacket));
            RegisterPacket(CSOffsets.CSInviteToTeamPacket, 1, typeof(CSInviteToTeamPacket));
            RegisterPacket(CSOffsets.CSReplyToJoinTeamPacket, 1, typeof(CSReplyToJoinTeamPacket));
            RegisterPacket(CSOffsets.CSLeaveTeamPacket, 1, typeof(CSLeaveTeamPacket));
            RegisterPacket(CSOffsets.CSKickTeamMemberPacket, 1, typeof(CSKickTeamMemberPacket));
            RegisterPacket(CSOffsets.CSMakeTeamOwnerPacket, 1, typeof(CSMakeTeamOwnerPacket));
            RegisterPacket(CSOffsets.CSSetTeamOfficerPacket, 1, typeof(CSSetTeamOfficerPacket));
            RegisterPacket(CSOffsets.CSConvertToRaidTeamPacket, 1, typeof(CSConvertToRaidTeamPacket));
            RegisterPacket(CSOffsets.CSMoveTeamMemberPacket, 1, typeof(CSMoveTeamMemberPacket));
            RegisterPacket(CSOffsets.CSDismissTeamPacket, 1, typeof(CSDismissTeamPacket));
            RegisterPacket(CSOffsets.CSSetTeamMemberRolePacket, 1, typeof(CSSetTeamMemberRolePacket));
            RegisterPacket(CSOffsets.CSSetOverHeadMarkerPacket, 1, typeof(CSSetOverHeadMarkerPacket));
            RegisterPacket(CSOffsets.CSChangeLootingRulePacket, 1, typeof(CSChangeLootingRulePacket));
            RegisterPacket(CSOffsets.CSUpdateActionSlotPacket, 1, typeof(CSUpdateActionSlotPacket));
            RegisterPacket(CSOffsets.CSUsePortalPacket, 1, typeof(CSUsePortalPacket));
            RegisterPacket(CSOffsets.CSUpgradeExpertLimitPacket, 1, typeof(CSUpgradeExpertLimitPacket));
            RegisterPacket(CSOffsets.CSDowngradeExpertLimitPacket, 1, typeof(CSDowngradeExpertLimitPacket));
            RegisterPacket(CSOffsets.CSConstructHouseTaxPacket, 1, typeof(CSConstructHouseTaxPacket));
            RegisterPacket(CSOffsets.CSChangeHouseNamePacket, 1, typeof(CSChangeHouseNamePacket));
            RegisterPacket(CSOffsets.CSChangeHousePermissionPacket, 1, typeof(CSChangeHousePermissionPacket));
            RegisterPacket(CSOffsets.CSDecorateHousePacket, 1, typeof(CSDecorateHousePacket));
            RegisterPacket(CSOffsets.CSCreateHousePacket, 1, typeof(CSCreateHousePacket));
            RegisterPacket(CSOffsets.CSChangeHousePayPacket, 1, typeof(CSChangeHousePayPacket));
            //RegisterPacket(CSOffsets.CSHireEmployeePacket, 1, typeof(CSHireEmployeePacket));
            //RegisterPacket(CSOffsets.CSFireEmployeePacket, 1, typeof(CSFireEmployeePacket));
            RegisterPacket(CSOffsets.CSRemoveMatePacket, 1, typeof(CSRemoveMatePacket));
            RegisterPacket(CSOffsets.CSChangeMateTargetPacket, 1, typeof(CSChangeMateTargetPacket));
            RegisterPacket(CSOffsets.CSChangeMateUserStatePacket, 1, typeof(CSChangeMateUserStatePacket));
            RegisterPacket(CSOffsets.CSRequestNpcSpawnerListPacket, 1, typeof(CSRequestNpcSpawnerListPacket));
            RegisterPacket(CSOffsets.CSRemoveNpcSpawnerPacket, 1, typeof(CSRemoveNpcSpawnerPacket));
            //RegisterPacket(CSOffsets.CSUpdateNpcSpawnerPacket, 1, typeof(CSUpdateNpcSpawnerPacket));
            RegisterPacket(CSOffsets.CSSpawnSlavePacket, 1, typeof(CSSpawnSlavePacket));
            RegisterPacket(CSOffsets.CSDespawnSlavePacket, 1, typeof(CSDespawnSlavePacket));
            RegisterPacket(CSOffsets.CSDestroySlavePacket, 1, typeof(CSDestroySlavePacket));
            RegisterPacket(CSOffsets.CSBindSlavePacket, 1, typeof(CSBindSlavePacket));
            RegisterPacket(CSOffsets.CSDiscardSlavePacket, 1, typeof(CSDiscardSlavePacket));
            RegisterPacket(CSOffsets.CSChangeSlaveTargetPacket, 1, typeof(CSChangeSlaveTargetPacket));
            //RegisterPacket(CSOffsets.CSRemoveAllFieldSlavesPacket, 1, typeof(CSRemoveAllFieldSlavesPacket));
            //RegisterPacket(CSOffsets.CSAddFieldSlavePacket, 1, typeof(CSAddFieldSlavePacket));
            RegisterPacket(CSOffsets.CSBoardingTransferPacket, 1, typeof(CSBoardingTransferPacket));
            RegisterPacket(CSOffsets.CSTurretStatePacket, 1, typeof(CSTurretStatePacket));
            RegisterPacket(CSOffsets.CSCreateSkillControllerPacket, 1, typeof(CSCreateSkillControllerPacket));
            RegisterPacket(CSOffsets.CSActiveWeaponChangedPacket, 1, typeof(CSActiveWeaponChangedPacket));
            RegisterPacket(CSOffsets.CSJoinTrialAudiencePacket, 1, typeof(CSJoinTrialAudiencePacket));
            RegisterPacket(CSOffsets.CSLeaveTrialAudiencePacket, 1, typeof(CSLeaveTrialAudiencePacket));
            RegisterPacket(CSOffsets.CSUnMountMatePacket, 1, typeof(CSUnMountMatePacket));
            RegisterPacket(CSOffsets.CSUnbondDoodadPacket, 1, typeof(CSUnbondDoodadPacket));
            RegisterPacket(CSOffsets.CSInstanceLoadedPacket, 1, typeof(CSInstanceLoadedPacket));
            RegisterPacket(CSOffsets.CSApplyToInstantGamePacket, 1, typeof(CSApplyToInstantGamePacket));
            RegisterPacket(CSOffsets.CSCancelInstantGamePacket, 1, typeof(CSCancelInstantGamePacket));
            RegisterPacket(CSOffsets.CSJoinInstantGamePacket, 1, typeof(CSJoinInstantGamePacket));
            RegisterPacket(CSOffsets.CSEnteredInstantGameWorldPacket, 1, typeof(CSEnteredInstantGameWorldPacket));
            //RegisterPacket(CSOffsets.CSRequestPermissionToPlayCinemaForDirectingModePacket, 1, typeof(CSRequestPermissionToPlayCinemaForDirectingModePacket));
            RegisterPacket(CSOffsets.CSStartQuestContextPacket, 1, typeof(CSStartQuestContextPacket));
            RegisterPacket(CSOffsets.CSCompleteQuestContextPacket, 1, typeof(CSCompleteQuestContextPacket));
            RegisterPacket(CSOffsets.CSDropQuestContextPacket, 1, typeof(CSDropQuestContextPacket));
            RegisterPacket(CSOffsets.CSResetQuestContextPacket, 1, typeof(CSResetQuestContextPacket));
            RegisterPacket(CSOffsets.CSAcceptCheatQuestContextPacket, 1, typeof(CSAcceptCheatQuestContextPacket));
            RegisterPacket(CSOffsets.CSQuestTalkMadePacket, 1, typeof(CSQuestTalkMadePacket));
            RegisterPacket(CSOffsets.CSQuestStartWithPacket, 1, typeof(CSQuestStartWithPacket));
            RegisterPacket(CSOffsets.CSTryQuestCompleteAsLetItDonePacket, 1, typeof(CSTryQuestCompleteAsLetItDonePacket));
            RegisterPacket(CSOffsets.CSRestartMainQuestPacket, 1, typeof(CSRestartMainQuestPacket));
            //RegisterPacket(CSOffsets.CSRemoveAreaSpheresPacket, 1, typeof(CSRemoveAreaSpheresPacket));
            //RegisterPacket(CSOffsets.CSPlaceAreaSpheresPacket, 1, typeof(CSPlaceAreaSpheresPacket));
            RegisterPacket(CSOffsets.CSLearnSkillPacket, 1, typeof(CSLearnSkillPacket));
            RegisterPacket(CSOffsets.CSLearnBuffPacket, 1, typeof(CSLearnBuffPacket));
            RegisterPacket(CSOffsets.CSResetSkillsPacket, 1, typeof(CSResetSkillsPacket));
            RegisterPacket(CSOffsets.CSSwapAbilityPacket, 1, typeof(CSSwapAbilityPacket));
            RegisterPacket(CSOffsets.CSRemoveBuffPacket, 1, typeof(CSRemoveBuffPacket));
            RegisterPacket(CSOffsets.CSStopCastingPacket, 1, typeof(CSStopCastingPacket));
            RegisterPacket(CSOffsets.CSDeletePortalPacket, 1, typeof(CSDeletePortalPacket));
            RegisterPacket(CSOffsets.CSSetForceAttackPacket, 1, typeof(CSSetForceAttackPacket));
            RegisterPacket(CSOffsets.CSStartSkillPacket, 1, typeof(CSStartSkillPacket));
            RegisterPacket(CSOffsets.CSCreateDoodadPacket, 1, typeof(CSCreateDoodadPacket));
            RegisterPacket(CSOffsets.CSNaviTeleportPacket, 1, typeof(CSNaviTeleportPacket));
            RegisterPacket(CSOffsets.CSNaviOpenPortalPacket, 1, typeof(CSNaviOpenPortalPacket));
            RegisterPacket(CSOffsets.CSNaviOpenBountyPacket, 1, typeof(CSNaviOpenBountyPacket));
            RegisterPacket(CSOffsets.CSSetLogicDoodadPacket, 1, typeof(CSSetLogicDoodadPacket));
            RegisterPacket(CSOffsets.CSCleanupLogicLinkPacket, 1, typeof(CSCleanupLogicLinkPacket));
            RegisterPacket(CSOffsets.CSSelectInteractionExPacket, 1, typeof(CSSelectInteractionExPacket));
            RegisterPacket(CSOffsets.CSBuyItemsPacket, 1, typeof(CSBuyItemsPacket));
            RegisterPacket(CSOffsets.CSBuyCoinItemPacket, 1, typeof(CSBuyCoinItemPacket));
            RegisterPacket(CSOffsets.CSChangeDoodadPhasePacket, 1, typeof(CSChangeDoodadPhasePacket));
            RegisterPacket(CSOffsets.CSHangPacket, 1, typeof(CSHangPacket));
            RegisterPacket(CSOffsets.CSInteractNPCPacket, 1, typeof(CSInteractNPCPacket));
            RegisterPacket(CSOffsets.CSInteractNPCEndPacket, 1, typeof(CSInteractNPCEndPacket));
            RegisterPacket(CSOffsets.CSStartInteractionPacket, 1, typeof(CSStartInteractionPacket));
            //RegisterPacket(CSOffsets.CSFactionImmigratePacket, 1, typeof(CSFactionImmigratePacket));
            RegisterPacket(CSOffsets.CSRequestHouseTaxPacket, 1, typeof(CSRequestHouseTaxPacket));
            RegisterPacket(CSOffsets.CSSpecialtyRatioPacket, 1, typeof(CSSpecialtyRatioPacket));
            RegisterPacket(CSOffsets.CSListSpecialtyGoodsPacket, 1, typeof(CSListSpecialtyGoodsPacket));
            RegisterPacket(CSOffsets.CSJoinUserChatChannelPacket, 1, typeof(CSJoinUserChatChannelPacket));
            RegisterPacket(CSOffsets.CSLeaveChatChannelPacket, 1, typeof(CSLeaveChatChannelPacket));
            RegisterPacket(CSOffsets.CSSendChatMessagePacket, 1, typeof(CSSendChatMessagePacket));
            RegisterPacket(CSOffsets.CSRollDicePacket, 1, typeof(CSRollDicePacket));
            RegisterPacket(CSOffsets.CSSendMailPacket, 1, typeof(CSSendMailPacket));
            RegisterPacket(CSOffsets.CSListMailPacket, 1, typeof(CSListMailPacket));
            RegisterPacket(CSOffsets.CSListMailContinuePacket, 1, typeof(CSListMailContinuePacket));
            RegisterPacket(CSOffsets.CSReadMailPacket, 1, typeof(CSReadMailPacket));
            RegisterPacket(CSOffsets.CSTakeAttachmentMoneyPacket, 1, typeof(CSTakeAttachmentMoneyPacket));
            RegisterPacket(CSOffsets.CSPayChargeMoneyPacket, 1, typeof(CSPayChargeMoneyPacket));
            RegisterPacket(CSOffsets.CSDeleteMailPacket, 1, typeof(CSDeleteMailPacket));
            RegisterPacket(CSOffsets.CSReportSpamPacket, 1, typeof(CSReportSpamPacket));
            RegisterPacket(CSOffsets.CSReturnMailPacket, 1, typeof(CSReturnMailPacket));
            RegisterPacket(CSOffsets.CSTakeAttachmentItemPacket, 1, typeof(CSTakeAttachmentItemPacket));
            RegisterPacket(CSOffsets.CSRepairSlaveItemsPacket, 1, typeof(CSRepairSlaveItemsPacket));
            RegisterPacket(CSOffsets.CSRepairPetItemsPacket, 1, typeof(CSRepairPetItemsPacket));
            RegisterPacket(CSOffsets.CSSaveDoodadUccStringPacket, 1, typeof(CSSaveDoodadUccStringPacket));
            RegisterPacket(CSOffsets.CSAllowHousingRecoverPacket, 1, typeof(CSAllowHousingRecoverPacket));
            RegisterPacket(CSOffsets.CSBuyPriestBuffPacket, 1, typeof(CSBuyPriestBuffPacket));
            RegisterPacket(CSOffsets.CSChangeSlaveNamePacket, 1, typeof(CSChangeSlaveNamePacket));
            RegisterPacket(CSOffsets.CSUseTeleportPacket, 1, typeof(CSUseTeleportPacket));
            RegisterPacket(CSOffsets.CSLeaveWorldPacket, 1, typeof(CSLeaveWorldPacket));
            RegisterPacket(CSOffsets.CSCancelLeaveWorldPacket, 1, typeof(CSCancelLeaveWorldPacket));
            RegisterPacket(CSOffsets.CSAuctionPostPacket, 1, typeof(CSAuctionPostPacket));
            RegisterPacket(CSOffsets.CSAuctionSearchPacket, 1, typeof(CSAuctionSearchPacket));
            RegisterPacket(CSOffsets.CSAuctionMyBidListPacket, 1, typeof(CSAuctionMyBidListPacket));
            RegisterPacket(CSOffsets.CSCancelAuctionPacket, 1, typeof(CSCancelAuctionPacket));
            RegisterPacket(CSOffsets.CSBidAuctionPacket, 1, typeof(CSBidAuctionPacket));
            //RegisterPacket(CSOffsets.CSExecuteCraftPacket, 1, typeof(CSExecuteCraftPacket));
            RegisterPacket(CSOffsets.CSSetLpManageCharacterPacket, 1, typeof(CSSetLpManageCharacterPacket));
            RegisterPacket(CSOffsets.CSCreateShipyardPacket, 1, typeof(CSCreateShipyardPacket));
            RegisterPacket(CSOffsets.CSSetCraftingPayPacket, 1, typeof(CSSetCraftingPayPacket));
            RegisterPacket(CSOffsets.CSDestroyItemPacket, 1, typeof(CSDestroyItemPacket));
            RegisterPacket(CSOffsets.CSSplitBagItemPacket, 1, typeof(CSSplitBagItemPacket));
            RegisterPacket(CSOffsets.CSSwapItemsPacket, 1, typeof(CSSwapItemsPacket));
            RegisterPacket(CSOffsets.CSSplitCofferItemPacket, 1, typeof(CSSplitCofferItemPacket));
            RegisterPacket(CSOffsets.CSSwapCofferItemsPacket, 1, typeof(CSSwapCofferItemsPacket));
            RegisterPacket(CSOffsets.CSExpandSlotsPacket, 1, typeof(CSExpandSlotsPacket));
            RegisterPacket(CSOffsets.CSDepositMoneyPacket, 1, typeof(CSDepositMoneyPacket));
            RegisterPacket(CSOffsets.CSWithdrawMoneyPacket, 1, typeof(CSWithdrawMoneyPacket));
            RegisterPacket(CSOffsets.CSRepairSingleEquipmentPacket, 1, typeof(CSRepairSingleEquipmentPacket));
            RegisterPacket(CSOffsets.CSRepairAllEquipmentsPacket, 1, typeof(CSRepairAllEquipmentsPacket));
            RegisterPacket(CSOffsets.CSChangeItemLookPacket, 1, typeof(CSChangeItemLookPacket));
            RegisterPacket(CSOffsets.CSChangeMateEquipmentPacket, 1, typeof(CSChangeMateEquipmentPacket));
            RegisterPacket(CSOffsets.CSItemUccPacket, 1, typeof(CSItemUccPacket));
            RegisterPacket(CSOffsets.CSLootOpenBagPacket, 1, typeof(CSLootOpenBagPacket));
            RegisterPacket(CSOffsets.CSLootItemPacket, 1, typeof(CSLootItemPacket));
            RegisterPacket(CSOffsets.CSLootCloseBagPacket, 1, typeof(CSLootCloseBagPacket));
            RegisterPacket(CSOffsets.CSLootDicePacket, 1, typeof(CSLootDicePacket));
            RegisterPacket(CSOffsets.CSSellBackpackGoodsPacket, 1, typeof(CSSellBackpackGoodsPacket));
            RegisterPacket(CSOffsets.CSBuySpecialtyItemPacket, 1, typeof(CSBuySpecialtyItemPacket));
            RegisterPacket(CSOffsets.CSSellItemsPacket, 1, typeof(CSSellItemsPacket));
            RegisterPacket(CSOffsets.CSListSoldItemPacket, 1, typeof(CSListSoldItemPacket));
            RegisterPacket(CSOffsets.CSStartTradePacket, 1, typeof(CSStartTradePacket));
            RegisterPacket(CSOffsets.CSCanStartTradePacket, 1, typeof(CSCanStartTradePacket));
            RegisterPacket(CSOffsets.CSCannotStartTradePacket, 1, typeof(CSCannotStartTradePacket));
            RegisterPacket(CSOffsets.CSCancelTradePacket, 1, typeof(CSCancelTradePacket));
            RegisterPacket(CSOffsets.CSPutupTradeItemPacket, 1, typeof(CSPutupTradeItemPacket));
            RegisterPacket(CSOffsets.CSTakedownTradeItemPacket, 1, typeof(CSTakedownTradeItemPacket));
            RegisterPacket(CSOffsets.CSTradeLockPacket, 1, typeof(CSTradeLockPacket));
            RegisterPacket(CSOffsets.CSTradeOkPacket, 1, typeof(CSTradeOkPacket));
            RegisterPacket(CSOffsets.CSPutupTradeMoneyPacket, 1, typeof(CSPutupTradeMoneyPacket));

            // Proxy
            RegisterPacket(0x000, 2, typeof(ChangeStatePacket));
            RegisterPacket(0x001, 2, typeof(FinishStatePacket));
            RegisterPacket(0x002, 2, typeof(FlushMsgsPacket));
            RegisterPacket(0x004, 2, typeof(UpdatePhysicsTimePacket));
            RegisterPacket(0x005, 2, typeof(BeginUpdateObjPacket));
            RegisterPacket(0x006, 2, typeof(EndUpdateObjPacket));
            RegisterPacket(0x007, 2, typeof(BeginBindObjPacket));
            RegisterPacket(0x008, 2, typeof(EndBindObjPacket));
            RegisterPacket(0x009, 2, typeof(UnbindPredictedObjPacket));
            RegisterPacket(0x00A, 2, typeof(RemoveStaticObjPacket));
            RegisterPacket(0x00B, 2, typeof(VoiceDataPacket));
            RegisterPacket(0x00C, 2, typeof(UpdateAspectPacket));
            RegisterPacket(0x00D, 2, typeof(SetAspectProfilePacket));
            RegisterPacket(0x00E, 2, typeof(PartialAspectPacket));
            RegisterPacket(0x00F, 2, typeof(SetGameTypePacket));
            RegisterPacket(0x010, 2, typeof(ChangeCVarPacket));
            RegisterPacket(0x011, 2, typeof(EntityClassRegistrationPacket));
            RegisterPacket(0x012, 2, typeof(PingPacket));
            RegisterPacket(0x013, 2, typeof(PongPacket));
            RegisterPacket(0x014, 2, typeof(PacketSeqChange));
            RegisterPacket(0x015, 2, typeof(FastPingPacket));
            RegisterPacket(0x016, 2, typeof(FastPongPacket));
        }

        public void Start()
        {
            var config = AppConfiguration.Instance.Network;
            _server = new Server(new IPEndPoint(config.Host.Equals("*") ? IPAddress.Any : IPAddress.Parse(config.Host), config.Port), 10);
            _server.SetHandler(_handler);
            _server.Start();

            _log.Info("Network started");
        }

        public void Stop()
        {
            if (_server.IsStarted)
                _server.Stop();

            _log.Info("Network stoped");
        }

        private void RegisterPacket(uint type, byte level, Type classType)
        {
            _handler.RegisterPacket(type, level, classType);
        }
    }
}
