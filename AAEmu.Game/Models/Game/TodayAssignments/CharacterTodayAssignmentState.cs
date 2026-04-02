using System;

using AAEmu.Game.Models.Game.Quests.Static;

namespace AAEmu.Game.Models.Game.TodayAssignments;

public class CharacterTodayAssignmentState
{
    public int RealStep { get; set; }
    public int StepId { get; set; }
    public int GroupId { get; set; }
    public int QuestContextId { get; set; }
    public long QuestId { get; set; }
    public byte[] QuestData { get; set; }
    public QuestStatus QuestStatus { get; set; }
    public TodayAssignmentData Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool NeedsClientQuestStartSync { get; set; }
}
