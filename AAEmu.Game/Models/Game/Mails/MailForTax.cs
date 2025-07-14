using System;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Mails.Static;

namespace AAEmu.Game.Models.Game.Mails;

public class MailForTax : BaseMail
{
    /*
     * Working example for 1.2
     * /testmail 6 .houseTax title(25) "body('Test','1606565186','1607169986','1606565186','250000','50','3','0','500000','true','1')" 0 500000
     * 
     * Values for the extra flag look as following
     * Extra = ((long)zoneGroupId << 48) + ((long)extraUnknown << 32) + ((long)houseId);
     * 
     */

    private House _house;
    private static string TaxSenderName = ".houseTax";

    public MailForTax(House house)
    {
        _house = house;
        MailType = MailType.Billing;
        Header.Status = MailStatus.Unread;
        Body.SendDate = DateTime.UtcNow;
        Body.RecvDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Подготовка письма для обычных домов
    /// </summary>
    /// <param name="mail"></param>
    /// <param name="house"></param>
    /// <returns></returns>
    public static bool UpdateTaxInfo(BaseMail mail, House house)
    {
        // Check if owner is still valid
        var ownerName = NameManager.Instance.GetCharacterName(house.OwnerId);
        if (ownerName == null)
        {
            return false;
        }

        mail.Header.ReceiverId = house.OwnerId;
        mail.ReceiverName = ownerName;

        // Grab the zone the house is in
        var zone = ZoneManager.Instance.GetZoneByKey(house.Transform.ZoneId);
        if (zone == null)
        {
            return false;
        }

        // Set mail title
        mail.Title = "title(" + zone.GroupId + ")"; // Title calls a function to call zone group name

        // Get Tax info
        if (!HousingManager.Instance.CalculateBuildingTaxInfo(house.AccountId, house.Template, false, out var totalTaxAmountDue, out var heavyTaxHouseCount, out var normalTaxHouseCount, out var hostileTaxRate, out var oneWeekTaxCount))
        {
            return false;
        }

        // Note: I'm sure this can be done better, but it works and displays correctly
        var lateFees = 0;
        var paymentDeadLine = house.TaxDueDate;
        if (house.TaxDueDate <= DateTime.UtcNow)
        {
            lateFees = 1;
            paymentDeadLine = house.ProtectionEndDate;
        }

        // for 1.2
        // /testmail 6 .houseTax title(25) "body('Test','1606565186','1607169986','1606565186','250000','50','3','0','500000','true','1')" 0 500000
        // for 5.0.7.0
        // /testmail 6 .houseTax title(25) "body('Test','1726064262','1726669062','1726064262','150000','0','1','0','150000','true','0','0')" 0 500000 0
        mail.Body.Text = string.Format("body('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}')",
            house.Name.Replace("'", ""),                // House Name
            Helpers.UnixTime(house.TaxDueDate),         // Tax period start (this might need to be the same as tax due date)
            Helpers.UnixTime(house.ProtectionEndDate),  // Tax period end
            Helpers.UnixTime(paymentDeadLine),          // Tax Due Date
            house.Template.Taxation.Tax,                // This house base tax rate
            hostileTaxRate,                             // dominion tax rate (castle tax rate ?)
            heavyTaxHouseCount,                         // number of heavy tax houses
            lateFees,                                   // unpaid week count (listed as late fee)
            totalTaxAmountDue,                          // amount to Pay (as gold reference)
            house.Template.HeavyTax ? "true" : "false", // is this a heavy tax building
            normalTaxHouseCount,                        // number of tax-exempt houses
            0                                           // added in 5070
        );
        // In newer version this has an extra field at the end, which I assume is would be the hostile tax rate

        mail.Body.BillingAmount = totalTaxAmountDue;

        // Extra tag
        ushort extraUnknown = 0;
        mail.Header.Extra = ((long)zone.GroupId << 48) + ((long)extraUnknown << 32) + house.Id;

        return true;
    }

    /// <summary>
    /// Prepare mail
    /// </summary>
    /// <returns></returns>
    public bool FinalizeMail()
    {
        Header.SenderId = (uint)SystemMailSenderKind.None;
        Header.SenderName = TaxSenderName;

        return UpdateTaxInfo(this, _house);
    }

    /// <summary>
    /// Подготовка письма для новых домов
    /// </summary>
    /// <param name="mail"></param>
    /// <param name="house"></param>
    /// <returns></returns>
    public static bool UpdateTaxInfoNew(BaseMail mail, House house)
    {
        // Check if owner is still valid
        var ownerName = NameManager.Instance.GetCharacterName(house.OwnerId);
        if (ownerName == null)
        {
            return false;
        }

        mail.Header.ReceiverId = house.OwnerId;
        mail.ReceiverName = ownerName;

        // Grab the zone the house is in
        var zone = ZoneManager.Instance.GetZoneByKey(house.Transform.ZoneId);
        if (zone == null)
        {
            return false;
        }

        // Set mail title
        mail.Title = "title";

        // Get Tax info
        if (!HousingManager.Instance.CalculateBuildingTaxInfo(house.AccountId, house.Template, false, out var totalTaxAmountDue, out var heavyTaxHouseCount, out var normalTaxHouseCount, out var hostileTaxRate, out var oneWeekTaxCount))
        {
            return false;
        }

        // Note: I'm sure this can be done better, but it works and displays correctly
        var lateFees = 0;
        var paymentDeadLine = house.TaxDueDate;
        if (house.TaxDueDate <= DateTime.UtcNow)
        {
            lateFees = 1;
            paymentDeadLine = house.ProtectionEndDate;
        }

        // for 1.2
        // /testmail 6 .houseTax title(25) "body('Test','1606565186','1607169986','1606565186','250000','50','3','0','500000','true','1')" 0 500000
        // for 5.0.7.0
        // /testmail 6 .houseTax title(25) "body(6, 'Test','1726064262','1726669062','1726064262','150000','0','1','0','150000','true','0','0')" 0 500000 0
        mail.Body.Text = string.Format("body({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}, {12}')",
            mail.MailType,                              // тип письма = Billing
            house.Name,                                 // House Name
            Helpers.UnixTime(house.TaxDueDate),         // Tax period start (this might need to be the same as tax due date)
            Helpers.UnixTime(house.ProtectionEndDate),  // Tax period end
            Helpers.UnixTime(paymentDeadLine),          // Tax Due Date
            house.Template.Taxation.Tax,                // This house base tax rate
            hostileTaxRate,                             // dominion tax rate (castle tax rate ?)
            heavyTaxHouseCount,                         // number of heavy tax houses
            lateFees,                                   // unpaid week count (listed as late fee)
            totalTaxAmountDue,                          // amount to Pay (as gold reference)
            house.Template.HeavyTax ? "true" : "false", // is this a heavy tax building
            normalTaxHouseCount,                        // number of tax-exempt houses
            0                                           // added in 5070
        );
        // In newer version this has an extra field at the end, which I assume is would be the hostile tax rate

        mail.Body.BillingAmount = totalTaxAmountDue;

        // Extra tag
        ushort extraUnknown = 0;
        mail.Header.Extra = ((long)zone.GroupId << 48) + ((long)extraUnknown << 32) + house.Id;

        return true;
    }

    /// <summary>
    /// Prepare mail for new houses
    /// </summary>
    /// <returns></returns>
    public bool FinalizeMailNew()
    {
        Header.SenderId = (uint)SystemMailSenderKind.None;
        Header.SenderName = TaxSenderName;

        return UpdateTaxInfoNew(this, _house);
    }
}
