﻿namespace AAEmu.Game.Models.Game.Skills.Plots
{
    public class PlotEventCondition
    {
        public PlotCondition Condition { get; set; }
        public int Position { get; set; }
        public int SourceId { get; set; }
        public int TargetId { get; set; }
        public bool NotifyFailure { get; set; }
    }
}
