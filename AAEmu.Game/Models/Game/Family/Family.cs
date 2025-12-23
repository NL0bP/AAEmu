using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Family;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System;
using System.Linq;

public class Family : PacketMarshaler
{
    private List<uint> _removedMembers;

    public uint Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public string Content1 { get; set; }
    public string Content2 { get; set; }
    public int IncMemberCount { get; set; }
    public DateTime ResetTime { get; set; }
    public DateTime ChangeNameTime { get; set; }

    public List<FamilyMember> Members { get; }

    public Family()
    {
        _removedMembers = [];
        Level = 1;
        Name = "";
        Content1 = "";
        Content2 = "";
        Members = [];
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(Id); // family
        stream.Write(Members.Count); // TODO max length 8
        foreach (var member in Members)
        {
            member.Write(stream);
        }
        stream.Write(Name);           // name
        stream.Write(Level);          // level
        stream.Write(Exp);            // exp
        stream.Write(Content1);       // content1
        stream.Write(Content2);       // content2
        stream.Write(IncMemberCount); // incMemberCount
        stream.Write(ResetTime);      // resetTime
        stream.Write(ChangeNameTime); // changeNameTime

        return stream;
    }

    public void AddMember(FamilyMember member)
    {
        Members.Add(member);
    }

    public void RemoveMember(FamilyMember member)
    {
        if (member == null)
            return;
            
        Members.Remove(member);
        _removedMembers.Add(member.Id);
        member.Character?.ApplyFamilyEffects();
    }

    public void RemoveMember(Character character)
    {
        if (character == null)
            return;
            
        var member = GetMember(character);
        if (member != null)
        {
            RemoveMember(member);
            character.Family = 0;
        }
    }

    public FamilyMember GetMember(Character character)
    {
        return Members.FirstOrDefault(member => member.Id == character.Id);
    }

    public void SendPacket(GamePacket packet, uint exclude = 0)
    {
        foreach (var member in Members.Where(member => member.Id != exclude))
            member.Character?.SendPacket(packet);
    }

    public void Load(MySqlConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM families WHERE id=@id";
            command.Parameters.AddWithValue("id", Id);
            command.Prepare();
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                Id = reader.GetUInt32("id");
                Name = reader.GetString("name");
                Level = reader.GetInt32("level");
                Exp = reader.GetInt32("exp");
                Content1 = reader.GetString("content1");
                Content2 = reader.GetString("content2");
                IncMemberCount = reader.GetInt32("inc_member_count");
                ResetTime = reader.GetDateTime("reset_time");
                ChangeNameTime = reader.GetDateTime("change_name_time");
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM family_members WHERE family_id=@family_id";
            command.Parameters.AddWithValue("family_id", Id);
            command.Prepare();
            using var readerMembers = command.ExecuteReader();
            while (readerMembers.Read())
            {
                var member = new FamilyMember();
                member.Id = readerMembers.GetUInt32("character_id");
                member.Name = readerMembers.GetString("name");
                member.Role = readerMembers.GetByte("role");
                member.Title = readerMembers.GetString("title");
                member.Character = WorldManager.Instance.GetCharacterById(member.Id) ??
                                   WorldManager.Instance.GetOfflineCharacterInfo(member.Id);
                AddMember(member);
            }
        }
    }

    public void Save(MySqlConnection connection, MySqlTransaction transaction)
    {
        using (var command = connection.CreateCommand())
        {
            command.Connection = connection;
            command.Transaction = transaction;

            command.CommandText = "REPLACE INTO families(`id`, `name`, `level`, `exp`, `content1`, `content2`, `inc_member_count`, `reset_time`, `change_name_time`) " +
                                  "VALUES (@id, @name, @level, @exp, @content1, @content2, @inc_member_count, @reset_time, @change_name_time)";
            command.Parameters.AddWithValue("@id", Id);
            command.Parameters.AddWithValue("@name", Name);
            command.Parameters.AddWithValue("@level", Level);
            command.Parameters.AddWithValue("@exp", Exp);
            command.Parameters.AddWithValue("@content1", Content1);
            command.Parameters.AddWithValue("@content2", Content2);
            command.Parameters.AddWithValue("@inc_member_count", IncMemberCount);
            command.Parameters.AddWithValue("@reset_time", ResetTime);
            command.Parameters.AddWithValue("@change_name_time", ChangeNameTime);
            command.ExecuteNonQuery();
        }

        if (_removedMembers.Count > 0)
        {
            var removedMembers = string.Join(",", _removedMembers);

            using (var command = connection.CreateCommand())
            {
                command.Connection = connection;
                command.Transaction = transaction;

                command.CommandText = $"DELETE FROM family_members WHERE character_id IN ({removedMembers})";
                command.Parameters.AddWithValue("@family_id", Id);
                command.Prepare();
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.Connection = connection;
                command.Transaction = transaction;

                command.CommandText = $"UPDATE characters SET family = 0 WHERE `characters`.`id` IN ({removedMembers})";
                command.Parameters.AddWithValue("@family_id", Id);
                command.Prepare();
                command.ExecuteNonQuery();
            }

            _removedMembers.Clear();
        }

        using (var command = connection.CreateCommand())
        {
            command.Connection = connection;
            command.Transaction = transaction;
            foreach (var member in Members)
            {
                command.CommandText = "REPLACE INTO " +
                                      "family_members(`character_id`,`family_id`,`name`,`role`,`title`)" +
                                      " VALUES " +
                                      "(@character_id,@family_id,@name,@role,@title)";
                command.Parameters.AddWithValue("@character_id", member.Id);
                command.Parameters.AddWithValue("@family_id", Id);
                command.Parameters.AddWithValue("@name", member.Name);
                command.Parameters.AddWithValue("@role", member.Role);
                command.Parameters.AddWithValue("@title", member.Title);
                command.ExecuteNonQuery();
                command.Parameters.Clear();
            }
        }
    }
}
