using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Game.World.Xml;
using AAEmu.Game.Models.Game.World.Zones;
using NLog;

namespace AAEmu.Game.Models.Game.World;

public class World
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public uint Id { get; set; } // iid - InstanceId
    public virtual string Name { get; set; }
    public float MaxHeight { get; set; }
    public virtual double HeightMaxCoefficient { get; set; }
    public float OceanLevel { get; set; } = 100f;
    public int CellX { get; set; }
    public int CellY { get; set; }
    public uint TemplateId { get; set; } // worldId
    /// <summary>
    /// Reference to the WorldTemplate this instance was created from (nikes: WorldInstance.Template)
    /// </summary>
    public WorldTemplate Template { get; set; }
    public WorldSpawnPosition SpawnPosition { get; set; } = new();
    public Region[,] Regions { get; set; } // TODO ... world - okay, instance - ....
    public virtual ushort[,] HeightMaps { get; set; }
    public List<uint> ZoneKeys { get; set; } = new();
    public ConcurrentDictionary<uint, XmlWorldZone> XmlWorldZones;
    public PhysicsManager Physics { get; set; }
    public WaterBodies Water { get; set; }
    public WorldEvents Events { get; set; } = new();
    public Dictionary<uint, List<Area>> SubZones { get; set; } // uint is zoneid 
    public Dictionary<uint, List<Area>> HousingZones { get; set; } // uint is zoneid

    public ShipStaticBarrierZones ShipStaticBarriers { get; set; }
    public object ShipStaticBarriersMutationLock { get; } = new();
    public HashSet<(int, int)> ShipBarrierBaiIngestedCells { get; } = new();
    public int ShipBarrierBaiNameSerial { get; set; }

    public World()
    {
        Events = new WorldEvents();
        SubZones = new Dictionary<uint, List<Area>>();
        HousingZones = new Dictionary<uint, List<Area>>();
    }

    #region Ship Static Barrier Script API

    /// <summary>Barrier count while holding the mutation lock (e.g. script pre/post ingest deltas).</summary>
    public int GetShipStaticBarrierCountLocked()
    {
        lock (ShipStaticBarriersMutationLock)
            return ShipStaticBarriers?.Barriers.Count ?? 0;
    }

    /// <summary>Clears BAI-ingested ship barriers and per-world ingest bookkeeping (GM reset).</summary>
    public void ClearShipStaticBarriersAndBaiIngest(out int clearedBarrierCount, out int clearedIngestedCellCount)
    {
        lock (ShipStaticBarriersMutationLock)
        {
            clearedBarrierCount = ShipStaticBarriers?.Barriers.Count ?? 0;
            clearedIngestedCellCount = ShipBarrierBaiIngestedCells.Count;
            if (ShipStaticBarriers is null)
                return;
            ShipStaticBarriers.Barriers.Clear();
            ShipStaticBarriers.SpatialGridBuiltForBarrierCount = -1;
            ShipStaticBarriers.SpatialGrid = null;
            ShipBarrierBaiIngestedCells.Clear();
            ShipBarrierBaiNameSerial = 0;
        }
    }

    /// <summary>Snapshot barrier and ingested-cell counts for status output.</summary>
    public void GetShipStaticBarrierDebugCounts(out int barrierCount, out int ingestedCellCount)
    {
        lock (ShipStaticBarriersMutationLock)
        {
            barrierCount = ShipStaticBarriers?.Barriers.Count ?? 0;
            ingestedCellCount = ShipBarrierBaiIngestedCells.Count;
        }
    }

    /// <summary>Lazily ingests <c>areasmission</c> polygons for one world cell into <see cref="ShipStaticBarriers"/>.</summary>
    public void EnsureShipStaticBarrierBaiCell(int cellX, int cellY) =>
        ShipStaticBarrierBaiIngestor.EnsureCell(this, cellX, cellY);

    #endregion

    #region Water reload stubs

    /// <summary>Clears ingested zones and rebuilds them from cells already loaded. Stub until full nikes water pipeline is ported.</summary>
    public void ReloadWaterFromLoadedCells()
    {
        // TODO: port full implementation from nikes WorldInstance.ReloadWaterFromLoadedCells
        Logger.Info($"ReloadWaterFromLoadedCells called on world {Id} — stub (no-op).");
    }

    #endregion

    /// <summary>Convenience: look up any GameObject by ObjId via WorldManager.</summary>
    public GameObject GetGameObject(uint objId) => WorldManager.Instance.GetGameObject(objId);

    /// <summary>Convenience: look up a BaseUnit by ObjId via WorldManager.</summary>
    public BaseUnit GetBaseUnit(uint objId) => WorldManager.Instance.GetBaseUnit(objId);

    /// <summary>Convenience: look up a Unit by ObjId via WorldManager.</summary>
    public Units.Unit GetUnit(uint objId) => WorldManager.Instance.GetUnit(objId);

    ~World()
    {
        Logger.Info($"World {Id} removed");
    }

    public bool IsWater(Vector3 position) => IsWater(position, out _);

    public bool IsWater(Vector3 point, out Vector3 flowDirection)
    {
        if (Water != null)
            return Water.IsWater(point, out flowDirection);

        flowDirection = Vector3.Zero;

        if (point.Z <= OceanLevel)
            return true;

        // TODO: Check shapes
        return false;
    }

    public float GetRawHeightMapHeight(int x, int y)
    {
        // This is the old GetHeight()
        var sx = x / 2;
        var sy = y / 2;
        return (float)(HeightMaps[sx, sy] / HeightMaxCoefficient);
    }

    public static float Lerp(float s, float e, float t)
    {
        return s + (e - s) * t;
    }

    private static float Blerp(float cX0Y0, float cX1Y0, float cX0Y1, float cX1Y1, float tx, float ty)
    {
        return Lerp(Lerp(cX0Y0, cX1Y0, tx), Lerp(cX0Y1, cX1Y1, tx), ty);
    }

    private static System.Drawing.Rectangle FindNearestSignificantPoints(int x, int y)
    {
        return new System.Drawing.Rectangle(x - (x % 2), y - (y % 2), 2, 2);
    }

    public float GetHeight(float x, float y)
    {
        // return GetRawHeightMapHeight((int)x, (int)y); // <-- the old way we used to do things

        // Get bordering points
        var border = FindNearestSignificantPoints((int)Math.Floor(x), (int)Math.Floor(y));

        // Get heights for these points
        var heightTL = GetRawHeightMapHeight(border.Left, border.Top);
        var heightTR = GetRawHeightMapHeight(border.Right, border.Top);
        var heightBL = GetRawHeightMapHeight(border.Left, border.Bottom);
        var heightBR = GetRawHeightMapHeight(border.Right, border.Bottom);
        var offX = (x - border.Left) / 2;
        var offY = (y - border.Top) / 2;
        var height = Blerp(heightTL, heightTR, heightBL, heightBR, offX, offY); // bilinear interpolation

        return height;
    }

    /// <summary>
    /// Get Sector at specific offset
    /// </summary>
    /// <param name="x">X offset of the Sector</param>
    /// <param name="y">Y offset of the Sector</param>
    /// <returns></returns>
    public Region GetRegion(int x, int y)
    {
        if (ValidRegion(x, y))
            if (Regions[x, y] == null)
                return Regions[x, y] = new Region(Id, x, y, 0);
            else
                return Regions[x, y];

        return null;
    }

    public bool ValidRegion(int x, int y)
    {
        return x >= 0 && x < CellX * WorldManager.SECTORS_PER_CELL && y >= 0 && y < CellY * WorldManager.SECTORS_PER_CELL;
    }
}
