﻿namespace AAEmu.Game.Core.Packets.G2C
{
    public static class SCOffsets
    {
        // All opcodes here are updated for version client Archeage 0.5.1.35870 2012.12.04 r126247 XLGames OBT Preload
        public const ushort X2WorldToClientPacket = 0x000;
        // первые пакеты, которые необходимы для работы лобби
        //public const ushort SCHGRequestPacket = 0xfff; //
        public const ushort SCInitialConfigPacket = 0x005; //+
        public const ushort SCAccountInfoPacket = 0x18C; //+
        //public const ushort SCTrionConfigPacket = 0xfff; // нет такого пакета в этой версии
        public const ushort SCChatSpamConfigPacket = 0xfff; //
        //public const ushort SCAccountAttributeConfigPacket = 0xfff //
        //public const ushort SCLevelRestrictionConfigPacket = 0xfff; //
        public const ushort SCTaxItemConfigPacket = 0xfff; //
        public const ushort SCInGameShopConfigPacket = 0xfff; //
        public const ushort SCGameRuleConfigPacket = 0xfff; //
        public const ushort SCTaxItemConfig2Packet = 0xfff; //

        //public const ushort SCGameGuardPacket = 0xfff;    // level = 5

        //public const ushort X2WorldToClientPacket = 0x000;
        public const ushort SCReconnectAuthPacket = 0x001;
        public const ushort SCPrepareLeaveWorldPacket = 0x002;
        public const ushort SCLeaveWorldGrantedPacket = 0x003;
        public const ushort SCLeaveWorldCanceledPacket = 0x004;
        //public const ushort SCInitialConfigPacket = 0x005;
        public const ushort SCFactionListPacket = 0x006;
        public const ushort SCFactionRelationListPacket = 0x007;
        public const ushort SCFactionPendingRelationListPacket = 0x008;
        public const ushort SCExpeditionRolePolicyListPacket = 0x00A;
        public const ushort SCExpeditionRolePolicyChangedPacket = 0x00B;
        public const ushort SCExpeditionRoleChangedPacket = 0x00C;
        public const ushort SCExpeditionOwnerChangedPacket = 0x00D;
        public const ushort SCExpeditionNameChangedPacket = 0x00E;
        public const ushort SCExpeditionSponsorChangedPacket = 0x00F;
        //public const ushort SCExpeditionSponsorChangedPacket = 0x010; // дубль
        public const ushort SCExpeditionDismissedPacket = 0x011;
        public const ushort SCFactionSetFriendshipPacket = 0x015;
        public const ushort SCExpeditionInvitationPacket = 0x016;
        public const ushort SCUnitFactionChangedPacket = 0x017;
        public const ushort SCUnitExpeditionChangedPacket = 0x018;
        public const ushort SCExpeditionMemberListPacket = 0x012;
        public const ushort SCExpeditionMemberPacket = 0x013;
        public const ushort SCExpeditionMemberStatusChangedPacket = 0x014;
        public const ushort SCFamilyInvitationPacket = 0x023;
        public const ushort SCFamilyCreatedPacket = 0x024;
        public const ushort SCFamilyRemovedPacket = 0x025;
        public const ushort SCFamilyDescPacket = 0x026;
        public const ushort SCFamilyMemberAddedPacket = 0x027;
        public const ushort SCFamilyMemberRemovedPacket = 0x028;
        public const ushort SCFamilyOwnerChangedPacket = 0x029;
        public const ushort SCFamilyTitleChangedPacket = 0x02A;
        public const ushort SCFamilyTitlePacket = 0x02B;
        public const ushort SCFamilyMemberOnlinePacket = 0x02C;
        public const ushort SCDeleteCharacterResponsePacket = 0x02E;
        public const ushort SCCharacterDeletedPacket = 0x030;
        public const ushort SCCancelCharacterDeleteResponsePacket = 0x031;
        public const ushort SCCharacterCreationFailedPacket = 0x032;
        public const ushort SCRaceCongestionPacket = 0x034;
        public const ushort SCCharacterInvenInitPacket = 0x03F;
        public const ushort SCCharacterInvenContentsPacket = 0x040;
        public const ushort SCCharacterMatesPacket = 0x036;
        public const ushort SCNotifyResurrectionPacket = 0x037;
        public const ushort SCCharacterResurrectedPacket = 0x038;
        public const ushort SCForceAttackSetPacket = 0x039;
        public const ushort SCCharacterLaborPowerChangedPacket = 0x03A;
        public const ushort SCAddActionPointPacket = 0x03B;
        public const ushort SCLpManagedPacket = 0x03C;
        public const ushort SCExpertLimitModifiedPacket = 0x18B;
        //public const ushort SCAccountInfoPacket = 0x18C;
        public const ushort SCDumpCombatInfoPacket = 0x17D;
        public const ushort SCSlaveCreatedPacket = 0x053;
        public const ushort SCSlaveRemovedPacket = 0x054;
        public const ushort SCSlaveDespawnPacket = 0x055;
        public const ushort SCSlaveBoundPacket = 0x056;
        public const ushort SCUnitsRemovedPacket = 0x059;
        public const ushort SCUnitPortalUsedPacket = 0x05C;
        public const ushort SCSkillControllerStatePacket = 0x05B;
        public const ushort SCActiveWeaponChangedPacket = 0x05D;
        public const ushort SCUnitNameChangedPacket = 0x05E;
        public const ushort SCUnitDeathPacket = 0x05F;
        public const ushort SCTeleportUnitPacket = 0x060;
        public const ushort SCBlinkUnitPacket = 0x061;
        public const ushort SCUnitAttachedPacket = 0x064;
        public const ushort SCUnitDetachedPacket = 0x065;
        public const ushort SCUnitInvisiblePacket = 0x066;
        public const ushort SCTargetChangedPacket = 0x070;
        public const ushort SCItemTaskSuccessPacket = 0x07B;
        public const ushort SCUnitEquipmentIdsPacket = 0x080;
        public const ushort SCSyncItemLifespanPacket = 0x083;
        public const ushort SCSpecialtyRatioPacket = 0x084;
        public const ushort SCSpecialtyGoodsPacket = 0x085;
        public const ushort SCSkillStartedPacket = 0x086;
        public const ushort SCSkillFiredPacket = 0x087;
        public const ushort SCSkillEndedPacket = 0x088;
        public const ushort SCSkillStoppedPacket = 0x089;
        public const ushort SCCastingStoppedPacket = 0x08A;
        public const ushort SCCastingDelayedPacket = 0x08B;
        public const ushort SCUnitDamagedPacket = 0x08C;
        public const ushort SCUnitHealedPacket = 0x08D;
        public const ushort SCBuffStatePacket = 0x098;
        public const ushort SCBuffCreatedPacket = 0x099;
        public const ushort SCBuffRemovedPacket = 0x09A;
        public const ushort SCBuffUpdatedPacket = 0x09C;
        public const ushort SCUnitPointsPacket = 0x09D;
        public const ushort SCCombatEngagedPacket = 0x071;
        public const ushort SCCombatClearedPacket = 0x072;
        public const ushort SCCombatFirstHitPacket = 0x075;
        public const ushort SCDuelChallengedPacket = 0x076;
        public const ushort SCDuelStartCountdownPacket = 0x077;
        public const ushort SCDuelStartedPacket = 0x078;
        public const ushort SCDuelEndedPacket = 0x079;
        public const ushort SCUnitDuelStatePacket = 0x07A;
        public const ushort SCHouseBuildProgressPacket = 0x09F;
        public const ushort SCHousePermissionChangedPacket = 0x0A0;
        public const ushort SCHouseDemolishedPacket = 0x0A2;
        public const ushort SCMyHouseRemovedPacket = 0x0A4;
        public const ushort SCHouseFarmPacket = 0x0A5;
        public const ushort SCJoinedChatChannelPacket = 0x0A7;
        public const ushort SCLeavedChatChannelPacket = 0x0A8;
        public const ushort SCChatMessagePacket = 0x0A9;
        public const ushort SCChatLocalizedMessagePacket = 0x0AC;
        public const ushort SCChatSpamDelayPacket = 0x0AD;
        public const ushort SCNoticeMessagePacket = 0x0AA;
        public const ushort SCChatFailedPacket = 0x0AB;
        public const ushort SCAskToJoinTeamPacket = 0x0AE;
        public const ushort SCAskToJoinTeamAreaPacket = 0x0AF;
        public const ushort SCRejectedTeamPacket = 0x0B1;
        public const ushort SCLeavedTeamPacket = 0x0B2;
        public const ushort SCTeamDismissedPacket = 0x0B3;
        public const ushort SCTeamMemberJoinedPacket = 0x0B4;
        public const ushort SCTeamMemberLeavedPacket = 0x0B5;
        public const ushort SCTeamOwnerChangedPacket = 0x0B7;
        public const ushort SCTeamOfficerChangedPacket = 0x0B8;
        public const ushort SCTeamMemberRoleChangedPacket = 0x0B9;
        public const ushort SCTeamBecameRaidTeamPacket = 0x0BA;
        public const ushort SCTeamMemberMovedPacket = 0x0BB;
        public const ushort SCRefreshTeamMemberPacket = 0x0BE;
        public const ushort SCTeamRemoteMembersExPacket = 0x0BF;
        public const ushort SCTeamAreaInvitedPacket = 0x0C0;
        public const ushort SCOverHeadMarkerSetPacket = 0x0C1;
        public const ushort SCSiegeDeclaredPacket = 0x0C4;
        public const ushort SCSiegeTicketConsumedPacket = 0x0C5;
        public const ushort SCSiegeMemberPacket = 0x0C6;
        public const ushort SCConflictZoneStatePacket = 0x0C7;
        public const ushort SCTimeOfDayPacket = 0x0C8;
        public const ushort SCDetailedTimeOfDayPacket = 0x0C9;
        public const ushort SCNpcInteractionSkillListPacket = 0x067;
        public const ushort SCNpcInteractionEndedByZonePacket = 0x068;
        public const ushort SCWorldInteractionSkillListPacket = 0x06A;
        public const ushort SCWorldInteractionCanceledPacket = 0x06B;
        public const ushort SCNpcInteractionStatusChangedPacket = 0x06C;
        public const ushort SCDuelStatePacket = 0x07A;
        public const ushort SCNpcFriendshipListPacket = 0x06E;
        public const ushort SCNpcFriendshipChangedPacket = 0x06F;
        public const ushort SCQuestsPacket = 0x0CA;
        public const ushort SCCompletedQuestsPacket = 0x0CB;
        public const ushort SCCraftItemUnlockPacket = 0x0CD;
        public const ushort SCItemLookChangedPacket = 0x0CE;
        public const ushort SCLootableStatePacket = 0x0CF;
        public const ushort SCUnitLootingStatePacket = 0x0D0;
        public const ushort SCLootItemTookPacket = 0x0D2;
        public const ushort SCLootItemFailedPacket = 0x0D3;
        public const ushort SCLootDiceSummaryPacket = 0x0D6;
        public const ushort SCExpChangedPacket = 0x0D7;
        public const ushort SCRecoverableExpPacket = 0x0D8;
        public const ushort SCMileageChangedPacket = 0x0D9;
        public const ushort SCLevelChangedPacket = 0x0DA;
        public const ushort SCUnitModelPostureChangedPacket = 0x0DB;
        public const ushort SCUnitModelScalingPacket = 0x0DC;
        public const ushort SCUnitEntitySlotChangedPacket = 0x0DD;
        public const ushort SCSkillLearnedPacket = 0x0DE;
        public const ushort SCSkillUpgradedPacket = 0x0DF;
        public const ushort SCBuffLearnedPacket = 0x0E0;
        public const ushort SCSkillsResetPacket = 0x0E1;
        public const ushort SCAbilitySwappedPacket = 0x0E2;
        public const ushort SCErrorMsgPacket = 0x0E5;
        public const ushort SCDoodadRemovedPacket = 0x0E8;
        public const ushort SCDoodadChangedPacket = 0x0E9;
        public const ushort SCDoodadPhaseChangedPacket = 0x0EA;
        public const ushort SCDoodadPuzzleScenePacket = 0x0EB;
        public const ushort SCDoodadQuestAcceptPacket = 0x0EC;
        public const ushort SCDoodadsRemovedPacket = 0x0EE;
        public const ushort SCDoodadOriginatorPacket = 0x0EF;
        public const ushort SCMineAmountPacket = 0x0FF;
        public const ushort SCMailFailedPacket = 0x0F0;
        public const ushort SCCountUnreadMailPacket = 0x0F1;
        public const ushort SCMailSentPacket = 0x0F2;
        public const ushort SCGotMailPacket = 0x0F3;
        public const ushort SCMailListPacket = 0x0F4;
        public const ushort SCMailListEndPacket = 0x0F5;
        public const ushort SCMailReceiverOpenedPacket = 0x0F7;
        public const ushort SCAttachmentTakenPacket = 0x0F8;
        public const ushort SCMailDeletedPacket = 0x0FA;
        public const ushort SCSpamReportedPacket = 0x0FB;
        public const ushort SCMailReturnedPacket = 0x0FC;
        public const ushort SCMailStatusUpdatedPacket = 0x0FD;
        public const ushort SCMailRemovedPacket = 0x0FE;
        public const ushort SCNewMatePacket = 0x100;
        public const ushort SCMateSpawnedPacket = 0x101;
        public const ushort SCSiegeReinforcePacket = 0xfff;
        public const ushort SCEmotionExpressedPacket = 0x108;
        public const ushort SCActionSlotsPacket = 0x10A;
        public const ushort SCAuctionBidPacket = 0x10D;
        public const ushort SCUnitTrackedPacket = 0x08E;
        public const ushort SCBombUpdatedPacket = 0x08F;
        public const ushort SCCombatTextPacket = 0x090;
        public const ushort SCSkillCooldownResetPacket = 0x092;
        public const ushort SCPlotEndedPacket = 0x094;
        public const ushort SCPlotCastingStoppedPacket = 0x095;
        public const ushort SCPlotChannelingStoppedPacket = 0x096;
        public const ushort SCEnvDamagePacket = 0x097;
        public const ushort SCDiceValuePacket = 0x110;
        public const ushort SCActabilityPacket = 0x112;
        public const ushort SCVegetationCutdowningPacket = 0x06D;
        public const ushort SCUnhungPacket = 0x114;
        public const ushort SCBondDoodadPacket = 0x115;
        public const ushort SCUnbondDoodadPacket = 0x116;
        public const ushort SCPlaySequencePacket = 0x117;
        public const ushort SCGimmicksRemovedPacket = 0x11A;
        public const ushort SCGimmickJointsBrokenPacket = 0x11C;
        public const ushort SCGimmickResetJointsPacket = 0x11D;
        public const ushort SCGimmickGraspedPacket = 0x11E;
        public const ushort SCWorldRayCastingResultPacket = 0x11F;
        public const ushort SCWorldAimPointPacket = 0x120;
        public const ushort SCTalentPointSetPacket = 0x122;
        public const ushort SCQuestContextFailedPacket = 0x123;
        public const ushort SCQuestContextStartedPacket = 0x124;
        public const ushort SCQuestContextUpdatedPacket = 0x125;
        public const ushort SCCSQuestAcceptConditionalPacket = 0x126;
        public const ushort SCQuestContextCompletedPacket = 0x127;
        public const ushort SCQuestContextResetPacket = 0x128;
        public const ushort SCDoodadCompleteQuestPacket = 0x129;
        public const ushort SCQuestRewardedByMailPacket = 0x12A;
        public const ushort SCGmCommandPacket = 0x16C;
        public const ushort SCGmDumpInventoryPacket = 0x16D;
        public const ushort SCGmDumpQuestsPacket = 0x16F;
        public const ushort SCGmDumpCompletedQuestsPacket = 0x170;
        public const ushort SCKickedPacket = 0x171;
        public const ushort SCAccountWarnedPacket = 0x172;
        public const ushort SCAboxTeleportPacket = 0x173;
        public const ushort SCPortalDeletedPacket = 0x050;
        //public const ushort SCUnitPortalUsedPacket = 0x052; // дубль
        public const ushort SCSoundAreaEventPacket = 0x12B;
        public const ushort SCAreaChatBubblePacket = 0x12C;
        public const ushort SCChatBubblePacket = 0x12D;
        public const ushort SCAreaTeamMessagePacket = 0x12E;
        public const ushort SCDoodadSoundPacket = 0x12F;
        public const ushort SCDoodadPhaseMsgPacket = 0x130;
        public const ushort SCCooldownsPacket = 0x03E;
        public const ushort SCDoodadUccDataPacket = 0x133;
        public const ushort SCUccCharacterNameLoadedPacket = 0x134;
        public const ushort SCNaviTeleportPacket = 0x135;
        public const ushort SCTradeStartedPacket = 0x137;
        public const ushort SCTradeCanceledPacket = 0x138;
        public const ushort SCTradeItemPutupPacket = 0x139;
        public const ushort SCTradeItemTookdownPacket = 0x13D;
        public const ushort SCTradeOkUpdatePacket = 0x13F;
        public const ushort SCTradeLockUpdatePacket = 0x140;
        public const ushort SCTradeMadePacket = 0x141;
        public const ushort SCTowerDefListPacket = 0x142;
        public const ushort SCTowerDefStartPacket = 0x143;
        public const ushort SCTowerDefEndPacket = 0x144;
        public const ushort SCTowerDefWaveStartPacket = 0x145;
        public const ushort SCCrimeChangedPacket = 0x146;
        public const ushort SCImprisonPacket = 0x147;
        public const ushort SCLockupPacket = 0x148;
        public const ushort SCCriminalArrestedPacket = 0x149;
        public const ushort SCAskImprisonOrTrialPacket = 0x14A;
        public const ushort SCInviteJuryPacket = 0x14B;
        public const ushort SCSummonJuryPacket = 0x14C;
        public const ushort SCJuryBeSeatedPacket = 0x14D;
        public const ushort SCSummonDefendantPacket = 0x14E;
        public const ushort SCCrimeDataPacket = 0x14F;
        public const ushort SCCrimeRecordsPacket = 0x150;
        public const ushort SCTrialInfoPacket = 0x15A;
        public const ushort SCChangeTrialStatePacket = 0x151;
        public const ushort SCChangeJuryOKCountPacket = 0x152;
        public const ushort SCChangeJuryVerdictCountPacket = 0x153;
        public const ushort SCTrialWaitStatusPacket = 0x154;
        public const ushort SCJuryWaitStatusPacket = 0x155;
        public const ushort SCRulingStatusPacket = 0x156;
        public const ushort SCRulingClosedPacket = 0x157;
        public const ushort SCTrialAudienceLeftPacket = 0x158;
        //public const ushort SCTrialAudienceLeftPacket = 0x159; // дубль
        public const ushort SCJuryWaitingNumberPacket = 0x15B;
        public const ushort SCUnderWaterPacket = 0x15C;
        public const ushort SCCharacterGamePointsPacket = 0x15D;
        public const ushort SCHonorPointAddedPacket = 0x15E;
        public const ushort SCJuryPointChangedPacket = 0x15F;
        public const ushort SCAppliedToInstantGamePacket = 0x160;
        public const ushort SCInstantGameStatePacket = 0x161;
        public const ushort SCInviteToInstantGamePacket = 0x162;
        public const ushort SCInstantGameInviteTimeoutPacket = 0x163;
        public const ushort SCInstantGameJoinedPacket = 0x164;
        public const ushort SCInstantGameStartPacket = 0x165;
        public const ushort SCInstantGameEndPacket = 0x166;
        public const ushort SCInstantGameAddPointPacket = 0x167;
        public const ushort SCInstantGameKillPacket = 0x168;
        public const ushort SCInstantGameKillstreakPacket = 0x169;
        public const ushort SCProcessingInstancePacket = 0x16B;
        public const ushort SCDelayedTaskOnInGameNotifyPacket = 0x174;
        public const ushort SCAppellationsPacket = 0x175;
        public const ushort SCAppellationChangedPacket = 0x176;
        public const ushort SCTutorialSavedPacket = 0x177;
        public const ushort SCEmblemUploadedPacket = 0x178;
        public const ushort SCEmblemDownloadedPacket = 0x179;
        public const ushort SCDoodadUccStringPacket = 0x131;
        public const ushort SCSlaveStatePacket = 0x17B;
        public const ushort SCMateStatePacket = 0x17C;
        public const ushort SCFriendsPacket = 0x042;
        public const ushort SCSearchListPacket = 0x043;
        public const ushort SCAddFriendPacket = 0x044;
        public const ushort SCDeleteFriendPacket = 0x045;
        public const ushort SCFriendStatusChangedPacket = 0x046;
        public const ushort SCBlockedUsersPacket = 0x048;
        public const ushort SCAddBlockedUserPacket = 0x049;
        public const ushort SCDeleteBlockedUserPacket = 0x04A;
        public const ushort SCCharBriefPacket = 0x04C;
        public const ushort SCItemUccDataChangedPacket = 0x136;
        public const ushort SCShowQuestAreaPacket = 0x17E;
        public const ushort SCShowCommonFarmPacket = 0x17F;
        public const ushort SCTelescopeToggledPacket = 0x181;
        public const ushort SCMateFiredPacket = 0x01A;
        public const ushort SCDominionOwnerChangedPacket = 0x01B;
        public const ushort SCDominionTaxRatePacket = 0x01C;
        public const ushort SCDominionTaxMoneyPacket = 0x01D;
        public const ushort SCFactionRelationChangeProposedPacket = 0x01E;
        public const ushort SCFactionIndependencePacket = 0x01F;
        public const ushort SCInvitationCanceledPacket = 0xfff;
        public const ushort SCFactionRelationChangeCancelledPacket = 0x021;
        public const ushort SCFactionRelationChangeExpiredPacket = 0x022;
        public const ushort SCQuestMailSentPacket = 0xfff;
        public const ushort SCDemoCharResetItemPacket = 0x184;
        public const ushort SCDemoCharResetLocPacket = 0x185;
        public const ushort SCDemoResetActionSlotPacket = 0x186;
        public const ushort SCSetBreathPacket = 0x187;
        public const ushort SCHungPacket = 0x113;
        public const ushort SCHSRequestPacket = 0x18F;
        public const ushort SCDoodadReqBattleFieldPacket = 0x189;
        public const ushort SCCraftFailedPacket = 0x18A;
        public const ushort SCUnitLocationPacket = 0x190;
        public const ushort SCRestrictInfoPacket = 0x191;
        public const ushort SCAppellationGainedPacket = 0xfff;
        public const ushort SCResponseUIDataPacket = 0x193;
        public const ushort SCUnitVisualOptionsPacket = 0x194;
        public const ushort SCNotifyUIMessagePacket = 0x195;
        public const ushort SCRefreshInCharacterListPacket = 0x196;
        public const ushort SCResultRestrictCheckPacket = 0x197;
        public const ushort SCCharacterReturnDistrictsPacket = 0x04E;
        public const ushort SCCharacterBoundPacket = 0x03D;
        public const ushort SCKnockBackUnitPacket = 0x062;
        public const ushort SCItemDetailUpdatedPacket = 0x07E;
        public const ushort SCUnitEquipmentsChangedPacket = 0x07F;
        public const ushort SCCofferContentsUpdatePacket = 0x081;
        public const ushort SCItemAcquisitionPacket = 0x082;
        public const ushort SCCvFCombatRelationshipPacket = 0x073;
        public const ushort SCFvFCombatRelationshipPacket = 0x074;
        public const ushort SCTeamMemberDisconnectedPacket = 0x0B6;
        public const ushort SCTeamPingPosPacket = 0x0C2;
        public const ushort SCLootDicePacket = 0x0D4;
        public const ushort SCLootDiceNotifyPacket = 0x0D5;
        public const ushort SCDoodadCreatedPacket = 0x0E7;
        public const ushort SCDoodadsCreatedPacket = 0x0ED;
        public const ushort SCSoldItemListPacket = 0x109;
        public const ushort SCAuctionPostedPacket = 0xfff;
        public const ushort SCAuctionCanceledPacket = 0xfff;
        public const ushort SCPlotEventPacket = 0x093;
        public const ushort SCInteractionCinemaPacket = 0x118;
        public const ushort SCGimmicksCreatedPacket = 0x119;
        public const ushort SCWorldAABBPacket = 0x121;
        public const ushort SCGmDumpEquipmentPacket = 0x16E;
        public const ushort SCPortalInfoSavedPacket = 0x04F;
        public const ushort SCOtherTradeItemPutupPacket = 0x13A;
        public const ushort SCOtherTradeItemTookdownPacket = 0x13E;
        public const ushort SCLoadInstancePacket = 0x16A;
        public const ushort SCResponseCommonFarmListPacket = 0x180;
        public const ushort SCAiDebugPacket = 0x18D;
        public const ushort SCAiAggroPacket = 0x18E;
        public const ushort SCCreateCharacterResponsePacket = 0x02D;
        public const ushort SCEditCharacterResponsePacket = 0x02F;
        public const ushort SCCharacterListPacket = 0x033;
        public const ushort SCInvenExpandedPacket = 0x041;
        public const ushort SCCharacterPortalsPacket = 0x04D;
        public const ushort SCUnitStatePacket = 0x058;
        public const ushort SCItemEnchantedPacket = 0x07D;
        public const ushort SCHouseBuildPayChangedPacket = 0x0A1;
        public const ushort SCMyHousePacket = 0x0A3;
        public const ushort SCHouseTaxInfoPacket = 0x0A6;
        public const ushort SCJoinedTeamPacket = 0x0B0;
        public const ushort SCTeamLootingRuleChangedPacket = 0x0BD;
        public const ushort SCSiegeStatePacket = 0x0C3;
        public const ushort SCLootBagDataPacket = 0x0D1;
        public const ushort SCMailBodyPacket = 0x0F6;
        public const ushort SCChargeMoneyPaidPacket = 0x0F9;
        public const ushort SCMateEquipmentChangedPacket = 0x103;
        public const ushort SCAuctionSearchedPacket = 0x10C;
        public const ushort SCAuctionMessagePacket = 0x10F;
        public const ushort SCGimmickMovementPacket = 0x11B;
        public const ushort SCTradeMoneyPutupPacket = 0x13B;
        public const ushort SCOtherTradeMoneyPutupPacket = 0x13C;
        public const ushort SCShipyardStatePacket = 0x17A;
        public const ushort SCCharDetailPacket = 0x047;
        public const ushort SCLoginCharInfoHousePacket = 0x04B;
        public const ushort SCTelescopeUnitsPacket = 0x182;
        public const ushort SCDominionDataPacket = 0x019;
        public const ushort SCUnitMovementsPacket = 0x05A;
        public const ushort SCHouseStatePacket = 0x09E;
        public const ushort SCFarmFieldSeedListPacket = 0x0E3;
        public const ushort SCNpcSpawnerPacket = 0x111;
        public const ushort SCCharacterStatePacket = 0x035;

        // нет таких
        public const ushort SCAbilityExpChangedPacket = 0xfff;
        public const ushort SCOnOffSnowPacket = 0xfff;
        public const ushort SCPremiumPointChangedPacket = 0xfff;
        public const ushort SCPremiumServiceListPacket = 0xfff;
        public const ushort SCRankAlarmPacket = 0xfff;
        public const ushort SCScheduleItemUpdatePacket = 0xfff;
        public const ushort SCSlaveEquipmentChangedPacket = 0xfff;
        public const ushort SCSuspectGoingBotTrial = 0xfff;
        public const ushort SCTeamAckRiskyActionPacket = 0xfff;
        public const ushort SCUnitFlyingStateChangedPacket = 0xfff;
        public const ushort SCUnitGmModeChangedPacket = 0xfff;
        public const ushort SCUnitIdleStatusPacket = 0xfff;
        public const ushort SCUnitPvPPointsChangedPacket = 0xfff;
        public const ushort SCUnlockCurrencySlotPacket = 0xfff;
        public const ushort SCUpdatePremiumPointPacket = 0xfff;
        public const ushort SCWorldMessagePacket = 0xfff;
        public const ushort SCWorldQueuePacket = 0xfff;
        public const ushort SCAuctionLowestPricePacket = 0xfff;
        public const ushort SCBmPointPacket = 0xfff;
        public const ushort SCCharacterRenamedPacket = 0xfff;
        public const ushort SCDominionTaxBalancedPacket = 0xfff;
        public const ushort SCEscapeSlavePacket = 0xfff;
        public const ushort SCExpeditionShowRenameUIPacket = 0xfff;
        public const ushort SCExpertExpandedPacket = 0xfff;
        public const ushort SCFactionDeclareHostileResultPacket = 0xfff;
        public const ushort SCFactionImmigrateInvitePacket = 0xfff;
        public const ushort SCFactionImmigrateInviteResultPacket = 0xfff;
        public const ushort SCFactionImmigrateToOriginResultPacket = 0xfff;
        public const ushort SCFactionKickToOriginResultPacket = 0xfff;
        public const ushort SCFactionOwnerChangedPacket = 0xfff;
        public const ushort SCFactionRelationExpiredPacket = 0xfff;
        public const ushort SCFactionRenamedPacket = 0xfff;
        public const ushort SCFactionRetryRenamePacket = 0xfff;
        public const ushort SCFamilyMemberNameChangedPacket = 0xfff;
        public const ushort SCGamePointChangedPacket = 0xfff;
        public const ushort SCGetSlotCountPacket = 0xfff;
        public const ushort SCGradeEnchantBroadcastPacket = 0xfff;
        public const ushort SCGradeEnchantResultPacket = 0xfff;
        public const ushort SCHackGuardRetAddrsRequestPacket = 0xfff;
        public const ushort SCHouseOwnerNameChangedPacket = 0xfff;
        public const ushort SCHouseResetForSalePacket = 0xfff;
        public const ushort SCHouseSetForSalePacket = 0xfff;
        public const ushort SCHouseSoldPacket = 0xfff;
        public const ushort SCIdleKickPacket = 0xfff;
        public const ushort SCItemSocketingLunagemResultPacket = 0xfff;
        public const ushort SCItemSocketingLunastoneResultPacket = 0xfff;
        public const ushort SCNationalMonumentChangedPacket = 0xfff;
        public const ushort SCNationalTaxRatePacket = 0xfff;
        public const ushort SCICSExchangeRatioPacket = 0xfff;
        public const ushort SCOneUnitMovementPacket = 0xfff;
        public const ushort SCAccountAttributeConfigPacket = 0xfff;
        public const ushort SCPlaytimePacket = 0xfff;
        public const ushort SCTrialAudienceJoinedPacket = 0xfff;
        public const ushort SCTrialCancledPacket = 0xfff;
        public const ushort SCTrionConfigPacket = 0xfff;
        public const ushort SCUnitBountyMoneyPacket = 0xfff;
        public const ushort SCUnitOfflinePacket = 0xfff;
        public const ushort SCAggroTargetChanged = 0xfff;
        public const ushort SCCannotStartTradePacket = 0xfff;
        public const ushort SCCanStartTradePacket = 0xfff;
        public const ushort SCConflictZoneHonorPointSumPacket = 0xfff;
        public const ushort SCConstructHouseTaxPacket = 0xfff;
        public const ushort SCDominionDeletedPacket = 0xfff;
        public const ushort SCFactionCreatedPacket = 0xfff;
        public const ushort SCFactionSetRelationStatePacket = 0xfff;
        public const ushort SCHousingRecoverTogglePacket = 0xfff;
        public const ushort SCICSBuyResultPacket = 0xfff;
        public const ushort SCICSCashPointPacket = 0xfff;
        public const ushort SCICSCheckTimePacket = 0xfff;
        public const ushort SCICSGoodDetailPacket = 0xfff;
        public const ushort SCICSGoodListPacket = 0xfff;
        public const ushort SCICSMenuListPacket = 0xfff;
        public const ushort SCICSSyncGoodPacket = 0xfff;
        public const ushort SCLevelRestrictionConfigPacket = 0xfff;
        public const ushort SCMySlavePacket = 0xfff;
        public const ushort SCNpcChatMessagePacket = 0xfff;
    }
}
