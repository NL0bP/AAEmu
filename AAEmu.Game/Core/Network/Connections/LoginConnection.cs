using System.Net;
using AAEmu.Commons.Network;
using AAEmu.Commons.Network.Core;
using AAEmu.Game.Core.Network.Login;
using AAEmu.Game.Core.Packets.G2L;
using AAEmu.Game.Models;
using NLog;

namespace AAEmu.Game.Core.Network.Connections
{
    public class LoginConnection
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
        private ClientSession _clientSession;

        public uint Id => _clientSession.SessionId;
        public IPAddress Ip => _clientSession.Ip;

        public bool Block { get; set; }
        public PacketStream LastPacket { get; set; }       


        public LoginConnection(ClientSession clientSession)
        {
            _clientSession = clientSession;
        }

        public void OnConnect()
        {
            var secretKey = AppConfiguration.Instance.SecretKey;
            var gsId = AppConfiguration.Instance.Id;
            var additionalesGsId = AppConfiguration.Instance.AdditionalesId;
            SendPacket(new GLRegisterGameServerPacket(secretKey, gsId, additionalesGsId));
        }

        public void SendPacket(LoginPacket packet)
        {
            if (Block)
                return;
            packet.Connection = this;
            byte[] buf = packet.Encode();
            _clientSession.SendPacket(buf);
        }

        public void Close()
        {
            _clientSession.Close();
        }
    }
}
