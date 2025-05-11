using System;

using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Attendance;

public class AttendanceRecord
{
    public DateTime AccountAttendance { get; set; }
    public bool Accept { get; set; }

    public void MarkAsAttended(DateTime date)
    {
        AccountAttendance = date;
        Accept = true;
    }

    public void Write(PacketStream stream)
    {
        stream.Write(AccountAttendance);
        stream.Write(Accept); // add in 5+
    }
}
