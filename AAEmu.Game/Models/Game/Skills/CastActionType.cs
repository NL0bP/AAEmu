namespace AAEmu.Game.Models.Game.Skills
{
    public enum CastActionType : byte
    {
        Skill = 0,
        Plot = 1,
        Buff = 2,
        BuffTarget = 3,   // Passive
        DestroyTarget = 4 // Doodad
    }
}
