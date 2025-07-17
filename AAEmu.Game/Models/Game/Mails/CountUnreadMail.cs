using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Mails;

public class CountUnreadMail : PacketMarshaler
{
    public int TotalSent { get; set; }
    public int TotalReceived { get; set; }
    public int TotalMiaReceived { get; set; }
    public int TotalCommercialReceived { get; set; }
    public int UnreadSent { get; set; }
    public int UnreadReceived { get; set; }
    public int UnreadMiaReceived { get; set; }
    public int UnreadCommercialReceived { get; set; }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(TotalSent);
        stream.Write(TotalReceived);
        stream.Write(TotalMiaReceived);
        stream.Write(TotalCommercialReceived);
        stream.Write(UnreadSent);
        stream.Write(UnreadReceived);
        stream.Write(UnreadMiaReceived);
        stream.Write(UnreadCommercialReceived);

        return stream;
    }

    public void ResetReceived()
    {
        TotalReceived = 0;
        TotalMiaReceived = 0;
        TotalCommercialReceived = 0;
        UnreadReceived = 0;
        UnreadMiaReceived = 0;
        UnreadCommercialReceived = 0;
    }

    public void UpdateReceived(MailType mailType, int amount)
    {
        if (mailType is MailType.Charged or MailType.Promotion)
        {
            TotalCommercialReceived += amount;
            if (TotalCommercialReceived <= 0)
            {
                TotalCommercialReceived = 0;
            }
        }
        else
        if (mailType == MailType.MiaRecv)
        {
            TotalMiaReceived += amount;
            if (TotalMiaReceived <= 0)
            {
                TotalMiaReceived = 0;
            }
        }
        else
        {
            TotalReceived += amount;
            if (TotalReceived <= 0)
            {
                TotalReceived = 0;
            }
        }

        Logger.Debug($"UpdateReceived: TotalCommercialReceived={TotalCommercialReceived}, TotalMiaReceived={TotalMiaReceived}, TotalReceived={TotalReceived}");
    }

    public void UpdateUnreadReceived(MailType mailType, int amount)
    {
        if (mailType is MailType.Charged or MailType.Promotion)
        {
            UnreadCommercialReceived += amount;
            if (UnreadCommercialReceived <= 0)
            {
                UnreadCommercialReceived = 0;
            }
        }
        else
        if (mailType == MailType.MiaRecv)
        {
            UnreadMiaReceived += amount;
            if (UnreadMiaReceived <= 0)
            {
                UnreadMiaReceived = 0;
            }
        }
        else
        {
            UnreadReceived += amount;
            if (UnreadReceived <= 0)
            {
                UnreadReceived = 0;
            }
        }
        Logger.Debug($"UpdateUnreadReceived: UnreadCommercialReceived={UnreadCommercialReceived}, UnreadMiaReceived={UnreadMiaReceived}, UnreadReceived={UnreadReceived}");
    }

    public void UpdateSend(int amount)
    {
        TotalSent += amount;
        if (TotalSent <= 0)
        {
            TotalSent = 0;
        }

        Logger.Debug($"UpdateSend: TotalSent={TotalSent}");
    }
    public void UpdateSend2(int amount)
    {
        TotalSent = amount;
        if (TotalSent <= 0)
        {
            TotalSent = 0;
        }

        Logger.Debug($"UpdateSend: TotalSent={TotalSent}");
    }
}
