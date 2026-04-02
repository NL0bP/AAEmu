using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.GameData.Framework;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Crafts;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.TodayAssignments;
using AAEmu.Game.Models.Game.World.Zones;
using AAEmu.Game.Utils.DB;

using Microsoft.Data.Sqlite;

namespace AAEmu.Game.GameData
{
    [GameData]
    public class TodayAssignmentGameData : Singleton<TodayAssignmentGameData>, IGameDataLoader
    {
        private sealed class AssignmentCandidate
        {
            public required int GroupId { get; init; }
            public required int QuestContextId { get; init; }
            public required int TierMarker { get; init; }
            public required int ChainLength { get; init; }
            public required int ProfessionPoints { get; init; }
            public required int LevelDistance { get; init; }
            public required bool HasProfessionMatch { get; init; }
        }

        private sealed class QuestChainCandidate
        {
            public required int GroupId { get; init; }
            public required int QuestContextId { get; init; }
            public required int TierMarker { get; init; }
            public required int ChainLength { get; init; }
            public required int LevelDistance { get; init; }
        }

        private sealed class PatrolCandidate
        {
            public required int GroupId { get; init; }
            public required int QuestContextId { get; init; }
            public required int TierMarker { get; init; }
            public required int ChainLength { get; init; }
            public required uint ZoneGroupId { get; init; }
            public required int ZoneMinLevel { get; init; }
            public required int ZoneMaxLevel { get; init; }
            public required int NpcLevelDistance { get; init; }
            public required float ZoneDistance { get; init; }
            public required bool IsCurrentZone { get; init; }
            public required bool IsConflictPreferred { get; init; }
        }

        private sealed class PatrolQuestInfo
        {
            public required uint ZoneGroupId { get; init; }
            public required int MinLevel { get; init; }
            public required int MaxLevel { get; init; }
        }

        private sealed class HeroCandidate
        {
            public required int GroupId { get; init; }
            public required int QuestContextId { get; init; }
            public required int ChainLength { get; init; }
            public required bool HasKillObjective { get; init; }
            public required bool HasZoneObjective { get; init; }
            public required int LevelDistance { get; init; }
            public required float ZoneDistance { get; init; }
        }

        private sealed class HeroQuestInfo
        {
            public required bool HasKillObjective { get; init; }
            public required bool HasZoneObjective { get; init; }
            public required int LevelDistance { get; init; }
            public required float ZoneDistance { get; init; }
        }

        private sealed class EliteCandidate
        {
            public required int GroupId { get; init; }
            public required int QuestContextId { get; init; }
            public required int ChainLength { get; init; }
            public required string ChainKey { get; init; }
        }

        private ConcurrentDictionary<int, TodayQuestStep> _todayQuestSteps;
        private ConcurrentDictionary<int, TodayQuestGroup> _todayQuestGroups;
        private ConcurrentDictionary<int, ConcurrentBag<TodayQuestGroupQuest>> _todayQuestGroupQuests;
        private List<TodayQuestGoal> _todayQuestGoals;

        #region TodayAssignment

        public int GetTodayQuestStepId(int realStep)
        {
            return GetTodayQuestStepByRealStep(realStep)?.Id ?? 0;
        }

        public int GetTodayQuestGroupId(int todayQuestStepId)
        {
            return GetTodayQuestGroups(todayQuestStepId).FirstOrDefault()?.Id ?? 0;
        }

        public int GetTodayQuestGroupQuestContextId(int todayQuestGroupId)
        {
            return GetTodayQuestGroupQuests(todayQuestGroupId).FirstOrDefault()?.QuestContextId ?? 0;
        }

        public IReadOnlyCollection<int> GetAllQuestContextIds()
        {
            return _todayQuestGroupQuests.Values
                .SelectMany(groupQuests => groupQuests)
                .Select(groupQuest => groupQuest.QuestContextId)
                .Distinct()
                .ToList();
        }

        public TodayQuestStep GetTodayQuestStepByRealStep(int realStep)
        {
            return _todayQuestSteps.Values
                .FirstOrDefault(step => step.RealStep == realStep);
        }

        public IReadOnlyList<TodayQuestGroup> GetTodayQuestGroups(int stepId)
        {
            return _todayQuestGroups.Values
                .Where(group => group.StepId == stepId)
                .OrderBy(group => group.Id)
                .ToList();
        }

        public IReadOnlyList<TodayQuestGroupQuest> GetTodayQuestGroupQuests(int groupId)
        {
            if (!_todayQuestGroupQuests.TryGetValue(groupId, out var groupQuests))
            {
                return [];
            }

            return groupQuests
                .OrderBy(groupQuest => groupQuest.QuestContextId)
                .ToList();
        }

        public int GetQuestTierIndexForLevel(int level)
        {
            if (_todayQuestGoals.Count == 0)
            {
                return level <= 17 ? 0 : level <= 29 ? 1 : 2;
            }

            var goalId = _todayQuestGoals
                .OrderBy(goal => goal.Level)
                .FirstOrDefault(goal => level <= goal.Level)?.Id ?? _todayQuestGoals.Max(goal => goal.Id);

            return goalId switch
            {
                1 => 0,
                2 => 1,
                _ => 2
            };
        }

        public CharacterTodayAssignmentState SelectAssignmentForCharacter(Character owner, int realStep, int preferredGroupId = 0)
        {
            var step = GetTodayQuestStepByRealStep(realStep);
            if (step == null || !PassStepRequirements(owner, step))
            {
                return null;
            }

            if (step.RealStep == 2)
            {
                var preferredGroups = preferredGroupId > 0 ? new List<int> { preferredGroupId } : new List<int>();
                var productionCandidate = SelectProductionCandidate(
                    owner,
                    preferredGroups.Count > 0
                        ? preferredGroups
                        : GetTodayQuestGroups(step.Id)
                            .Where(group => PassGroupRequirements(owner, group))
                            .Select(group => group.Id)
                            .ToList());

                if (productionCandidate != null)
                {
                    return new CharacterTodayAssignmentState
                    {
                        RealStep = realStep,
                        StepId = step.Id,
                        GroupId = productionCandidate.GroupId,
                        QuestContextId = productionCandidate.QuestContextId
                    };
                }
            }

            if (step.RealStep == 1)
            {
                var preferredGroups = preferredGroupId > 0 ? new List<int> { preferredGroupId } : new List<int>();
                var patrolCandidate = SelectPatrolCandidate(
                    owner,
                    preferredGroups.Count > 0
                        ? preferredGroups
                        : GetTodayQuestGroups(step.Id)
                            .Where(group => PassGroupRequirements(owner, group))
                            .Select(group => group.Id)
                            .ToList());

                if (patrolCandidate != null)
                {
                    return new CharacterTodayAssignmentState
                    {
                        RealStep = realStep,
                        StepId = step.Id,
                        GroupId = patrolCandidate.GroupId,
                        QuestContextId = patrolCandidate.QuestContextId
                    };
                }
            }

            if (step.RealStep == 3)
            {
                var preferredGroups = preferredGroupId > 0 ? new List<int> { preferredGroupId } : new List<int>();
                var dungeonCandidate = SelectDungeonCandidate(
                    owner,
                    preferredGroups.Count > 0
                        ? preferredGroups
                        : GetTodayQuestGroups(step.Id)
                            .Where(group => PassGroupRequirements(owner, group))
                            .Select(group => group.Id)
                            .ToList());

                if (dungeonCandidate != null)
                {
                    return new CharacterTodayAssignmentState
                    {
                        RealStep = realStep,
                        StepId = step.Id,
                        GroupId = dungeonCandidate.GroupId,
                        QuestContextId = dungeonCandidate.QuestContextId
                    };
                }
            }

            if (step.RealStep == 4)
            {
                var preferredGroups = preferredGroupId > 0 ? new List<int> { preferredGroupId } : new List<int>();
                var heroCandidate = SelectHeroCandidate(
                    owner,
                    preferredGroups.Count > 0
                        ? preferredGroups
                        : GetTodayQuestGroups(step.Id)
                            .Where(group => PassGroupRequirements(owner, group))
                            .Select(group => group.Id)
                            .ToList());

                if (heroCandidate != null)
                {
                    return new CharacterTodayAssignmentState
                    {
                        RealStep = realStep,
                        StepId = step.Id,
                        GroupId = heroCandidate.GroupId,
                        QuestContextId = heroCandidate.QuestContextId
                    };
                }
            }

            if (step.RealStep is 5 or 6)
            {
                var preferredGroups = preferredGroupId > 0 ? new List<int> { preferredGroupId } : new List<int>();
                var eliteCandidate = SelectEliteCandidate(
                    owner,
                    realStep,
                    preferredGroups.Count > 0
                        ? preferredGroups
                        : GetTodayQuestGroups(step.Id)
                            .Where(group => PassGroupRequirements(owner, group))
                            .Select(group => group.Id)
                            .ToList());

                if (eliteCandidate != null)
                {
                    return new CharacterTodayAssignmentState
                    {
                        RealStep = realStep,
                        StepId = step.Id,
                        GroupId = eliteCandidate.GroupId,
                        QuestContextId = eliteCandidate.QuestContextId
                    };
                }
            }

            if (preferredGroupId > 0)
            {
                var preferredQuestId = SelectQuestContextId(owner, preferredGroupId);
                if (preferredQuestId > 0)
                {
                    return new CharacterTodayAssignmentState
                    {
                        RealStep = realStep,
                        StepId = step.Id,
                        GroupId = preferredGroupId,
                        QuestContextId = preferredQuestId
                    };
                }
            }

            foreach (var group in GetTodayQuestGroups(step.Id))
            {
                if (!PassGroupRequirements(owner, group))
                {
                    continue;
                }

                var questContextId = SelectQuestContextId(owner, group.Id);
                if (questContextId <= 0)
                {
                    continue;
                }

                return new CharacterTodayAssignmentState
                {
                    RealStep = realStep,
                    StepId = step.Id,
                    GroupId = group.Id,
                    QuestContextId = questContextId
                };
            }

            return null;
        }

        public bool IsAssignmentStillValid(Character owner, CharacterTodayAssignmentState state)
        {
            var step = GetTodayQuestStepByRealStep(state.RealStep);
            if (step == null || !PassStepRequirements(owner, step))
            {
                return false;
            }

            if (!_todayQuestGroups.TryGetValue(state.GroupId, out var group) || group.StepId != step.Id)
            {
                return false;
            }

            if (!PassGroupRequirements(owner, group))
            {
                return false;
            }

            return GetTodayQuestGroupQuests(state.GroupId).Any(x => x.QuestContextId == state.QuestContextId) &&
                   PassQuestRequirements(owner, (uint)state.QuestContextId);
        }

        public int GetNextQuestContextId(Character owner, int groupId, int currentQuestContextId)
        {
            var chain = GetOrderedQuestContextIds(owner, groupId, currentQuestContextId);
            var currentIndex = chain.IndexOf(currentQuestContextId);
            if (currentIndex < 0)
            {
                return 0;
            }

            return currentIndex + 1 < chain.Count ? chain[currentIndex + 1] : 0;
        }

        public bool IsTodayAssignmentQuest(uint questContextId)
        {
            return _todayQuestGroupQuests.Values
                .SelectMany(groupQuests => groupQuests)
                .Any(groupQuest => groupQuest.QuestContextId == questContextId);
        }

        private bool PassStepRequirements(Character owner, TodayQuestStep step)
        {
            if (step.FamilyOnly)
            {
                var family = FamilyManager.Instance.GetFamily(owner.Family);
                if (family == null)
                {
                    return false;
                }

                if (step.FamilyLevelMin > 0 && family.Level < step.FamilyLevelMin)
                {
                    return false;
                }

                if (step.FamilyLevelMax > 0 && family.Level > step.FamilyLevelMax)
                {
                    return false;
                }
            }

            if (step.ExpeditionOnly)
            {
                var expedition = owner.Expedition;
                if (expedition == null)
                {
                    return false;
                }

                if (step.ExpeditionLevelMin > 0 && expedition.Level < step.ExpeditionLevelMin)
                {
                    return false;
                }

                if (step.ExpeditionLevelMax > 0 && expedition.Level > step.ExpeditionLevelMax)
                {
                    return false;
                }
            }

            return true;
        }

        private bool PassGroupRequirements(Character owner, TodayQuestGroup group)
        {
            if (group.ExpeditionLevelMin > 0 || group.ExpeditionLevelMax > 0)
            {
                var expedition = owner.Expedition;
                if (expedition == null)
                {
                    return false;
                }

                if (group.ExpeditionLevelMin > 0 && expedition.Level < group.ExpeditionLevelMin)
                {
                    return false;
                }

                if (group.ExpeditionLevelMax > 0 && expedition.Level > group.ExpeditionLevelMax)
                {
                    return false;
                }
            }

            var groupFactionSide = CharacterTodayAssignments.GetFactionSideForQuests(
                GetTodayQuestGroupQuests(group.Id).Select(x => (uint)x.QuestContextId)
            );
            var characterSide = CharacterTodayAssignments.GetFactionSide(owner);
            if (groupFactionSide != 0 && characterSide != 0 && groupFactionSide != characterSide)
            {
                return false;
            }

            return true;
        }

        private int SelectQuestContextId(Character owner, int groupId)
        {
            if (IsPatrolStepGroup(groupId))
            {
                var patrolCandidate = SelectPatrolCandidate(owner, [groupId]);
                if (patrolCandidate != null)
                {
                    return patrolCandidate.QuestContextId;
                }
            }

            if (IsProductionStepGroup(groupId))
            {
                var productionCandidate = SelectProductionCandidate(owner, [groupId]);
                if (productionCandidate != null)
                {
                    return productionCandidate.QuestContextId;
                }
            }

            if (IsDungeonStepGroup(groupId))
            {
                var dungeonCandidate = SelectDungeonCandidate(owner, [groupId]);
                if (dungeonCandidate != null)
                {
                    return dungeonCandidate.QuestContextId;
                }
            }

            if (IsHeroStepGroup(groupId))
            {
                var heroCandidate = SelectHeroCandidate(owner, [groupId]);
                if (heroCandidate != null)
                {
                    return heroCandidate.QuestContextId;
                }
            }

            if (IsEliteStepGroup(groupId))
            {
                var realStep = _todayQuestGroups.TryGetValue(groupId, out var group) &&
                               _todayQuestSteps.TryGetValue(group.StepId, out var step)
                    ? step.RealStep
                    : 0;
                var eliteCandidate = SelectEliteCandidate(owner, realStep, [groupId]);
                if (eliteCandidate != null)
                {
                    return eliteCandidate.QuestContextId;
                }
            }

            var orderedQuestIds = GetOrderedQuestContextIds(owner, groupId);
            if (orderedQuestIds.Count == 0)
            {
                return 0;
            }

            return orderedQuestIds[0];
        }

        private PatrolCandidate SelectPatrolCandidate(Character owner, IReadOnlyCollection<int> groupIds)
        {
            var currentZoneGroupId = ZoneManager.Instance.GetZoneByKey(owner.Transform.ZoneId)?.GroupId ?? 0;
            var currentContinentId = currentZoneGroupId > 0
                ? ZoneManager.Instance.GetZoneGroupById(currentZoneGroupId)?.TargetId ?? 0
                : 0;
            var candidates = new List<PatrolCandidate>();

            foreach (var groupId in groupIds)
            {
                foreach (var chain in GetQuestChains(owner, groupId))
                {
                    if (chain.Count == 0)
                    {
                        continue;
                    }

                    var starterQuestId = chain[0];
                    var patrolInfo = GetPatrolQuestInfo((uint)starterQuestId);
                    var zoneDistance = GetZoneGroupDistance(currentZoneGroupId, patrolInfo.ZoneGroupId);
                    var isConflictPreferred = owner.Level >= 30 &&
                                              patrolInfo.ZoneGroupId > 0 &&
                                              IsConflictZone(patrolInfo.ZoneGroupId) &&
                                              IsConflictZoneNearby(currentZoneGroupId, patrolInfo.ZoneGroupId, zoneDistance);

                    candidates.Add(new PatrolCandidate
                    {
                        GroupId = groupId,
                        QuestContextId = starterQuestId,
                        TierMarker = GetQuestTierMarker((uint)starterQuestId),
                        ChainLength = chain.Count,
                        ZoneGroupId = patrolInfo.ZoneGroupId,
                        ZoneMinLevel = patrolInfo.MinLevel,
                        ZoneMaxLevel = patrolInfo.MaxLevel,
                        NpcLevelDistance = GetPatrolLevelDistance(owner.Level, patrolInfo.MinLevel, patrolInfo.MaxLevel),
                        ZoneDistance = zoneDistance,
                        IsCurrentZone = currentZoneGroupId > 0 && patrolInfo.ZoneGroupId == currentZoneGroupId,
                        IsConflictPreferred = isConflictPreferred
                    });
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (owner.Level < 40 && currentContinentId > 0)
            {
                var sameContinentCandidates = candidates
                    .Where(candidate => candidate.ZoneGroupId > 0 &&
                                        ZoneManager.Instance.GetZoneGroupById(candidate.ZoneGroupId)?.TargetId == currentContinentId)
                    .ToList();
                if (sameContinentCandidates.Count > 0)
                {
                    candidates = sameContinentCandidates;
                }
            }

            if (owner.Level <= 20)
            {
                var currentZoneCandidates = candidates
                    .Where(candidate => candidate.ZoneGroupId == currentZoneGroupId &&
                                        IsSuitableLowLevelZone(owner.Level, candidate.ZoneMinLevel, candidate.ZoneMaxLevel))
                    .ToList();
                if (currentZoneCandidates.Count > 0)
                {
                    var currentZoneOrdered = currentZoneCandidates
                        .OrderBy(candidate => candidate.NpcLevelDistance)
                        .ThenByDescending(candidate => candidate.ChainLength)
                        .ThenBy(candidate => candidate.QuestContextId)
                        .ToList();

                    var bestCurrentZone = currentZoneOrdered[0];
                    var currentZonePool = currentZoneOrdered
                        .Where(candidate => candidate.NpcLevelDistance <= bestCurrentZone.NpcLevelDistance + 2)
                        .ToList();

                    return currentZonePool[Rand.Next(currentZonePool.Count)];
                }
            }

            if (owner.Level >= 51)
            {
                var highLevelCandidates = candidates
                    .Where(candidate => candidate.ZoneMaxLevel >= 50)
                    .ToList();
                if (highLevelCandidates.Count > 0)
                {
                    candidates = highLevelCandidates;
                }

                var ordered = candidates
                    .OrderBy(candidate => candidate.NpcLevelDistance)
                    .ThenByDescending(candidate => candidate.IsConflictPreferred)
                    .ThenBy(candidate => candidate.ZoneDistance)
                    .ThenByDescending(candidate => candidate.ChainLength)
                    .ThenBy(candidate => candidate.GroupId)
                    .ThenBy(candidate => candidate.QuestContextId)
                    .ToList();

                var best = ordered[0];
                var levelPool = ordered
                    .Where(candidate => candidate.NpcLevelDistance <= best.NpcLevelDistance + 3)
                    .ToList();
                var warPool = levelPool
                    .Where(candidate => candidate.IsConflictPreferred == best.IsConflictPreferred)
                    .ToList();
                var distancePool = warPool
                    .Where(candidate => candidate.ZoneDistance <= warPool[0].ZoneDistance + 12000f)
                    .ToList();

                var randomPool = distancePool.Count > 0 ? distancePool : (warPool.Count > 0 ? warPool : levelPool);
                return randomPool[Rand.Next(randomPool.Count)];
            }

            if (owner.Level >= 40)
            {
                var ordered = candidates
                    .OrderBy(candidate => candidate.NpcLevelDistance)
                    .ThenBy(candidate => candidate.ZoneDistance)
                    .ThenByDescending(candidate => candidate.IsConflictPreferred)
                    .ThenByDescending(candidate => candidate.ChainLength)
                    .ThenBy(candidate => candidate.GroupId)
                    .ThenBy(candidate => candidate.QuestContextId)
                    .ToList();

                var best = ordered[0];
                var levelPool = ordered
                    .Where(candidate => candidate.NpcLevelDistance <= best.NpcLevelDistance + 3)
                    .ToList();
                var nearbyPool = levelPool
                    .Where(candidate => candidate.ZoneDistance <= levelPool[0].ZoneDistance + 12000f)
                    .ToList();
                var warPool = nearbyPool
                    .Where(candidate => candidate.IsConflictPreferred == nearbyPool[0].IsConflictPreferred)
                    .ToList();

                var randomPool = warPool.Count > 0 ? warPool : (nearbyPool.Count > 0 ? nearbyPool : levelPool);
                return randomPool[Rand.Next(randomPool.Count)];
            }

            var closeOrdered = candidates
                .OrderBy(candidate => candidate.ZoneDistance)
                .ThenBy(candidate => candidate.NpcLevelDistance)
                .ThenByDescending(candidate => candidate.IsConflictPreferred)
                .ThenByDescending(candidate => candidate.ChainLength)
                .ThenBy(candidate => candidate.GroupId)
                .ThenBy(candidate => candidate.QuestContextId)
                .ToList();

            var closest = closeOrdered[0];
            var distancePoolLow = closeOrdered
                .Where(candidate => candidate.ZoneDistance <= closest.ZoneDistance + 12000f)
                .ToList();
            var levelPoolLow = distancePoolLow
                .Where(candidate => candidate.NpcLevelDistance <= distancePoolLow[0].NpcLevelDistance + 4)
                .ToList();
            var warPoolLow = levelPoolLow
                .Where(candidate => candidate.IsConflictPreferred == levelPoolLow[0].IsConflictPreferred)
                .ToList();

            var lowRandomPool = warPoolLow.Count > 0 ? warPoolLow : (levelPoolLow.Count > 0 ? levelPoolLow : distancePoolLow);
            return lowRandomPool[Rand.Next(lowRandomPool.Count)];
        }

        private static bool IsSuitableLowLevelZone(int characterLevel, int minLevel, int maxLevel)
        {
            if (minLevel <= 0 && maxLevel <= 0)
            {
                return false;
            }

            if (minLevel > characterLevel + 4)
            {
                return false;
            }

            if (maxLevel > 0 && maxLevel < characterLevel - 6)
            {
                return false;
            }

            return true;
        }

        private static bool PassQuestRequirements(Character owner, uint questContextId)
        {
            var template = QuestManager.Instance.GetTemplate(questContextId);
            if (template == null)
            {
                return false;
            }

            if (template.Race != 255 && template.Race != (byte)owner.Race)
            {
                return false;
            }

            if (template.MinLevel > 0 && owner.Level < template.MinLevel)
            {
                return false;
            }

            if (template.MaxLevel > 0 && owner.Level > template.MaxLevel)
            {
                return false;
            }

            var questFactionSide = CharacterTodayAssignments.GetFactionSideForQuest(questContextId);
            var characterSide = CharacterTodayAssignments.GetFactionSide(owner);
            if (questFactionSide != CharacterTodayAssignments.TodayAssignmentFactionSide.Neutral &&
                characterSide != CharacterTodayAssignments.TodayAssignmentFactionSide.Neutral &&
                questFactionSide != characterSide)
            {
                return false;
            }

            if (!CanCharacterCompleteCraftObjectives(owner, template))
            {
                return false;
            }

            return true;
        }

        private static bool CanCharacterCompleteCraftObjectives(Character owner, QuestTemplate template)
        {
            foreach (var component in template.Components.Values)
            {
                foreach (var actTemplate in component.ActTemplates)
                {
                    if (actTemplate is QuestActObjCraft craftAct &&
                        !CanCharacterPerformCraft(owner, craftAct.CraftId))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool CanCharacterPerformCraft(Character owner, uint craftId)
        {
            Craft craft;
            try
            {
                craft = CraftManager.Instance.GetCraftById(craftId);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }

            if (craft == null)
            {
                return false;
            }

            var actabilityType = SkillManager.Instance.GetSkillActAbility(craft.SkillId);
            if (actabilityType == ActabilityType.None)
            {
                return true;
            }

            var points = owner.Actability?.Actabilities.TryGetValue((uint)actabilityType, out var actability) == true
                ? actability.Point
                : 0;

            return points >= craft.ActabilityLimit;
        }

        private int GetQuestTierMarkerForLevel(int level)
        {
            return GetQuestTierIndexForLevel(level) switch
            {
                0 => 10,
                1 => 20,
                _ => 30
            };
        }

        private int GetProductionTierMarkerForLevel(int level)
        {
            return GetQuestTierIndexForLevel(level) switch
            {
                0 => 50,
                1 => 100,
                _ => 200
            };
        }

        private static bool IsQuestMatchingTier(uint questContextId, int tierMarker)
        {
            var template = QuestManager.Instance.GetTemplate(questContextId);
            if (template?.Name == null)
            {
                return false;
            }

            return template.Name.EndsWith($" {tierMarker}", StringComparison.Ordinal);
        }

        private List<int> GetOrderedQuestContextIds(Character owner, int groupId)
        {
            var chain = GetQuestChains(owner, groupId)
                .FirstOrDefault();

            if (chain == null || chain.Count == 0)
            {
                return [];
            }

            return chain;
        }

        private List<int> GetOrderedQuestContextIds(Character owner, int groupId, int currentQuestContextId)
        {
            var chains = GetQuestChains(owner, groupId);
            var currentChain = chains.FirstOrDefault(chain => chain.Contains(currentQuestContextId));
            return currentChain is { Count: > 0 } ? currentChain : GetOrderedQuestContextIds(owner, groupId);
        }

        private AssignmentCandidate SelectProductionCandidate(Character owner, IReadOnlyCollection<int> groupIds)
        {
            var candidates = new List<AssignmentCandidate>();

            foreach (var groupId in groupIds)
            {
                var starters = GetProductionStarterQuestIds(owner, groupId);
                foreach (var questContextId in starters)
                {
                    var template = QuestManager.Instance.GetTemplate((uint)questContextId);
                    if (template?.Name == null)
                    {
                        continue;
                    }

                    candidates.Add(new AssignmentCandidate
                    {
                        GroupId = groupId,
                        QuestContextId = questContextId,
                        TierMarker = GetQuestTierMarker((uint)questContextId),
                        ChainLength = GetOrderedQuestContextIds(owner, groupId, questContextId).Count,
                        ProfessionPoints = 0,
                        LevelDistance = 0,
                        HasProfessionMatch = false
                    });
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[Rand.Next(candidates.Count)];
        }

        private List<int> GetProductionStarterQuestIds(Character owner, int groupId)
        {
            var result = new List<int>();
            foreach (var chain in GetQuestChains(owner, groupId))
            {
                if (chain.Count > 0 && IsEligibleProductionChain(chain))
                {
                    result.Add(chain[0]);
                }
            }

            return result;
        }

        private static bool IsEligibleProductionChain(IReadOnlyList<int> chain)
        {
            if (chain.Count == 0)
            {
                return false;
            }

            return chain.Any(questContextId => HasActionableProductionObjective((uint)questContextId));
        }

        private static bool HasActionableProductionObjective(uint questContextId)
        {
            var template = QuestManager.Instance.GetTemplate(questContextId);
            if (template == null)
            {
                return false;
            }

            foreach (var component in template.Components.Values)
            {
                foreach (var actTemplate in component.ActTemplates)
                {
                    switch (actTemplate)
                    {
                        case QuestActObjCraft:
                        case QuestActObjItemGather:
                        case QuestActObjLaborPower:
                            return true;
                    }
                }
            }

            return false;
        }

        private QuestChainCandidate SelectDungeonCandidate(Character owner, IReadOnlyCollection<int> groupIds)
        {
            var eligibleGroupIds = GetEligibleDungeonGroupIds(owner.Level, groupIds)
                .ToList();
            if (eligibleGroupIds.Count == 0)
            {
                return null;
            }

            var weightedGroups = new List<int>();
            foreach (var groupId in eligibleGroupIds)
            {
                var weight = GetDungeonGroupWeight(owner.Level, groupId);
                for (var i = 0; i < weight; i++)
                {
                    weightedGroups.Add(groupId);
                }
            }

            if (weightedGroups.Count == 0)
            {
                return null;
            }

            var selectedGroupId = weightedGroups[Rand.Next(weightedGroups.Count)];
            var candidates = new List<QuestChainCandidate>();
            foreach (var chain in GetQuestChains(owner, selectedGroupId))
            {
                if (chain.Count == 0)
                {
                    continue;
                }

                var starterQuestId = chain[0];
                candidates.Add(new QuestChainCandidate
                {
                    GroupId = selectedGroupId,
                    QuestContextId = starterQuestId,
                    TierMarker = GetQuestTierMarker((uint)starterQuestId),
                    ChainLength = chain.Count,
                    LevelDistance = 0
                });
            }

            return candidates.Count == 0 ? null : candidates[Rand.Next(candidates.Count)];
        }

        private static IEnumerable<int> GetEligibleDungeonGroupIds(int characterLevel, IReadOnlyCollection<int> groupIds)
        {
            var earlyGroups = new HashSet<int> { 33, 34, 36 };
            var lateGroups = new HashSet<int> { 38, 39, 40, 92 };

            if (groupIds.Count == 1)
            {
                return groupIds;
            }

            if (characterLevel < 52)
            {
                var filtered = groupIds.Where(earlyGroups.Contains).ToList();
                return filtered.Count > 0 ? filtered : groupIds;
            }

            var combined = groupIds.Where(groupId => earlyGroups.Contains(groupId) || lateGroups.Contains(groupId)).ToList();
            return combined.Count > 0 ? combined : groupIds;
        }

        private static int GetDungeonGroupWeight(int characterLevel, int groupId)
        {
            if (characterLevel < 52)
            {
                return groupId is 33 or 34 or 36 ? 1 : 0;
            }

            return groupId switch
            {
                38 or 39 or 40 or 92 => 3,
                33 or 34 or 36 => 1,
                _ => 0
            };
        }

        private QuestChainCandidate SelectHeroCandidate(Character owner, IReadOnlyCollection<int> groupIds)
        {
            var currentZoneGroupId = ZoneManager.Instance.GetZoneByKey(owner.Transform.ZoneId)?.GroupId ?? 0;
            var candidates = new List<HeroCandidate>();
            foreach (var groupId in groupIds)
            {
                foreach (var chain in GetQuestChains(owner, groupId))
                {
                    if (chain.Count == 0)
                    {
                        continue;
                    }

                    var starterQuestId = chain[0];
                    var questInfo = GetHeroQuestInfo(owner, (uint)starterQuestId, currentZoneGroupId);

                    candidates.Add(new HeroCandidate
                    {
                        GroupId = groupId,
                        QuestContextId = starterQuestId,
                        ChainLength = chain.Count,
                        HasKillObjective = questInfo.HasKillObjective,
                        HasZoneObjective = questInfo.HasZoneObjective,
                        LevelDistance = questInfo.LevelDistance,
                        ZoneDistance = questInfo.ZoneDistance
                    });
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var ordered = candidates
                .OrderBy(candidate => candidate.LevelDistance)
                .ThenBy(candidate => candidate.ZoneDistance)
                .ThenByDescending(candidate => candidate.ChainLength)
                .ThenBy(candidate => candidate.GroupId)
                .ThenBy(candidate => candidate.QuestContextId)
                .ToList();

            var best = ordered[0];
            var levelPool = ordered
                .Where(candidate => candidate.LevelDistance <= best.LevelDistance + 3)
                .ToList();
            var finiteZonePool = levelPool
                .Where(candidate => candidate.ZoneDistance < float.MaxValue / 8)
                .ToList();
            if (finiteZonePool.Count > 0)
            {
                var bestZone = finiteZonePool.Min(candidate => candidate.ZoneDistance);
                finiteZonePool = finiteZonePool
                    .Where(candidate => candidate.ZoneDistance <= bestZone + 12000f)
                    .ToList();
            }
            var chainBasePool = finiteZonePool.Count > 0 ? finiteZonePool : levelPool;
            var fallback = chainBasePool
                .OrderByDescending(candidate => candidate.ChainLength)
                .ThenBy(candidate => candidate.GroupId)
                .ThenBy(candidate => candidate.QuestContextId)
                .ToList();
            var fallbackPool = fallback
                .Where(candidate => candidate.ChainLength == fallback[0].ChainLength)
                .ToList();
            var selectedFallback = fallbackPool[Rand.Next(fallbackPool.Count)];
            return new QuestChainCandidate
            {
                GroupId = selectedFallback.GroupId,
                QuestContextId = selectedFallback.QuestContextId,
                TierMarker = GetQuestTierMarker((uint)selectedFallback.QuestContextId),
                ChainLength = selectedFallback.ChainLength,
                LevelDistance = 0
            };
        }

        private EliteCandidate SelectEliteCandidate(Character owner, int realStep, IReadOnlyCollection<int> groupIds)
        {
            var candidates = new List<EliteCandidate>();
            foreach (var groupId in groupIds)
            {
                foreach (var chain in GetQuestChains(owner, groupId))
                {
                    if (chain.Count == 0)
                    {
                        continue;
                    }

                    var starterQuestId = chain[0];
                    var chainKey = GetQuestChainIdentity(groupId, starterQuestId);
                    if (chainKey.Length == 0)
                    {
                        continue;
                    }

                    candidates.Add(new EliteCandidate
                    {
                        GroupId = groupId,
                        QuestContextId = starterQuestId,
                        ChainLength = chain.Count,
                        ChainKey = chainKey
                    });
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var uniqueCandidates = candidates
                .GroupBy(candidate => candidate.ChainKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(candidate => candidate.ChainLength)
                    .ThenBy(candidate => candidate.GroupId)
                    .ThenBy(candidate => candidate.QuestContextId)
                    .First())
                .ToList();

            var takenChainKeys = owner.TodayAssignments?.GetAssignedSelections()
                .Where(selection => selection.RealStep != realStep && selection.RealStep is 5 or 6)
                .Select(selection => GetQuestChainIdentity(selection.GroupId, selection.QuestContextId))
                .Where(chainKey => chainKey.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? [];

            var availableCandidates = uniqueCandidates
                .Where(candidate => !takenChainKeys.Contains(candidate.ChainKey))
                .ToList();

            var pool = availableCandidates.Count > 0 ? availableCandidates : uniqueCandidates;
            return pool[Rand.Next(pool.Count)];
        }

        private List<List<int>> GetQuestChains(Character owner, int groupId)
        {
            var eligibleQuestIds = GetTodayQuestGroupQuests(groupId)
                .Select(groupQuest => groupQuest.QuestContextId)
                .Where(questId => PassQuestRequirements(owner, (uint)questId))
                .Distinct()
                .OrderBy(questId => questId)
                .ToList();

            if (eligibleQuestIds.Count == 0)
            {
                return [];
            }

            if (IsDungeonStepGroup(groupId))
            {
                return BuildDungeonQuestChains(eligibleQuestIds);
            }

            var groupedChains = eligibleQuestIds
                .Select(questId => new
                {
                    QuestContextId = questId,
                    Template = QuestManager.Instance.GetTemplate((uint)questId)
                })
                .Where(x => x.Template?.Name != null)
                .GroupBy(x => GetQuestChainKey(groupId, x.QuestContextId, x.Template!.Name))
                .Select(group => group
                    .Select(x => x.QuestContextId)
                    .OrderBy(questId =>
                    {
                        var tierMarker = GetQuestTierMarker((uint)questId);
                        return tierMarker == 0 ? int.MaxValue : tierMarker;
                    })
                    .ThenBy(questId => questId)
                    .ToList())
                .OrderByDescending(chain => chain.Count)
                .ThenBy(chain => chain[0])
                .ToList();

            return groupedChains.Count > 0
                ? groupedChains
                : eligibleQuestIds.Select(questId => new List<int> { questId }).ToList();
        }

        private static List<List<int>> BuildDungeonQuestChains(IReadOnlyList<int> eligibleQuestIds)
        {
            var result = new List<List<int>>();
            var index = 0;
            while (index < eligibleQuestIds.Count)
            {
                var currentQuestId = eligibleQuestIds[index];
                if (index + 1 < eligibleQuestIds.Count &&
                    eligibleQuestIds[index + 1] - currentQuestId <= 10)
                {
                    result.Add([currentQuestId, eligibleQuestIds[index + 1]]);
                    index += 2;
                    continue;
                }

                result.Add([currentQuestId]);
                index++;
            }

            return result;
        }

        private bool IsProductionStepGroup(int groupId)
        {
            if (!_todayQuestGroups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            return _todayQuestSteps.TryGetValue(group.StepId, out var step) && step.RealStep == 2;
        }

        private bool IsPatrolStepGroup(int groupId)
        {
            if (!_todayQuestGroups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            return _todayQuestSteps.TryGetValue(group.StepId, out var step) && step.RealStep == 1;
        }

        private bool IsDungeonStepGroup(int groupId)
        {
            if (!_todayQuestGroups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            return _todayQuestSteps.TryGetValue(group.StepId, out var step) && step.RealStep == 3;
        }

        private bool IsHeroStepGroup(int groupId)
        {
            if (!_todayQuestGroups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            return _todayQuestSteps.TryGetValue(group.StepId, out var step) && step.RealStep == 4;
        }

        private bool IsEliteStepGroup(int groupId)
        {
            if (!_todayQuestGroups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            return _todayQuestSteps.TryGetValue(group.StepId, out var step) && step.RealStep is 5 or 6;
        }

        private static int GetTierDistance(int targetTier, int actualTier)
        {
            if (actualTier <= 0)
            {
                return int.MaxValue / 2;
            }

            return Math.Abs(targetTier - actualTier);
        }

        private static int GetHeroTierDistance(int targetTier, int actualTier)
        {
            if (actualTier is 10 or 20 or 30)
            {
                return GetTierDistance(targetTier, actualTier);
            }

            return 1;
        }

        private static HeroQuestInfo GetHeroQuestInfo(Character owner, uint questContextId, uint currentZoneGroupId)
        {
            var template = QuestManager.Instance.GetTemplate(questContextId);
            if (template == null)
            {
                return new HeroQuestInfo
                {
                    HasKillObjective = false,
                    HasZoneObjective = false,
                    LevelDistance = int.MaxValue / 2,
                    ZoneDistance = float.MaxValue / 4
                };
            }

            var hasKillObjective = false;
            var hasZoneObjective = false;
            var bestLevelDistance = int.MaxValue / 2;
            var bestZoneDistance = float.MaxValue / 4;

            foreach (var component in template.Components.Values)
            {
                foreach (var actTemplate in component.ActTemplates)
                {
                    switch (actTemplate)
                    {
                        case QuestActObjZoneKill zoneKillAct:
                            {
                                hasKillObjective = true;
                                hasZoneObjective = true;
                                var minLevel = zoneKillAct.LvlMinNpc;
                                var maxLevel = zoneKillAct.LvlMaxNpc;
                                if (SpawnManager.Instance.TryGetZoneGroupNpcLevelRange(zoneKillAct.ZoneId, out var zoneMinLevel, out var zoneMaxLevel))
                                {
                                    minLevel = zoneMinLevel;
                                    maxLevel = zoneMaxLevel;
                                }

                                bestLevelDistance = Math.Min(bestLevelDistance, GetPatrolLevelDistance(owner.Level, minLevel, maxLevel));
                                bestZoneDistance = Math.Min(bestZoneDistance, GetZoneGroupDistance(currentZoneGroupId, zoneKillAct.ZoneId));
                                break;
                            }
                        case QuestActObjZoneMonsterHunt zoneMonsterHuntAct:
                            {
                                hasKillObjective = true;
                                hasZoneObjective = true;
                                if (SpawnManager.Instance.TryGetZoneGroupNpcLevelRange(zoneMonsterHuntAct.ZoneId, out var zoneMinLevel, out var zoneMaxLevel))
                                {
                                    bestLevelDistance = Math.Min(bestLevelDistance, GetPatrolLevelDistance(owner.Level, zoneMinLevel, zoneMaxLevel));
                                }

                                bestZoneDistance = Math.Min(bestZoneDistance, GetZoneGroupDistance(currentZoneGroupId, zoneMonsterHuntAct.ZoneId));
                                break;
                            }
                        case QuestActObjMonsterHunt monsterHuntAct:
                            {
                                hasKillObjective = true;
                                var npcTemplate = NpcManager.Instance.GetTemplate(monsterHuntAct.NpcId);
                                var npcLevel = npcTemplate?.Level ?? template.Level;
                                bestLevelDistance = Math.Min(bestLevelDistance, GetPatrolLevelDistance(owner.Level, npcLevel, npcLevel));
                                break;
                            }
                        case QuestActObjMonsterGroupHunt:
                            {
                                hasKillObjective = true;
                                bestLevelDistance = Math.Min(bestLevelDistance, GetPatrolLevelDistance(owner.Level, template.MinLevel, template.MaxLevel));
                                break;
                            }
                        case QuestActObjTalk talkAct:
                            {
                                hasZoneObjective = true;
                                break;
                            }
                    }
                }
            }

            if (!hasKillObjective && bestLevelDistance >= int.MaxValue / 4)
            {
                bestLevelDistance = GetQuestLevelDistance(owner.Level, template.MinLevel, template.MaxLevel);
            }

            return new HeroQuestInfo
            {
                HasKillObjective = hasKillObjective,
                HasZoneObjective = hasZoneObjective,
                LevelDistance = bestLevelDistance,
                ZoneDistance = bestZoneDistance
            };
        }

        private static int GetQuestLevelDistance(int characterLevel, int minLevel, int maxLevel)
        {
            if (minLevel > 0 && characterLevel < minLevel)
            {
                return minLevel - characterLevel;
            }

            if (maxLevel > 0 && characterLevel > maxLevel)
            {
                return characterLevel - maxLevel;
            }

            return 0;
        }

        private static int GetPatrolLevelDistance(int characterLevel, int minLevel, int maxLevel)
        {
            if (minLevel <= 0 && maxLevel <= 0)
            {
                return 1000;
            }

            if (minLevel > 0 && characterLevel < minLevel)
            {
                // Patrol should avoid zones that start above the player level unless there is no better fit.
                return (minLevel - characterLevel) * 20;
            }

            if (maxLevel > 0 && characterLevel > maxLevel)
            {
                // Slightly prefer harder zones where the character is near the upper edge,
                // but still allow lower zones as fallback.
                return (characterLevel - maxLevel) * 4 + 25;
            }

            if (maxLevel > 0)
            {
                // Inside the range, smaller distance to the zone cap means a tougher and better patrol pick.
                return Math.Max(0, maxLevel - characterLevel);
            }

            return Math.Max(0, characterLevel - minLevel);
        }

        private static PatrolQuestInfo GetPatrolQuestInfo(uint questContextId)
        {
            var template = QuestManager.Instance.GetTemplate(questContextId);
            if (template == null)
            {
                return new PatrolQuestInfo { ZoneGroupId = 0, MinLevel = 0, MaxLevel = 0 };
            }

            foreach (var component in template.Components.Values)
            {
                foreach (var actTemplate in component.ActTemplates)
                {
                    if (actTemplate is not QuestActObjZoneKill zoneKillAct)
                    {
                        continue;
                    }

                    var minNpcLevel = zoneKillAct.LvlMinNpc;
                    var maxNpcLevel = zoneKillAct.LvlMaxNpc;
                    if (SpawnManager.Instance.TryGetZoneGroupNpcLevelRange(zoneKillAct.ZoneId, out var zoneMinLevel, out var zoneMaxLevel))
                    {
                        minNpcLevel = zoneMinLevel;
                        maxNpcLevel = zoneMaxLevel;
                    }
                    else if (minNpcLevel <= 0 && maxNpcLevel <= 0)
                    {
                        minNpcLevel = template.MinLevel;
                        maxNpcLevel = template.MaxLevel;
                    }

                    return new PatrolQuestInfo
                    {
                        ZoneGroupId = zoneKillAct.ZoneId,
                        MinLevel = minNpcLevel,
                        MaxLevel = maxNpcLevel
                    };
                }
            }

            return new PatrolQuestInfo
            {
                ZoneGroupId = 0,
                MinLevel = template.MinLevel,
                MaxLevel = template.MaxLevel
            };
        }

        private static float GetZoneGroupDistance(uint currentZoneGroupId, uint targetZoneGroupId)
        {
            if (currentZoneGroupId == 0 || targetZoneGroupId == 0)
            {
                return float.MaxValue / 4;
            }

            if (currentZoneGroupId == targetZoneGroupId)
            {
                return 0f;
            }

            var currentGroup = ZoneManager.Instance.GetZoneGroupById(currentZoneGroupId);
            var targetGroup = ZoneManager.Instance.GetZoneGroupById(targetZoneGroupId);
            if (currentGroup == null || targetGroup == null)
            {
                return float.MaxValue / 4;
            }

            var dx = currentGroup.X - targetGroup.X;
            var dy = currentGroup.Y - targetGroup.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private static bool IsConflictZone(uint zoneGroupId)
        {
            var conflict = ZoneManager.Instance.GetZoneGroupById(zoneGroupId)?.Conflict;
            return conflict != null && conflict.CurrentZoneState is ZoneConflictType.Conflict or ZoneConflictType.War;
        }

        private static bool IsConflictZoneNearby(uint currentZoneGroupId, uint targetZoneGroupId, float distance)
        {
            if (distance <= 0f)
            {
                return true;
            }

            var currentGroup = ZoneManager.Instance.GetZoneGroupById(currentZoneGroupId);
            var targetGroup = ZoneManager.Instance.GetZoneGroupById(targetZoneGroupId);
            if (currentGroup == null || targetGroup == null)
            {
                return false;
            }

            var allowedDistance = Math.Max(
                Math.Max(currentGroup.Width, currentGroup.Hight) + Math.Max(targetGroup.Width, targetGroup.Hight),
                7000f);

            return distance <= allowedDistance;
        }

        private string GetQuestChainKey(int groupId, int questContextId, string questName)
        {
            if (IsHeroStepGroup(groupId))
            {
                var stepFourKey = GetStepFourChainKey(questName);
                if (stepFourKey.Length > 0)
                {
                    return stepFourKey;
                }
            }

            var normalizedName = questName.Trim();
            var tierMarker = GetQuestTierMarkerFromName(normalizedName);
            if (tierMarker > 0)
            {
                var tierSuffixes = tierMarker switch
                {
                    1 => new[] { " 1\uB2E8\uACC4", " Stage 1" },
                    2 => new[] { " 2\uB2E8\uACC4", " Stage 2" },
                    _ => new[] { $" {tierMarker}" }
                };

                foreach (var tierSuffix in tierSuffixes)
                {
                    if (normalizedName.EndsWith(tierSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedName = normalizedName[..^tierSuffix.Length].TrimEnd();
                        break;
                    }
                }
            }

            normalizedName = normalizedName
                .Replace(" Dabbler", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" Laborer", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" Professional", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            return normalizedName.Length > 0 ? normalizedName : questContextId.ToString();
        }

        private string GetQuestChainIdentity(int groupId, int questContextId)
        {
            var template = QuestManager.Instance.GetTemplate((uint)questContextId);
            if (template?.Name == null)
            {
                return string.Empty;
            }

            return GetQuestChainKey(groupId, questContextId, template.Name);
        }

        private static string GetStepFourChainKey(string questName)
        {
            var normalizedName = questName.Trim();
            if (normalizedName.EndsWith(" 10", StringComparison.Ordinal) ||
                normalizedName.EndsWith(" 20", StringComparison.Ordinal) ||
                normalizedName.EndsWith(" 30", StringComparison.Ordinal))
            {
                return normalizedName[..^3].TrimEnd();
            }

            if (normalizedName.EndsWith(" 1\uB2E8\uACC4", StringComparison.Ordinal) ||
                normalizedName.EndsWith(" 2\uB2E8\uACC4", StringComparison.Ordinal))
            {
                return normalizedName[..^5].TrimEnd();
            }

            if (normalizedName.EndsWith(" Stage 1", StringComparison.OrdinalIgnoreCase) ||
                normalizedName.EndsWith(" Stage 2", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedName[..^8].TrimEnd();
            }

            return string.Empty;
        }

        private static ActabilityType GetProductionProfessionType(string questName)
        {
            return questName switch
            {
                var name when name.Contains("축산", StringComparison.Ordinal) || name.Contains("Husbandry", StringComparison.OrdinalIgnoreCase) => ActabilityType.Husbandry,
                var name when name.Contains("농사", StringComparison.Ordinal) || name.Contains("Farming", StringComparison.OrdinalIgnoreCase) => ActabilityType.Farming,
                var name when name.Contains("낚시", StringComparison.Ordinal) || name.Contains("Fishing", StringComparison.OrdinalIgnoreCase) => ActabilityType.Fishing,
                var name when name.Contains("벌채", StringComparison.Ordinal) || name.Contains("Logging", StringComparison.OrdinalIgnoreCase) => ActabilityType.Logging,
                var name when name.Contains("채집", StringComparison.Ordinal) || name.Contains("Gathering", StringComparison.OrdinalIgnoreCase) => ActabilityType.Gathering,
                var name when name.Contains("채광", StringComparison.Ordinal) || name.Contains("Mining", StringComparison.OrdinalIgnoreCase) => ActabilityType.Mining,
                var name when name.Contains("연금", StringComparison.Ordinal) || name.Contains("Alchemy", StringComparison.OrdinalIgnoreCase) => ActabilityType.Alchemy,
                var name when name.Contains("요리", StringComparison.Ordinal) || name.Contains("Cooking", StringComparison.OrdinalIgnoreCase) => ActabilityType.Cooking,
                var name when name.Contains("공예", StringComparison.Ordinal) || name.Contains("Handicrafts", StringComparison.OrdinalIgnoreCase) => ActabilityType.Handicrafts,
                var name when name.Contains("기계", StringComparison.Ordinal) || name.Contains("Machining", StringComparison.OrdinalIgnoreCase) => ActabilityType.Machining,
                var name when name.Contains("금속", StringComparison.Ordinal) || name.Contains("Metalwork", StringComparison.OrdinalIgnoreCase) || name.Contains("MetalQuest", StringComparison.OrdinalIgnoreCase) => ActabilityType.Metalwork,
                var name when name.Contains("인쇄", StringComparison.Ordinal) || name.Contains("Printing", StringComparison.OrdinalIgnoreCase) => ActabilityType.Printing,
                var name when name.Contains("석공", StringComparison.Ordinal) || name.Contains("Masonry", StringComparison.OrdinalIgnoreCase) => ActabilityType.Masonry,
                var name when name.Contains("재봉", StringComparison.Ordinal) || name.Contains("Tailoring", StringComparison.OrdinalIgnoreCase) => ActabilityType.Tailoring,
                var name when name.Contains("가죽", StringComparison.Ordinal) || name.Contains("Leatherwork", StringComparison.OrdinalIgnoreCase) => ActabilityType.Leatherwork,
                var name when name.Contains("무기", StringComparison.Ordinal) || name.Contains("Weaponry", StringComparison.OrdinalIgnoreCase) => ActabilityType.Weaponry,
                var name when name.Contains("목공", StringComparison.Ordinal) || name.Contains("Carpentry", StringComparison.OrdinalIgnoreCase) => ActabilityType.Carpentry,
                var name when name.Contains("건축", StringComparison.Ordinal) || name.Contains("Construction", StringComparison.OrdinalIgnoreCase) => ActabilityType.Construction,
                var name when name.Contains("손재주", StringComparison.Ordinal) => ActabilityType.Handicrafts,
                var name when name.Contains("장사", StringComparison.Ordinal) || name.Contains("Commerce", StringComparison.OrdinalIgnoreCase) => ActabilityType.Commerce,
                var name when name.Contains("예술", StringComparison.Ordinal) || name.Contains("Artistry", StringComparison.OrdinalIgnoreCase) => ActabilityType.Artistry,
                var name when name.Contains("탐험", StringComparison.Ordinal) || name.Contains("Exploration", StringComparison.OrdinalIgnoreCase) => ActabilityType.Exploration,
                _ => ActabilityType.None
            };
        }

        private static int GetQuestTierMarker(uint questContextId)
        {
            var template = QuestManager.Instance.GetTemplate(questContextId);
            if (template?.Name == null)
            {
                return 0;
            }

            return GetQuestTierMarkerFromName(template.Name);
        }

        private static int GetQuestTierMarkerFromName(string questName)
        {
            if (questName.EndsWith(" 10", StringComparison.Ordinal))
            {
                return 10;
            }

            if (questName.EndsWith(" 20", StringComparison.Ordinal))
            {
                return 20;
            }

            if (questName.EndsWith(" 30", StringComparison.Ordinal))
            {
                return 30;
            }

            if (questName.EndsWith(" 50", StringComparison.Ordinal))
            {
                return 50;
            }

            if (questName.EndsWith(" 100", StringComparison.Ordinal))
            {
                return 100;
            }

            if (questName.EndsWith(" 200", StringComparison.Ordinal))
            {
                return 200;
            }

            if (questName.EndsWith(" 500", StringComparison.Ordinal))
            {
                return 500;
            }

            if (questName.EndsWith(" 1\uB2E8\uACC4", StringComparison.Ordinal) ||
                questName.EndsWith(" Stage 1", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (questName.EndsWith(" 2\uB2E8\uACC4", StringComparison.Ordinal) ||
                questName.EndsWith(" Stage 2", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (questName.EndsWith("Dabbler", StringComparison.OrdinalIgnoreCase))
            {
                return 50;
            }

            if (questName.EndsWith("Laborer", StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            if (questName.EndsWith("Professional", StringComparison.OrdinalIgnoreCase))
            {
                return 200;
            }

            return 0;
        }

        #endregion TodayAssignment

        #region Sqlite
        public void Load(SqliteConnection connection, SqliteConnection connection2)
        {
            InitializeDictionaries();
            LoadTodayQuestSteps(connection);
            LoadTodayQuestGroups(connection);
            LoadTodayQuestGroupQuests(connection);
            LoadTodayQuestGoals(connection);
        }

        private void InitializeDictionaries()
        {
            _todayQuestSteps = new ConcurrentDictionary<int, TodayQuestStep>();
            _todayQuestGroups = new ConcurrentDictionary<int, TodayQuestGroup>();
            _todayQuestGroupQuests = new ConcurrentDictionary<int, ConcurrentBag<TodayQuestGroupQuest>>();
            _todayQuestGoals = [];
        }

        private void LoadTodayQuestSteps(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM today_quest_steps";
            command.Prepare();
            using var sqliteReader = command.ExecuteReader();
            using var reader = new SQLiteWrapperReader(sqliteReader);
            while (reader.Read())
            {
                var step = new TodayQuestStep
                {
                    Id = reader.GetInt32("id"),
                    Description = reader.GetString("description"),
                    ExpeditionLevelMax = reader.GetInt32("expedition_level_max"),
                    ExpeditionLevelMin = reader.GetInt32("expedition_level_min"),
                    ExpeditionOnly = reader.GetBoolean("expedition_only"),
                    FamilyLevelMax = reader.GetInt32("family_level_max"),
                    FamilyLevelMin = reader.GetInt32("family_level_min"),
                    FamilyOnly = reader.GetBoolean("family_only"),
                    IconId = reader.GetInt32("icon_id"),
                    ItemNum = reader.GetInt32("item_num"),
                    ItemId = reader.GetInt32("item_id"),
                    Name = reader.GetString("name"),
                    OrUnitReqs = reader.GetBoolean("or_unit_reqs"),
                    RealStep = reader.GetInt32("real_step")
                };

                _todayQuestSteps[step.Id] = step;
            }
        }

        private void LoadTodayQuestGroups(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM today_quest_groups";
            command.Prepare();
            using var sqliteReader = command.ExecuteReader();
            using var reader = new SQLiteWrapperReader(sqliteReader);
            while (reader.Read())
            {
                var group = new TodayQuestGroup
                {
                    Id = reader.GetInt32("id"),
                    AutomaticRestart = reader.GetBoolean("automatic_restart"),
                    Description = reader.GetString("description"),
                    ExpeditionLevelMax = reader.GetInt32("expedition_level_max"),
                    ExpeditionLevelMin = reader.GetInt32("expedition_level_min"),
                    Name = reader.GetString("name"),
                    OrUnitReqs = reader.GetBoolean("or_unit_reqs"),
                    StepId = reader.GetInt32("step_id")
                };

                _todayQuestGroups[group.Id] = group;
            }
        }

        private void LoadTodayQuestGroupQuests(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM today_quest_group_quests";
            command.Prepare();
            using var sqliteReader = command.ExecuteReader();
            using var reader = new SQLiteWrapperReader(sqliteReader);
            while (reader.Read())
            {
                var groupQuest = new TodayQuestGroupQuest
                {
                    TodayQuestGroupId = reader.GetInt32("today_quest_group_id"),
                    QuestContextId = reader.GetInt32("quest_context_id")
                };

                if (!_todayQuestGroupQuests.ContainsKey(groupQuest.TodayQuestGroupId))
                {
                    _todayQuestGroupQuests[groupQuest.TodayQuestGroupId] = [];
                }

                _todayQuestGroupQuests[groupQuest.TodayQuestGroupId].Add(groupQuest);
            }
        }

        private void LoadTodayQuestGoals(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM today_quest_goals ORDER BY level";
            command.Prepare();
            using var sqliteReader = command.ExecuteReader();
            using var reader = new SQLiteWrapperReader(sqliteReader);
            while (reader.Read())
            {
                var goal = new TodayQuestGoal
                {
                    Id = reader.GetInt32("id"),
                    Comment = reader.GetString("comment"),
                    Level = reader.GetInt32("level")
                };
                _todayQuestGoals.Add(goal);
            }
        }

        public void PostLoad()
        {
        }
        #endregion Sqlite
    }
}
