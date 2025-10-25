using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Skills;

public class HeirSkill
{
    public uint Id { get; set; }
    public uint SkillId { get; set; }
    public uint HeirSkillId { get; set; }
    public int SkillLevel { get; set; }
    public byte Ability { get; set; }
    public byte HighAbility { get; set; }
    public bool ActiveType { get; set; }
    
    public PacketStream WriteHeirSkill(PacketStream stream)
    {
        stream.Write(Id);          // type - heir_skill_id из таблицы heir_skill_details
        stream.Write(SkillId);     // type - skill_id  из таблицы heir_skills ищем по полю id == heir_skill_id
        stream.Write(HeirSkillId); // type - skill_id из таблицы heir_skill_details ищем по полю heir_skill_id
        stream.Write(SkillLevel);  // skillLevel
        stream.Write(Ability);     // ability
        stream.Write(HighAbility); // highAbility
        stream.Write(ActiveType);  // activeType

        return stream;
    }
}
