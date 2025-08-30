using System.Collections.Generic;
using System.Numerics;

namespace AAEmu.Game.Models.Game.World;

// lighthouse ezi's
public static class  WorldConstants
{
    public const int DelayInMilliseconds = 1000; // 1 секунда
    public const int ProximityCheckDelayMs = 1000; // 1 second
    public const double DistanceThresholdSquared = 500.0 * 500.0; // 500 meters squared
    public const float CargoShipProximityThreshold = 35f; // 35 метров

    public static readonly HashSet<Vector3> TargetPositions = new()
    {
        new Vector3(16672.8f, 9303.9f, 175f),   // Dawn Peninsula, Eastern Continent
        new Vector3(13131f, 10105.2f, 175.2f),  // Two Crowns, Western Continent
        new Vector3(18962.6f, 26838f, 176.3f),  // Glittering Coast, Original Continent
        new Vector3(19971f, 26927.2f, 175.8f),  // Glittering Coast, Original Continent
        new Vector3(15227.6f, 22731f, 185.8f)
    };
}
