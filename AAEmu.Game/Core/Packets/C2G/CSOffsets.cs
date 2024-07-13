﻿namespace AAEmu.Game.Core.Packets.C2G;

public static class CSOffsets
{
    // All opcodes here are updated for version 3.0.4.2 r336598 AAClassic
    public const ushort X2EnterWorldPacket = 0x000;
    // double _01_&_05_
    public const ushort CSResturnAddrsPacket = 0x170;     // level = 1+
    public const ushort CSResturnAddrs_05_Packet = 0x170; // level = 5+
    public const ushort CSHgResponsePacket = 0x0AF;       // level = 1+
    public const ushort CSHgResponse_05_Packet = 0x0AF;   // level = 5+
    public const ushort CSAesXorKeyPacket = 0x03F;        // level = 1+
    public const ushort CSAesXorKey_05_Packet = 0x03F;    // level = 5+

    public const ushort CSGameGuardPacket = 0xfff;    // level = 5

    // остальные пакеты
    //public const ushort CSEnterWorldPacket = 0x000;
    public const ushort CSTodayAssignmentPacket = 0x031;
    public const ushort CSRequestSkipClientDrivenIndunPacket = 0x072;
    public const ushort CSRemoveClientNpcPacket = 0x0CD;
    public const ushort CSMoveUnitPacket = 0x094;
    public const ushort CSCofferInteractionPacket = 0x0A3;
    public const ushort CSRequestCommonFarmListPacket = 0x191;
    public const ushort CSChallengeDuelPacket = 0x148;
    public const ushort CSStartDuelPacket = 0x186;
    //public const ushort CSResturnAddrsPacket = 0x170;
    public const ushort off_39BFE09C = 0xfff;
    public const ushort CSHeroRankingListPacket = 0x02F;
    public const ushort CSHeroCandidateListPacket = 0x04A;
    public const ushort CSHeroAbstainPacket = 0x010;
    public const ushort CSHeroVotingPacket = 0x079;
    public const ushort CSConvertItemLookPacket = 0x146;
    public const ushort CSConvertItemLook2Packet = 0x0B1;
    public const ushort CSSetPingPosPacket = 0x086;
    public const ushort CSUpdateExploredRegionPacket = 0x0CF;
    public const ushort CSICSMoneyRequestPacket = 0x05B;
    public const ushort CSPremiumServiceBuyPacket = 0x156;
    public const ushort CSSetVisiblePremiumServicePacket = 0x028;
    public const ushort CSAddReputationPacket = 0x098;
    public const ushort off_39BFF26C = 0x003;
    public const ushort CSGetResidentDescPacket = 0x03B;
    public const ushort CSRefreshResidentMembersPacket = 0x16D;
    public const ushort CSGetResidentZoneListPacket = 0x178;
    public const ushort CSResidentFireNuonsArrowPacket = 0x00A;
    public const ushort CSUseBlessUthstinInitStatsPacket = 0x05A;
    public const ushort CSUseBlessUthstinExtendMaxStatsPacket = 0x185;
    public const ushort CSBlessUthstinUseApplyStatsItemPacket = 0x14C;
    public const ushort CSBlessUthstinApplyStatsPacket = 0x021;
    public const ushort CSEventCenterAddAttendancePacket = 0x042;
    public const ushort CSRequestGameEventInfoPacket = 0x039;
    public const ushort off_39C00C60 = 0x0F1;
    public const ushort off_39C00C6C = 0x0DB;
    public const ushort off_39C02F58 = 0x0FC;
    public const ushort CSSendNationMemberCountListPacket = 0x058;
    public const ushort CSNationSendExpeditionImmigrationAcceptRejectPacket = 0x0F6;
    public const ushort CSSendExpeditionImmigrationListPacket = 0x09D;
    public const ushort CSSendRelationFriendPacket = 0x15A;
    public const ushort CSSendRelationVotePacket = 0x016;
    public const ushort CSSendNationInfoSetPacket = 0x087;
    public const ushort CSSendNationInfoGetPacket = 0x06C;
    public const ushort CSRankCharacterPacket = 0x036;
    public const ushort CSRankSnapshotPacket = 0x018;
    public const ushort CSHeroRequestRankDataPacket = 0x1A5;
    public const ushort CSGetRankerInformationPacket = 0x0C9;
    public const ushort CSRequestRankerAppearancePacket = 0x0FA;
    public const ushort CSRequestSecondPassKeyTablesPacket = 0x164;
    public const ushort CSCreateSecondPassPacket = 0x11B;
    public const ushort CSChangeSecondPassPacket = 0x12B;
    public const ushort CSClearSecondPassPacket = 0x109;
    public const ushort CSCheckSecondPassPacket = 0x09E;
    public const ushort CSReplyImprisonOrTrialPacket = 0x107;
    public const ushort CSSkipFinalStatementPacket = 0x13D;
    public const ushort CSReplyInviteJuryPacket = 0x0DC;
    public const ushort CSJurySummonedPacket = 0x0B2;
    public const ushort CSJuryEndTestimonyPacket = 0x193;
    public const ushort CSCancelTrialPacket = 0x05C;
    public const ushort CSJurySentencePacket = 0x09B;
    public const ushort CSReportCrimePacket = 0x0D4;
    public const ushort CSRequestJuryWaitingNumberPacket = 0x175;
    public const ushort CSRequestSetBountyPacket = 0x05F;
    public const ushort CSUpdateBountyPacket = 0x041;
    public const ushort CSTrialReportBadUserPacket = 0x0BA;
    public const ushort CSTrialRequestBadUserListPacket = 0x06E;
    public const ushort off_39C05504 = 0x0E6;
    public const ushort CSSendUserMusicPacket = 0x067;
    public const ushort CSSaveUserMusicNotesPacket = 0x0BD;
    public const ushort CSRequestMusicNotesPacket = 0x155;
    public const ushort CSPauseUserMusicPacket = 0x07A;
    public const ushort CSAppliedToInstantGamePacket = 0x114;
    public const ushort CSBagHandleSelectiveItemsPacket = 0x068;
    public const ushort CSSkillControllerStatePacket = 0x161;
    public const ushort CSMountMatePacket = 0x0E0;
    public const ushort CSLeaveWorldPacket = 0x1A8;
    public const ushort CSCancelLeaveWorldPacket = 0x101;
    public const ushort CSIdleStatusPacket = 0x180;
    public const ushort CSChangeClientNpcTargetPacket = 0x149;
    public const ushort CSCompletedCinemaPacket = 0x048;
    public const ushort CSCheckDemoModePacket = 0x088;
    public const ushort CSDemoCharResetPacket = 0x1AE;
    public const ushort CSConsoleCmdUsedPacket = 0x035;
    public const ushort CSEditorGameModePacket = 0x0DF;
    public const ushort CSInteractGimmickPacket = 0x047;
    public const ushort CSWorldRaycastingPacket = 0x138;
    public const ushort CSOpenExpeditionImmigrationRequestPacket = 0x04F;
    public const ushort CSNationGetNationNamePacket = 0x043;
    public const ushort CSRefreshInCharacterListPacket = 0x030;
    public const ushort CSDeleteCharacterPacket = 0x0EC;
    public const ushort CSCancelCharacterDeletePacket = 0x076;
    public const ushort CSSelectCharacterPacket = 0x117;
    public const ushort CSCharacterConnectionRestrictPacket = 0x13C;
    public const ushort CSNotifyInGamePacket = 0x017;
    public const ushort CSNotifyInGameCompletedPacket = 0x140;
    public const ushort CSChangeTargetPacket = 0x1A0;
    public const ushort off_39C169A0 = 0x020;
    public const ushort CSGetSiegeAuctionBidCurrencyPacket = 0x1AA;
    public const ushort CSResurrectCharacterPacket = 0x053;
    public const ushort CSCriminalLockedPacket = 0x0B4;
    public const ushort CSExpressEmotionPacket = 0x14B;
    public const ushort CSUnhangPacket = 0x032;
    public const ushort CSChangeAppellationPacket = 0x03E;
    public const ushort CSStartedCinemaPacket = 0x0FB;
    //public const ushort CSHgResponsePacket = 0x0AF;
    public const ushort CSBroadcastVisualOptionPacket = 0x10D;
    public const ushort CSBroadcastOpenEquipInfoPacket = 0x052;
    public const ushort CSRestrictCheckPacket = 0x190;
    public const ushort CSICSMenuListRequestPacket = 0x034;
    public const ushort CSICSGoodsListRequestPacket = 0x012;
    public const ushort CSICSBuyGoodRequestPacket = 0x01A;
    public const ushort CSPremiumServiceMsgPacket = 0x121;
    public const ushort CSProtectSensitiveOperationPacket = 0x0B0;
    public const ushort CSCancelSensitiveOperationVerifyPacket = 0x090;
    public const ushort CSAntibotDataPacket = 0xfff;
    public const ushort CSBuyAaPointPacket = 0x154;
    public const ushort CSRequestTencentFatigueInfoPacket = 0x084;
    public const ushort CSPremiumServiceListPacket = 0x187;
    public const ushort CSRequestSysInstanceIndexPacket = 0x140;
    public const ushort CSQuitResponsePacket = 0x198;
    public const ushort CSSecurityReportPacket = 0x1AD;
    public const ushort CSEnprotectStubCallResponsePacket = 0x18E;
    public const ushort CSRepresentCharacterPacket = 0x15F;
    public const ushort off_39C16AE4 = 0x051;
    public const ushort CSPacketUnknown0x166Packet = 0x0CE; // off_39C16AF0
    public const ushort CSCreateCharacterPacket = 0x11E;
    public const ushort CSEditCharacterPacket = 0x179;
    public const ushort CSSpawnCharacterPacket = 0x0C7;
    public const ushort CSSpawnSlavePacket = 0x116;
    //public const ushort CSRsaencryptAeskeyXorkeyPacket = 0x03F;
    public const ushort CSNotifySubZonePacket = 0x015;
    public const ushort CSSaveTutorialPacket = 0x173;
    public const ushort CSRequestUIDataPacket = 0x009;
    public const ushort CSSaveUIDataPacket = 0x071;
    public const ushort CSBeautyShopDataPacket = 0x01B; // CSBeautyshopDataPacket
    public const ushort CSDominionUpdateTaxRatePacket = 0x18B;
    public const ushort CSDominionUpdateNationalTaxRatePacket = 0x0E3;
    public const ushort CSRequestCharacterBriefPacket = 0x0D7;
    public const ushort CSExpeditionCreatePacket = 0x02C; // CSCreateExpeditionPacket
    public const ushort CSExpeditionChangeRolePolicyPacket = 0x183; // CSChangeExpeditionRolePolicyPacket
    public const ushort CSExpeditionChangeMemberRolePacket = 0x05E; // CSChangeExpeditionMemberRolePacket
    public const ushort CSExpeditionChangeOwnerPacket = 0x106; // CSChangeExpeditionOwnerPacket
    public const ushort CSChangeNationOwnerPacket = 0x06B;
    public const ushort CSExpeditionRenamePacket = 0x176; // CSRenameFactionPacket CSRenameExpeditionPacket
    public const ushort CSExpeditionDismissPacket = 0x168; // CSDismissExpeditionPacket
    public const ushort CSExpeditionInvitePacket = 0x089; // CSInviteToExpeditionPacket
    public const ushort CSExpeditionLeavePacket = 0x027; // CSLeaveExpeditionPacket
    public const ushort CSExpeditionKickPacket = 0x061; // CSKickFromExpeditionPacket
    public const ushort CSExpeditionBeginnerJoinPacket = 0x19E;
    public const ushort CSDeclareExpeditionWarPacket = 0x08F;
    public const ushort CSFactionGetDeclarationMoneyPacket = 0x160;
    public const ushort CSRequestExpeditionWarKillScorePacket = 0x0CB;
    public const ushort CSFactionGetExpeditionWarHistoryPacket = 0x133;
    public const ushort CSFactionCancelProtectionPacket = 0x12F;
    public const ushort CSFactionImmigrationInvitePacket = 0x113;
    public const ushort CSFactionImmigrationInviteReplyPacket = 0x1AC;
    public const ushort CSFactionImmigrateToOriginPacket = 0x1A2;
    public const ushort CSFactionKickToOriginPacket = 0x11C;
    public const ushort CSFactionMobilizationOrderPacket = 0x0B3;
    public const ushort CSFactionCheckExpeditionExpNextDayPacket = 0x0BC;
    public const ushort CSFactionSetExpeditionLevelUpPacket = 0x045;
    public const ushort CSFactionSetExpeditionMotdPacket = 0x123;
    public const ushort CSFactionSetMyExpeditionInterestPacket = 0x18F;
    public const ushort off_39C356A0 = 0x07D;
    public const ushort CSExpeditionReplyInvitationPacket = 0x172;
    public const ushort CSFamilyInviteMemberPacket = 0x0DA;
    public const ushort CSFamilyLeavePacket = 0x0FD;
    public const ushort CSFamilyKickPacket = 0x029;
    public const ushort CSFamilyChangeTitlePacket = 0x197;
    public const ushort CSFamilyChangeOwnerPacket = 0x0F0;
    public const ushort CSFamilySetNamePacket = 0x102;
    public const ushort CSFamilySetContentPacket = 0x11A;
    public const ushort CSFamilyOpenIncreaseMemberPacket = 0x014;
    public const ushort CSFamilyChangeMemberRolePacket = 0x19C;
    public const ushort CSFamilyReplyInvitationPacket = 0x14A;
    public const ushort CSAddFriendPacket = 0x0C0;
    public const ushort CSDeleteFriendPacket = 0x158;
    public const ushort CSAddBlockedUserPacket = 0x1AB;
    public const ushort CSDeleteBlockedUserPacket = 0x182;
    public const ushort CSInviteAreaToTeamPacket = 0x134;
    public const ushort CSInviteToTeamPacket = 0x0EF;
    public const ushort CSReplyToJoinTeamPacket = 0x0BB;
    public const ushort CSLeaveTeamPacket = 0x02A;
    public const ushort CSKickTeamMemberPacket = 0x126;
    public const ushort CSMakeTeamOwnerPacket = 0x128;
    public const ushort CSConvertToRaidTeamPacket = 0x13B;
    public const ushort CSMoveTeamMemberPacket = 0x0A5;
    public const ushort CSDismissTeamPacket = 0x169;
    public const ushort CSSetTeamMemberRolePacket = 0x082;
    public const ushort CSSetOverHeadMarkerPacket = 0x0F9;
    public const ushort CSAskRiskyTeamActionPacket = 0x069;
    public const ushort CSTeamAcceptHandOverOwnerPacket = 0x0C1;
    public const ushort CSTeamAcceptOwnerOfferPacket = 0x03A;
    public const ushort CSChangeLootingRulePacket = 0x02E;
    public const ushort CSRenameCharacterPacket = 0x0AC;
    public const ushort CSUpdateActionSlotPacket = 0x162;
    public const ushort CSUsePortalPacket = 0x171;
    public const ushort CSUpgradeExpertLimitPacket = 0x199;
    public const ushort CSDowngradeExpertLimitPacket = 0x165;
    public const ushort CSExpandExpertPacket = 0x147;
    public const ushort CSEnterSysInstancePacket = 0x0C2;
    public const ushort CSEndPortalInteractionPacket = 0x181;
    public const ushort CSCreateShipyardPacket = 0x0E2;
    public const ushort CSCreateHousePacket = 0x075;
    public const ushort CSLeaveBeautyShopPacket = 0x100;
    public const ushort CSConstructHouseTaxPacket = 0x194;
    public const ushort CSChangeHouseNamePacket = 0x0E8;
    public const ushort CSChangeHousePermissionPacket = 0x0BF;
    public const ushort CSRequestHouseTaxPacket = 0x006;
    public const ushort CSPerpayHouseTaxPacket = 0x0B7;
    public const ushort CSAllowRecoverPacket = 0x0CA;
    public const ushort CSSellHouseCancelPacket = 0x189;
    public const ushort CSDecorateHousePacket = 0x064;
    public const ushort CSSellHousePacket = 0x0C4;
    public const ushort CSBuyHousePacket = 0x167;
    public const ushort CSRotateHousePacket = 0x019;
    public const ushort CSRemoveMatePacket = 0x08A;
    public const ushort CSChangeMateTargetPacket = 0x00F;
    public const ushort CSChangeMateUserStatePacket = 0x0F4;
    public const ushort CSDespawnSlavePacket = 0x144;
    public const ushort CSDestroySlavePacket = 0x0A8;
    public const ushort CSBindSlavePacket = 0x0ED;
    public const ushort off_39C39534 = 0x10C;
    public const ushort CSBoardingTransferPacket = 0x06A;
    public const ushort CSTurretStatePacket = 0x17F;
    public const ushort CSCreateSkillControllerPacket = 0x044;
    public const ushort CSJoinTrialAudiencePacket = 0x0E7;
    public const ushort CSLeaveTrialAudiencePacket = 0x0D5;
    public const ushort CSUnMountMatePacket = 0x0F2;
    public const ushort CSUnbondPacket = 0x0D5;
    public const ushort CSInstanceLoadedPacket = 0x015;
    public const ushort CSApplyToInstantGamePacket = 0x124;
    public const ushort CSCancelInstantGamePacket = 0x060;
    public const ushort CSJoinInstantGamePacket = 0x18C;
    public const ushort CSEnteredInstantGameWorldPacket = 0x0AE;
    public const ushort CSLeaveInstantGamePacket = 0x19F;
    public const ushort CSPickBuffInstantGamePacket = 0x08B;
    public const ushort CSBattlefieldPickshipPacket = 0x157;
    public const ushort CSRequestPermissionToPlayCinemaForDirectingModePacket = 0x1A4;
    public const ushort CSStartQuestContextPacket = 0x083;
    public const ushort CSCompleteQuestContextPacket = 0x0E5;
    public const ushort CSDropQuestContextPacket = 0x145;
    public const ushort CSQuestTalkMadePacket = 0x096;
    public const ushort CSQuestStartWithParamPacket = 0x0E9;
    public const ushort CSTryQuestCompleteAsLetItDonePacket = 0x040;
    public const ushort CSRestartMainQuestPacket = 0x1A7;
    public const ushort CSLearnSkillPacket = 0x118;
    public const ushort CSLearnBuffPacket = 0x0C8;
    public const ushort CSResetSkillsPacket = 0x0E2;
    public const ushort CSSwapAbilityPacket = 0x06D;
    public const ushort CSSelectHighAbilityPacket = 0x17A;
    public const ushort off_39C3B564 = 0x0D3;
    public const ushort CSRemoveBuffPacket = 0x049;
    public const ushort CSStopCastingPacket = 0x01C;
    public const ushort CSDeletePortalPacket = 0x0A9;
    public const ushort CSIndunDirectTelPacket = 0x12E;
    public const ushort CSSetForceAttackPacket = 0x03C;
    public const ushort CSStartSkillPacket = 0x024;
    public const ushort off_39C3BABC = 0x132;
    public const ushort CSStopLootingPacket = 0x0A6;
    public const ushort CSCreateDoodadPacket = 0x0E1;
    public const ushort CSNaviTeleportPacket = 0x15B;
    public const ushort CSNaviOpenPortalPacket = 0x188;
    public const ushort CSNaviOpenBountyPacket = 0x196;
    public const ushort CSSetLogicDoodadPacket = 0x120;
    public const ushort CSCleanupLogicLinkPacket = 0x16A;
    public const ushort CSSelectInteractionExPacket = 0x078;
    public const ushort CSChangeDoodadDataPacket = 0x0D8;
    public const ushort CSBuyItemsPacket = 0x166;
    public const ushort off_39C3D138 = 0x0EA;
    public const ushort off_39C3D144 = 0x097;
    public const ushort off_39C3D150 = 0x1A3;
    public const ushort CSUnitAttachedPacket = 0x141; // off_39C3D160
    public const ushort CSInteractNPCPacket = 0x046;
    public const ushort CSInteractNPCEndPacket = 0x174;
    public const ushort CSStartInteractionPacket = 0x04C;
    public const ushort CSBeautyShopBypassPacket = 0x063;
    public const ushort off_39C3D5C8 = 0x07E;
    public const ushort off_39C3D60C = 0x07C;
    public const ushort CSJoinUserChatChannelPacket = 0x092;
    public const ushort CSLeaveChatChannelPacket = 0x002;
    public const ushort CSSendChatMessagePacket = 0x129;
    public const ushort CSRollDicePacket = 0x05D;
    public const ushort CSSendMailPacket = 0x10A;
    public const ushort CSListMailPacket = 0x03D;
    public const ushort CSListMailContinuePacket = 0x080;
    public const ushort CSReadMailPacket = 0x153;
    public const ushort CSTakeAttachmentMoneyPacket = 0x093;
    public const ushort CSTakeAttachmentSequentiallyPacket = 0x02D;
    public const ushort CSPayChargeMoneyPacket = 0x007;
    public const ushort CSDeleteMailPacket = 0x062;
    public const ushort CSReportSpamPacket = 0x17E;
    public const ushort CSReturnMailPacket = 0x12D;
    public const ushort CSTakeAllAttachmentItemPacket = 0x0F8;
    public const ushort CSTakeAttachmentItemPacket = 0x105;
    public const ushort CSActiveWeaponChangedPacket = 0x0F5;
    public const ushort off_39C40290 = 0x15D;
    public const ushort CSRequestExpandAbilitySetSlotPacket = 0x00C;
    public const ushort CSSaveAbilitySetPacket = 0x08E;
    public const ushort CSDeleteAbilitySetPacket = 0x110;
    public const ushort CSRepairSlaveItemsPacket = 0x0FE;
    public const ushort CSRepairPetItemsPacket = 0x135;
    public const ushort CSFactionIssuanceOfMobilizationOrderPacket = 0x0D1;
    public const ushort CSGetExpeditionMyRecruitmentsPacket = 0x0AB;
    public const ushort CSExpeditionRecruitmentAddPacket = 0x091;
    public const ushort CSExpeditionRecruitmentDeletePacket = 0x192;
    public const ushort CSGetExpeditionApplicantsPacket = 0x095;
    public const ushort CSExpeditionApplicantAddPacket = 0x159;
    public const ushort CSExpeditionApplicantDeletePacket = 0x111;
    public const ushort CSExpeditionApplicantAcceptPacket = 0x055;
    public const ushort CSExpeditionApplicantRejectPacket = 0x125;
    public const ushort CSExpeditionSummonPacket = 0x08D;
    public const ushort CSExpeditionSummonReplyPacket = 0x0DD;
    public const ushort CSInstantTimePacket = 0x0B8;
    public const ushort CSSetHouseAllowRecoverPacket = 0x099; // CSAllowHousingRecoverPacket
    public const ushort CSRefreshBotCheckInfoPacket = 0x0A4;
    public const ushort CSAnswerBotCheckPacket = 0x17B;
    public const ushort CSChangeSlaveNamePacket = 0x0CC;
    public const ushort CSUseTeleportPacket = 0x057;
    public const ushort CSAuctionPostPacket = 0x19A;
    public const ushort CSAuctionSearchPacket = 0x0D6;
    public const ushort CSAuctionMyBidListPacket = 0x137;
    public const ushort CSAuctionLowestPricePacket = 0x0A1;
    public const ushort CSAuctionSearchSoldRecordPacket = 0x10F;
    public const ushort CSAuctionCancelPacket = 0x011;
    public const ushort CSAuctionBidPacket = 0x022;
    public const ushort CSExecuteCraftPacket = 0x07F;
    public const ushort CSSetLpManageCharacterPacket = 0x14E;
    public const ushort CSSetCraftingPayPacket = 0x0C6;
    public const ushort CSDestroyItemPacket = 0x131;
    public const ushort CSSplitBagItemPacket = 0x119;
    public const ushort CSSwapItemsPacket = 0x0DE;
    public const ushort CSSplitCofferItemPacket = 0x18A;
    public const ushort CSSwapCofferItemsPacket = 0x127;
    public const ushort CSExpandSlotsPacket = 0x122;
    public const ushort CSDepositMoneyPacket = 0x081;
    public const ushort CSWithdrawMoneyPacket = 0x18D;
    public const ushort CSItemSecurePacket = 0x0C5;
    public const ushort CSItemUnsecurePacket = 0x09C;
    public const ushort CSEquipmentsSecurePacket = 0x0A2;
    public const ushort CSEquipmentsUnsecurePacket = 0x0A7;
    public const ushort CSRepairSingleEquipmentPacket = 0x0F7;
    public const ushort CSRepairAllEquipmentsPacket = 0x01D;
    public const ushort CSChangeAutoUseAaPointPacket = 0x115;
    public const ushort CSThisTimeUnpackPacket = 0x1AF;
    public const ushort CSTakeScheduleItemPacket = 0x139;
    public const ushort CSChangeMateEquipmentPacket = 0x0F3;
    public const ushort CSChangeSlaveEquipmentPacket = 0x136;
    public const ushort CSLoginUccItemsPacket = 0x04E;
    public const ushort CSLootOpenBagPacket = 0x0B5;
    public const ushort CSLootItemPacket = 0x1A6;
    public const ushort CSLootCloseBagPacket = 0x054;
    public const ushort CSLootDicePacket = 0x0A0;
    public const ushort off_39C544F4 = 0x14F;
    public const ushort CSSellItemsPacket = 0x1A9;
    public const ushort CSListSoldItemPacket = 0x026;
    public const ushort CSRequestSpecialtyCurrentPacket = 0x00B; // CSSpecialtyCurrentLoadPacket
    public const ushort CSStartTradePacket = 0x050;
    public const ushort CSCanStartTradePacket = 0x16E;
    public const ushort CSCannotStartTradePacket = 0x13E;
    public const ushort CSCancelTradePacket = 0x04D;
    public const ushort CSPutupTradeItemPacket = 0x038;
    public const ushort CSTakedownItemPacket = 0x14D;
    public const ushort CSTradeLockPacket = 0x152;
    public const ushort CSTradeOkPacket = 0x059;
    public const ushort CSPutupTradeMoneyPacket = 0x09F;
    public const ushort CSReportSpammerPacket = 0x073;

    // эти опкоды для версии 3042 отсутствуют в x2game.dll
    public const ushort CSAcceptCheatQuestContextPacket = 0xfff;
    public const ushort CSAllowHousingRecoverPacket = 0xfff;
    //public const ushort CSBidAuctionPacket = 0xfff;
    public const ushort CSBuyCoinItemPacket = 0xfff;
    public const ushort CSBuyPriestBuffPacket = 0xfff;
    public const ushort CSBuySpecialtyItemPacket = 0xfff;
    //public const ushort CSCancelAuctionPacket = 0xfff;
    public const ushort CSChangeDoodadPhasePacket = 0xfff;
    public const ushort CSChangeExpeditionMemberRolePacket = 0xfff;
    public const ushort CSChangeExpeditionOwnerPacket = 0xfff;
    public const ushort CSChangeExpeditionRolePolicyPacket = 0xfff;
    public const ushort CSChangeExpeditionSponsorPacket = 0xfff;
    public const ushort CSChangeHousePayPacket = 0xfff;
    public const ushort CSChangeItemLookPacket = 0xfff;
    public const ushort CSChangeSlaveTargetPacket = 0xfff;
    public const ushort CSCharDetailPacket = 0xfff;
    public const ushort CSCreateExpeditionPacket = 0xfff;
    public const ushort CSDiscardSlavePacket = 0xfff;
    public const ushort CSDismissExpeditionPacket = 0xfff;
    public const ushort CSFactionDeclareHostilePacket = 0xfff;
    public const ushort CSHangPacket = 0xfff;
    public const ushort CSInviteToExpeditionPacket = 0xfff;
    public const ushort CSJuryVerdictPacket = 0xfff;
    public const ushort CSKickFromExpeditionPacket = 0xfff;
    public const ushort CSLeaveExpeditionPacket = 0xfff;
    public const ushort CSListCharacterPacket = 0xfff;
    public const ushort CSListSpecialtyGoodsPacket = 0xfff;
    public const ushort CSQuestStartWithPacket = 0xfff;
    public const ushort CSRenameExpeditionPacket = 0xfff;
    public const ushort CSReplyExpeditionInvitationPacket = 0xfff;
    public const ushort CSRequestCharBriefPacket = 0xfff;
    public const ushort CSRequestSecondPasswordKeyTablesPacket = 0xfff;
    public const ushort CSResetQuestContextPacket = 0xfff;
    public const ushort CSSaveDoodadUccStringPacket = 0xfff;
    public const ushort CSSearchListPacket = 0xfff;
    public const ushort CSSetTeamOfficerPacket = 0xfff;
    public const ushort CSSetupSecondPasswordPacket = 0xfff;
    public const ushort CSSpecialtyRecordLoadPacket = 0xfff;
    public const ushort CSTakedownTradeItemPacket = 0xfff;
    public const ushort CSThisTimeUnpackItemPacket = 0xfff;
    public const ushort CSUpdateDominionTaxRatePacket = 0xfff;
    public const ushort CSUpdateNationalTaxRatePacket = 0xfff;
    public const ushort CSSellBackpackGoodsPacket = 0xfff;
    public const ushort CSSpecialtyRatioPacket = 0xfff;
    public const ushort CSTeleportEndedPacket = 0xfff;
    public const ushort CSChangeMateNamePacket = 0xfff;
    public const ushort CSDetachFromDoodadPacket = 0xfff;
}
