using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AAEmu.Commons.Utils;
using NLog;

namespace AAEmu.Game.Core.Network.Connections;

public class GameConnectionTable : Singleton<GameConnectionTable>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private ConcurrentDictionary<ulong, GameConnection> _connections;

    private GameConnectionTable()
    {
        _connections = new ConcurrentDictionary<ulong, GameConnection>();
    }

    public void AddConnection(GameConnection con)
    {
        // Use AddOrUpdate to handle reconnection scenarios
        // This ensures new connections always replace old ones with the same ID
        var oldConnection = _connections.AddOrUpdate(con.Id, con, (key, existing) =>
        {
            Logger.Warn("Replacing existing connection with Id {0}. Old AccountId: {1}, New AccountId: {2}",
                key, existing.AccountId, con.AccountId);
            return con;
        });
        Logger.Debug("AddConnection: Id={0}, Total connections: {1}", con.Id, _connections.Count);
    }

    public GameConnection GetConnection(ulong id)
    {
        _connections.TryGetValue(id, out var con);
        return con;
    }

    public GameConnection RemoveConnection(ulong id)
    {
        var result = _connections.TryRemove(id, out var con);
        if (result)
        {
            Logger.Debug("Successfully removed connection with Id {0}. Remaining connections: {1}", id, _connections.Count);
        }
        else
        {
            Logger.Warn("Failed to remove connection with Id {0} - key not found!", id);
        }
        return con;
    }

    public List<GameConnection> GetConnections()
    {
        return new List<GameConnection>(_connections.Values);
    }

    public GameConnection GetConnectionByAccount(ulong accountId)
    {
        var connectionInfo = _connections.Where(c => c.Value.AccountId == accountId).ToList();
        if (connectionInfo.Count >= 1)
            return connectionInfo[0].Value;
        return null;
    }
}
