using System;
using System.Collections.Generic;
using System.Numerics;

using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Physics;
using AAEmu.Game.Utils;

using Xunit;

namespace AAEmu.UnitTests.Game.Physics;

public class ShipPhysicsTests
{
    // ----------------------------------------------------------------
    // 1. MathUtil.HalfPi
    // ----------------------------------------------------------------

    [Fact]
    public void MathUtil_HalfPi_Equals_PiOverTwo()
    {
        Assert.Equal(MathF.PI / 2f, MathUtil.HalfPi);
    }

    // ----------------------------------------------------------------
    // 2. ReplicationSmoothing
    // ----------------------------------------------------------------

    [Fact]
    public void ReplicationSmoothing_InitialState_HasExpectedDefaults()
    {
        var rs = new ReplicationSmoothing();

        Assert.False(rs.Seeded);
        Assert.Equal(0f, rs.PosX);
        Assert.Equal(0f, rs.PosY);
        Assert.Equal(0f, rs.PosZ);
        Assert.Equal(0f, rs.VelPx);
        Assert.Equal(0f, rs.VelPy);
        Assert.Equal(0f, rs.VelPz);
        Assert.Equal(0f, rs.BankSmoothed);
        Assert.Equal(0f, rs.GroundPitchSmoothed);
        Assert.Equal((byte)0, rs.ContactHoldTicks);
    }

    [Fact]
    public void ReplicationSmoothing_Reset_ClearsSeededAndContactHoldTicks()
    {
        var rs = new ReplicationSmoothing
        {
            Seeded = true,
            PosX = 10f,
            PosY = 20f,
            PosZ = 30f,
            ContactHoldTicks = 12
        };

        rs.Reset();

        Assert.False(rs.Seeded);
        Assert.Equal((byte)0, rs.ContactHoldTicks);
        // Position values are intentionally NOT cleared by Reset
        Assert.Equal(10f, rs.PosX);
    }

    // ----------------------------------------------------------------
    // 3. NumericExtensions ToCellIndex / ToPathsIndex
    // ----------------------------------------------------------------

    [Fact]
    public void ToCellIndex_KnownCoordinates_ReturnsExpectedCell()
    {
        var pos = new Vector3(2048f, 2048f, 0f);
        var (cx, cy) = pos.ToCellIndex();

        Assert.Equal(2, cx);
        Assert.Equal(2, cy);
    }

    [Fact]
    public void ToCellIndex_Origin_ReturnsZeroZero()
    {
        var pos = new Vector3(0f, 0f, 0f);
        var (cx, cy) = pos.ToCellIndex();

        Assert.Equal(0, cx);
        Assert.Equal(0, cy);
    }

    [Fact]
    public void ToCellIndex_NegativeCoordinates_ReturnsNegativeIndices()
    {
        // Math.Floor(-1 / 1024) = -1
        var pos = new Vector3(-1f, -1f, 0f);
        var (cx, cy) = pos.ToCellIndex();

        Assert.Equal(-1, cx);
        Assert.Equal(-1, cy);
    }

    [Fact]
    public void ToCellIndex_JustBelowBoundary_StaysInLowerCell()
    {
        var pos = new Vector3(1023.9f, 1023.9f, 0f);
        var (cx, cy) = pos.ToCellIndex();

        Assert.Equal(0, cx);
        Assert.Equal(0, cy);
    }

    [Fact]
    public void ToCellIndex_ExactBoundary_AdvancesToNextCell()
    {
        var pos = new Vector3(1024f, 1024f, 0f);
        var (cx, cy) = pos.ToCellIndex();

        Assert.Equal(1, cx);
        Assert.Equal(1, cy);
    }

    [Fact]
    public void ToPathsIndex_KnownCoordinates_ReturnsExpectedIndex()
    {
        var pos = new Vector3(2048f, 2048f, 0f);
        var (px, py) = pos.ToPathsIndex();

        Assert.Equal(8, px); // 2048 / 256 = 8
        Assert.Equal(8, py);
    }

    [Fact]
    public void ToPathsIndex_Origin_ReturnsZeroZero()
    {
        var pos = new Vector3(0f, 0f, 0f);
        var (px, py) = pos.ToPathsIndex();

        Assert.Equal(0, px);
        Assert.Equal(0, py);
    }

    [Fact]
    public void ToPathsIndex_NegativeCoordinates_ReturnsNegativeIndices()
    {
        var pos = new Vector3(-1f, -1f, 0f);
        var (px, py) = pos.ToPathsIndex();

        Assert.Equal(-1, px);
        Assert.Equal(-1, py);
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(255.9f, 255.9f, 0, 0)]
    [InlineData(256f, 256f, 1, 1)]
    [InlineData(512f, 768f, 2, 3)]
    public void ToPathsIndex_VariousPositions_MatchesExpected(float x, float y, int expectedPx, int expectedPy)
    {
        var pos = new Vector3(x, y, 0f);
        var (px, py) = pos.ToPathsIndex();

        Assert.Equal(expectedPx, px);
        Assert.Equal(expectedPy, py);
    }

    // ----------------------------------------------------------------
    // 4. ShipStaticBarrierSpatialGrid
    // ----------------------------------------------------------------

    [Fact]
    public void SpatialGrid_CellSizeMeters_Is128()
    {
        Assert.Equal(128f, ShipStaticBarrierSpatialGrid.CellSizeMeters);
    }

    [Fact]
    public void SpatialGrid_QueryOnEmptyGrid_ReturnsNoResults()
    {
        var grid = new ShipStaticBarrierSpatialGrid();
        grid.Rebuild(new List<ShipStaticBarrier>());

        var outList = new List<(ShipStaticBarrier, int)>();
        var dedupe = new HashSet<(ShipStaticBarrier, int)>();
        grid.Query(500f, 500f, 50f, outList, dedupe);

        Assert.Empty(outList);
    }

    [Fact]
    public void SpatialGrid_RebuildAndQuery_FindsBarrierNearSegment()
    {
        // Create a barrier via TryCreate
        var dto = new ShipStaticBarrierEntryDto
        {
            Name = "test_wall",
            ZoneKey = 1,
            ZMin = 0f,
            ZMax = 100f,
            HalfThicknessMeters = 2f,
            Enabled = true,
            PointsXY = new List<List<double>>
            {
                new() { 500.0, 500.0 },
                new() { 600.0, 500.0 }
            }
        };

        Assert.True(ShipStaticBarrier.TryCreate(dto, out var barrier, out _));

        var grid = new ShipStaticBarrierSpatialGrid();
        grid.Rebuild(new List<ShipStaticBarrier> { barrier });

        var outList = new List<(ShipStaticBarrier, int)>();
        var dedupe = new HashSet<(ShipStaticBarrier, int)>();

        // Query near the segment
        grid.Query(550f, 500f, 50f, outList, dedupe);
        Assert.NotEmpty(outList);
        Assert.Equal(barrier, outList[0].Item1);
        Assert.Equal(0, outList[0].Item2);
    }

    [Fact]
    public void SpatialGrid_Query_FarFromBarrier_ReturnsEmpty()
    {
        var dto = new ShipStaticBarrierEntryDto
        {
            Name = "far_wall",
            ZoneKey = 1,
            ZMin = 0f,
            ZMax = 100f,
            HalfThicknessMeters = 2f,
            Enabled = true,
            PointsXY = new List<List<double>>
            {
                new() { 500.0, 500.0 },
                new() { 600.0, 500.0 }
            }
        };

        Assert.True(ShipStaticBarrier.TryCreate(dto, out var barrier, out _));

        var grid = new ShipStaticBarrierSpatialGrid();
        grid.Rebuild(new List<ShipStaticBarrier> { barrier });

        var outList = new List<(ShipStaticBarrier, int)>();
        var dedupe = new HashSet<(ShipStaticBarrier, int)>();

        // Query very far from the segment (well beyond SegmentInsertExpandMeters)
        grid.Query(50000f, 50000f, 10f, outList, dedupe);
        Assert.Empty(outList);
    }

    [Fact]
    public void SpatialGrid_DisabledBarrier_NotInserted()
    {
        var dto = new ShipStaticBarrierEntryDto
        {
            Name = "disabled_wall",
            ZoneKey = 1,
            ZMin = 0f,
            ZMax = 100f,
            Enabled = false,
            PointsXY = new List<List<double>>
            {
                new() { 500.0, 500.0 },
                new() { 600.0, 500.0 }
            }
        };

        Assert.True(ShipStaticBarrier.TryCreate(dto, out var barrier, out _));

        var grid = new ShipStaticBarrierSpatialGrid();
        grid.Rebuild(new List<ShipStaticBarrier> { barrier });

        var outList = new List<(ShipStaticBarrier, int)>();
        var dedupe = new HashSet<(ShipStaticBarrier, int)>();
        grid.Query(550f, 500f, 50f, outList, dedupe);

        Assert.Empty(outList);
    }

    // ----------------------------------------------------------------
    // 5. ShipStaticObstacleContact constants
    // ----------------------------------------------------------------

    [Fact]
    public void ShipStaticObstacleContact_DefaultReplicationContactHoldTicks_Is8()
    {
        Assert.Equal((byte)8, ShipStaticObstacleContact.DefaultReplicationContactHoldTicks);
    }

    [Fact]
    public void ShipStaticObstacleContact_BridgeCeilingReplicationContactHoldTicks_Is24()
    {
        Assert.Equal((byte)24, ShipStaticObstacleContact.BridgeCeilingReplicationContactHoldTicks);
    }

    [Fact]
    public void ShipStaticObstacleContact_MinPenetrationForDamage_IsPositive()
    {
        Assert.True(ShipStaticObstacleContact.MinPenetrationMetersForEnvHullDamage > 0f);
        Assert.Equal(0.015f, ShipStaticObstacleContact.MinPenetrationMetersForEnvHullDamage);
    }

    // ----------------------------------------------------------------
    // 6. OneShotVelocityKick — tested via constants/structure only
    //    (requires a Jitter2 World instance which is heavy; verify type exists)
    // ----------------------------------------------------------------

    [Fact]
    public void OneShotVelocityKick_IsSubclassOfForceGenerator()
    {
        Assert.True(typeof(AAEmu.Game.Physics.Forces.OneShotVelocityKick)
            .IsSubclassOf(typeof(AAEmu.Game.Physics.Forces.ForceGenerator)));
    }

    // ----------------------------------------------------------------
    // 7. Slave property defaults
    // ----------------------------------------------------------------

    [Fact]
    public void Slave_TurnSpeedVelocityMul_DefaultsToOne()
    {
        var slave = new Slave();
        Assert.Equal(1f, slave.TurnSpeedVelocityMul);
    }

    [Fact]
    public void Slave_LastMoveDirSign_DefaultsToPositiveOne()
    {
        var slave = new Slave();
        Assert.Equal((sbyte)1, slave.LastMoveDirSign);
    }

    [Fact]
    public void Slave_BankAngle_DefaultsToZero()
    {
        var slave = new Slave();
        Assert.Equal(0f, slave.BankAngle);
    }

    [Fact]
    public void Slave_ShipHullCollisionDamageCooldown_IsEmptyDictionary()
    {
        var slave = new Slave();
        Assert.NotNull(slave.ShipHullCollisionDamageCooldownByOtherShipId);
        Assert.Empty(slave.ShipHullCollisionDamageCooldownByOtherShipId);
    }

    [Fact]
    public void Slave_GroundPitchAngle_DefaultsToZero()
    {
        var slave = new Slave();
        Assert.Equal(0f, slave.GroundPitchAngle);
    }

    [Fact]
    public void Slave_HarpoonRope_DefaultsToDisengaged()
    {
        var slave = new Slave();
        Assert.False(slave.HarpoonRope.IsEngaged);
    }

    // ----------------------------------------------------------------
    // 8. WindModelType enum
    // ----------------------------------------------------------------

    [Fact]
    public void WindModelType_HasOfficialValue()
    {
        Assert.True(Enum.IsDefined(typeof(WorldConfig.WindModelType), WorldConfig.WindModelType.Official));
    }

    [Fact]
    public void WindModelType_HasRealisticValue()
    {
        Assert.True(Enum.IsDefined(typeof(WorldConfig.WindModelType), WorldConfig.WindModelType.Realistic));
    }

    [Fact]
    public void WindModelType_OfficialIsZero()
    {
        Assert.Equal(0, (int)WorldConfig.WindModelType.Official);
    }

    [Fact]
    public void WindModelType_RealisticIsOne()
    {
        Assert.Equal(1, (int)WorldConfig.WindModelType.Realistic);
    }

    // ----------------------------------------------------------------
    // 9. NumericExtensions DegToRad / RadToDeg round-trip
    //    (indirect test of the math formulas used by ComputeVisualMaxBankDeg helpers)
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(0f)]
    [InlineData(45f)]
    [InlineData(90f)]
    [InlineData(180f)]
    [InlineData(360f)]
    public void DegToRad_RadToDeg_RoundTrip_Float(float degrees)
    {
        var radians = degrees.DegToRad();
        var backToDeg = radians.RadToDeg();

        Assert.Equal(degrees, backToDeg, precision: 3);
    }

    [Fact]
    public void DegToRad_90Degrees_EqualsHalfPi()
    {
        var rad = 90f.DegToRad();
        Assert.Equal(MathUtil.HalfPi, rad, precision: 5);
    }

    // ----------------------------------------------------------------
    // 10. ShipHarpoonRopeState
    // ----------------------------------------------------------------

    [Fact]
    public void ShipHarpoonRopeState_Default_IsNotEngaged()
    {
        var state = new ShipHarpoonRopeState();
        Assert.False(state.IsEngaged);
    }

    [Fact]
    public void ShipHarpoonRopeState_SetEngaged_IsEngagedReturnsTrue()
    {
        var state = new ShipHarpoonRopeState { IsEngaged = true };
        Assert.True(state.IsEngaged);
    }

    [Fact]
    public void ShipHarpoonRopeState_Default_HasZeroRopeLength()
    {
        var state = new ShipHarpoonRopeState();
        Assert.Equal(0f, state.RopeLength);
    }

    [Fact]
    public void ShipHarpoonRopeState_Default_HookWorldIsZero()
    {
        var state = new ShipHarpoonRopeState();
        Assert.Equal(Vector3.Zero, state.HookWorld);
    }

    [Fact]
    public void ShipHarpoonRopeState_Default_ControllerExpireIsNull()
    {
        var state = new ShipHarpoonRopeState();
        Assert.Null(state.ControllerExpireAtUtc);
    }

    [Fact]
    public void ShipHarpoonRopeState_Clear_ResetsAllFields()
    {
        var state = new ShipHarpoonRopeState
        {
            IsEngaged = true,
            HookWorld = new Vector3(100f, 200f, 300f),
            HookBasisObjId = 42,
            HookLocalInBasis = new Vector3(1f, 2f, 3f),
            RopeLength = 50f,
            LastTeared = true,
            LastCutout = true,
            MaxLaunchRange = 100f,
            HookAttachedToTerrain = true,
            ControllerExpireAtUtc = DateTime.UtcNow
        };

        state.Clear();

        Assert.False(state.IsEngaged);
        Assert.Equal(Vector3.Zero, state.HookWorld);
        Assert.Equal(0u, state.HookBasisObjId);
        Assert.Equal(Vector3.Zero, state.HookLocalInBasis);
        Assert.Equal(0f, state.RopeLength);
        Assert.False(state.LastTeared);
        Assert.False(state.LastCutout);
        Assert.Equal(0f, state.MaxLaunchRange);
        Assert.False(state.HookAttachedToTerrain);
        Assert.Null(state.ControllerExpireAtUtc);
    }

    // ----------------------------------------------------------------
    // ShipStaticBarrier.TryCreate validation
    // ----------------------------------------------------------------

    [Fact]
    public void ShipStaticBarrier_TryCreate_NullEntry_ReturnsFalse()
    {
        Assert.False(ShipStaticBarrier.TryCreate(null, out _, out var error));
        Assert.Contains("null", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShipStaticBarrier_TryCreate_TooFewPoints_ReturnsFalse()
    {
        var dto = new ShipStaticBarrierEntryDto
        {
            Name = "bad",
            PointsXY = new List<List<double>> { new() { 1.0, 2.0 } }
        };

        Assert.False(ShipStaticBarrier.TryCreate(dto, out _, out var error));
        Assert.Contains("at least 2", error);
    }

    [Fact]
    public void ShipStaticBarrier_TryCreate_ZMaxLessThanZMin_ReturnsFalse()
    {
        var dto = new ShipStaticBarrierEntryDto
        {
            Name = "inverted",
            ZMin = 100f,
            ZMax = 50f,
            PointsXY = new List<List<double>>
            {
                new() { 0.0, 0.0 },
                new() { 10.0, 10.0 }
            }
        };

        Assert.False(ShipStaticBarrier.TryCreate(dto, out _, out var error));
        Assert.Contains("ZMax", error);
    }

    [Fact]
    public void ShipStaticBarrier_TryCreate_ValidEntry_ReturnsTrue()
    {
        var dto = new ShipStaticBarrierEntryDto
        {
            Name = "ok_wall",
            ZoneKey = 5,
            ZMin = 0f,
            ZMax = 100f,
            HalfThicknessMeters = 3f,
            Enabled = true,
            PointsXY = new List<List<double>>
            {
                new() { 100.0, 200.0 },
                new() { 300.0, 400.0 }
            }
        };

        Assert.True(ShipStaticBarrier.TryCreate(dto, out var barrier, out _));
        Assert.NotNull(barrier);
        Assert.Equal("ok_wall", barrier.Name);
        Assert.Equal(5u, barrier.ZoneKey);
        Assert.Equal(3f, barrier.HalfThicknessMeters);
    }

    // ----------------------------------------------------------------
    // NumericExtensions — JVector / Vector3 conversions
    // ----------------------------------------------------------------

    [Fact]
    public void ToVector_SwapsYAndZ()
    {
        var jv = new Jitter2.LinearMath.JVector(1f, 2f, 3f);
        var v = jv.ToVector();

        Assert.Equal(1f, v.X);
        Assert.Equal(3f, v.Y); // Jitter Z -> Vector Y
        Assert.Equal(2f, v.Z); // Jitter Y -> Vector Z
    }

    [Fact]
    public void ToJVector_SwapsYAndZ()
    {
        var v = new Vector3(1f, 2f, 3f);
        var jv = v.ToJVector();

        Assert.Equal(1f, jv.X);
        Assert.Equal(3f, jv.Y); // Vector Z -> Jitter Y
        Assert.Equal(2f, jv.Z); // Vector Y -> Jitter Z
    }
}
