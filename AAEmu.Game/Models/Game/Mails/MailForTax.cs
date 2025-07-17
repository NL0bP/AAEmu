using System;
using System.Text;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Mails.Static;

namespace AAEmu.Game.Models.Game.Mails;

public sealed class MailForTax : BaseMail
{
    private const string TaxSenderName = ".houseTax";

    private readonly House _house;

    public MailForTax(House house)
    {
        _house = house ?? throw new ArgumentNullException(nameof(house));

        MailType = MailType.Billing;
        Header.Status = MailStatus.Unread;
        Body.SendDate = DateTime.UtcNow;
        Body.RecvDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Fills mail fields and returns success.
    /// </summary>
    public bool FinalizeMail()
    {
        Header.SenderId = (uint)SystemMailSenderKind.None;
        Header.SenderName = TaxSenderName;

        if (!BuildBody())
            return false;

        return true;
    }

    /// <summary>
    /// Updates an existing mail with actual tax data.
    /// </summary>
    public static bool UpdateTaxInfo(BaseMail mail, House house)
    {
        if (house is null) return false;
        return BuildBody(mail, house);
    }

    /* ---------- приватные ---------- */

    private bool BuildBody() => BuildBody(this, _house);

    private static bool BuildBody(BaseMail mail, House house)
    {
        var ownerName = NameManager.Instance.GetCharacterName(house.OwnerId);
        if (ownerName is null) return false;

        var zone = ZoneManager.Instance.GetZoneByKey(house.Transform.ZoneId);
        if (zone is null) return false;

        HousingManager.Instance.CalculateBuildingTaxInfo(
            house.AccountId,
            house.Template,
            buildingNewHouse: false,
            out var totalDue,
            out var heavyCount,
            out var normalCount,
            out var hostileRate,
            out _
        );

        var (lateFees, deadline) = house.TaxDueDate <= DateTime.UtcNow
            ? (1, house.ProtectionEndDate)
            : (0, house.TaxDueDate);

        if (lateFees == 1)
        {
            house.IsDirty = true;
            house.IsAlreadyPaid = false;
        }

        var zoneUnix = Helpers.UnixTime(house.TaxDueDate);
        var protUnix = Helpers.UnixTime(house.ProtectionEndDate);
        var payUnix = Helpers.UnixTime(deadline);

        // body('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}')
        mail.Body.Text = new StringBuilder("body('")
            .Append(Escape(house.Name)).Append("','")
            .Append(zoneUnix).Append("','")
            .Append(protUnix).Append("','")
            .Append(payUnix).Append("','")
            .Append(house.Template.Taxation?.Tax ?? 0).Append("','")
            .Append(hostileRate).Append("','")
            .Append(heavyCount).Append("','")
            .Append(lateFees).Append("','")
            .Append(totalDue).Append("','")
            .Append(house.Template.HeavyTax ? "true" : "false").Append("','")
            .Append(normalCount).Append("','")
            .Append("0')").ToString();

        mail.Body.BillingAmount = totalDue;
        mail.Title = $"title({zone.GroupId})";
        mail.ReceiverName = ownerName;
        mail.Header.ReceiverId = house.OwnerId;

        mail.Header.Extra = ((long)zone.GroupId << 48) | house.Id;
        return true;
    }

    private static string Escape(string value) =>
        value.Replace("'", "''");
}
