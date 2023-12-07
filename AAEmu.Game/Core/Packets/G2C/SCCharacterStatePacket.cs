﻿using System;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCCharacterStatePacket : GamePacket
    {
        private readonly Character _character;

        public SCCharacterStatePacket(Character character) : base(SCOffsets.SCCharacterStatePacket, 1)
        {
            _character = character;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_character.InstanceId); // instanceId
            stream.Write(_character.Guid);       // string[16] guid
            stream.Write("");       // mn[63]
            stream.Write(0);        // mf

            _character.Write(stream);

            //stream.Write(new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xDB, 0xFB, 0x17, 0xC0}); //angles
            stream.Write(0f);
            stream.Write(0f);
            stream.Write(0f);

            stream.Write(_character.Expirience);
            stream.Write(_character.RecoverableExp);
            //stream.Write(0u);      // penaltiedExp
            stream.Write(0);       // returnDistrictId
            stream.Write((uint)0); // returnDistrict -> type(id)
            stream.Write((uint)0); // resurrectionDistrict -> type(id)

            foreach (var ability in _character.Abilities.Values)
            {
                stream.Write(ability.Exp); // uint abilityExp
            }

            for (var i = 0; i < 4; i++)
            {
                stream.Write((short)0); // talent
            }

            stream.Write(_character.Mails.unreadMailCount.Received);           // unreadMail
            stream.Write(_character.Mails.unreadMailCount.MiaReceived);        // unreadMiaMail
            //stream.Write(_character.Mails.unreadMailCount.CommercialReceived); // unreadCommercialMail
            stream.Write(_character.NumInventorySlots);                        // numInvenSlots
            stream.Write(_character.NumBankSlots);                             // numBankSlots
            stream.Write(_character.Money);                                    // moneyAmount - Inventory
            stream.Write(_character.Money2);                                   // moneyAmount - Bank

            stream.Write(0); // juryPoint
            //stream.Write(0); // jailSeconds

            //stream.Write(0L);                // bountyMoney
            //stream.Write(DateTime.MinValue); // bountyTime

            //stream.Write(0); // reportedNo
            //stream.Write(0); // suspectedNo

            return stream;
        }
    }
}
