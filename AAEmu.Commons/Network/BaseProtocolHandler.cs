using AAEmu.Commons.Network.Core;

namespace AAEmu.Commons.Network
{
    public abstract class BaseProtocolHandler
    {
        // Server-side session methods
        public virtual void OnConnect(Session session)
        {
        }

        public virtual void OnReceive(Session session, byte[] buf, int bytes)
        {
        }

        public virtual void OnSend(Session session, byte[] buf, int offset, int bytes)
        {
        }

        public virtual void OnDisconnect(Session session)
        {
        }

        // Client-side session methods
        public virtual void OnConnect(ClientSession session)
        {
            // Default: delegate to server-side method for compatibility
            OnConnect((Session)null);
        }

        public virtual void OnReceive(ClientSession session, byte[] buf, int bytes)
        {
            // Default: delegate to server-side method for compatibility
            OnReceive((Session)null, buf, bytes);
        }

        public virtual void OnSend(ClientSession session, byte[] buf, int offset, int bytes)
        {
            // Default: delegate to server-side method for compatibility
            OnSend((Session)null, buf, offset, bytes);
        }

        public virtual void OnDisconnect(ClientSession session)
        {
            // Default: delegate to server-side method for compatibility
            OnDisconnect((Session)null);
        }
    }
}
