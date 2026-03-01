using System;
using System.Net;
using NetCoreServer;

namespace AAEmu.Commons.Network.Core
{
    /// <summary>
    /// Client session wrapper that doesn't inherit from TcpSession
    /// to avoid the requirement of having a TcpServer in NetCoreServer 8.x
    /// </summary>
    public class ClientSession
    {
        public BaseProtocolHandler ProtocolHandler { get; private set; }
        public IPEndPoint RemoteEndPoint { get; set; }
        public uint SessionId { get; set; }
        public IPAddress Ip { get; private set; }
        public Client Client { get; set; }

        public ClientSession(Client client, BaseProtocolHandler handler)
        {
            Client = client;
            ProtocolHandler = handler;
            if (client?.Endpoint != null)
            {
                var endpoint = (IPEndPoint)client.Endpoint;
                Ip = endpoint.Address;
                SessionId = (uint)endpoint.GetHashCode();
            }
        }

        public void OnConnected()
        {
            if (Client?.Socket?.RemoteEndPoint is IPEndPoint remoteEp)
            {
                RemoteEndPoint = remoteEp;
                SessionId = (uint)remoteEp.GetHashCode();
                Ip = remoteEp.Address;
            }
            ProtocolHandler?.OnConnect(this);
        }

        public void OnDisconnected()
        {
            ProtocolHandler?.OnDisconnect(this);
        }

        public void OnReceived(byte[] buffer, int size)
        {
            ProtocolHandler?.OnReceive(this, buffer, size);
        }

        public virtual void SendMessage(PacketStream message)
        {
            Client?.SendAsync(message);
        }

        public bool SendPacket(byte[] packet)
        {
            return Client?.SendAsync(packet) ?? false;
        }

        public void Close()
        {
            Client?.Disconnect();
        }
    }

    public class Client : NetCoreServer.TcpClient
    {
        private BaseProtocolHandler _handler;
        private ClientSession _session;
        
        public Client(IPAddress address, int port, BaseProtocolHandler handler) : base(address, port)
        {
            _handler = handler;
        }

        public BaseProtocolHandler GetHandler()
        {
            return _handler;
        }

        protected override void OnConnected()
        {
            _session = new ClientSession(this, _handler);
            _session.OnConnected();
        }

        protected override void OnDisconnected()
        {
            _session?.OnDisconnected();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _session?.OnReceived(buffer, (int)size);
        }

        protected override void OnSent(long sent, long pending)
        {
            base.OnSent(sent, pending);
        }
    }
}
