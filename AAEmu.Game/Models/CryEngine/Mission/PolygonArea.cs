using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AAEmu.Game.Models.CryEngine.Mission;

public class PolygonArea(uint zoneId) : Mission(zoneId)
{
    public List<Vector3> Points { get; set; } = [];
    public NavigationType NavigationType { get; set; } = NavigationType.Unset;
    public int Type { get; set; }
    public double Height { get; set; }
    public AiLightLevel AiLightLevel { get; set; } = AiLightLevel.None;

    public override bool Equals(Mission other)
    {
        if (this == other)
            return true;

        if (other is not PolygonArea poly)
            return false;

        return ZoneId.Equals(poly.ZoneId) &&
               Name.Equals(poly.Name) &&
               Points.SequenceEqual(poly.Points) &&
               NavigationType.Equals(poly.NavigationType) &&
               Type.Equals(poly.Type) &&
               Height.Equals(poly.Height) &&
               AiLightLevel.Equals(poly.NavigationType);
    }
}
