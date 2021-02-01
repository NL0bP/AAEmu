namespace AAEmu.Game.Models.Game.AI.v2.Params
{
    public class AiSkill
    {
        public uint SkillId { get; set; }
        public uint SkillType { get; set; }
        public bool Strafe { get; set; }
        public float Delay { get; set; }
        
        public uint HealthRangeMin { get; set; }
        public uint HealthRangeMax { get; set; }
    }
}
