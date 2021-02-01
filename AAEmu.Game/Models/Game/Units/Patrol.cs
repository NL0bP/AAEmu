using System;
using System.Numerics;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.Units.Route;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.UnitMove;
using NLog;

namespace AAEmu.Game.Models.Game.Units
{
    /// <summary>
    /// Unit 巡逻类
    /// Unit Patrol Class
    /// </summary>
    public abstract class Patrol
    {
        protected static Logger _log = LogManager.GetCurrentClassLogger();

        protected const float Tolerance = 0.5f; // 1.401298E-45f Погрешность
        public Skill skill;
        public int idx; // index of skill
        public float ReturnDistance;
        public bool GoBack { get; set; }
        public bool InPatrol { get; set; }
        public bool InitMovement { get; set; } // инициируем точку возврвата, для возврата, если NPC ушел далеко
        public short Degree { get; set; } = 360;
        public float Time { get; set; }
        public float DeltaTime { get; set; } = 0.1f;
        public DateTime UpdateTime { get; set; }
        public UnitMoveType moveType { get; set; }
        public double Angle { get; set; }
        public short AngleZ { get; set; }
        public float Distance { get; set; }
        public float MovingDistance { get; set; } = 0.27f;
        public double AngleTmp { get; set; }
        public float AngVelocity { get; set; } = 5.0f;
        public float MaxVelocityForward { get; set; } = 5.4f;
        public float MaxVelocityBackward { get; set; } = 0f;
        public float VelAccel { get; set; } = 1.0f;
        public Vector3 vBeginPoint { get; set; }
        public Vector3 vEndPoint { get; set; }
        public Vector3 vMovingDistance { get; set; }
        public Vector3 vMaxVelocityForwardRun { get; set; } = new Vector3(5.4f, 5.4f, 5.4f);
        public Vector3 vMaxVelocityBackRun { get; set; } = new Vector3(-5.4f, -5.4f, -5.4f);
        public Vector3 vMaxVelocityForwardWalk { get; set; } = new Vector3(1.8f, 1.8f, 1.8f);
        public Vector3 vMaxVelocityBackWalk { get; set; } = new Vector3(-1.8f, -1.8f, -1.8f);
        public Vector3 vVelocity { get; set; }
        public Vector3 vVelAccel { get; set; } = new Vector3(1.8f, 1.8f, 1.8f);
        public Vector3 vPosition { get; set; }
        public Vector3 vPausePosition { get; set; }
        public Vector3 vTarget { get; set; }
        public Vector3 vDistance { get; set; }
        public float RangeToCheckPoint { get; set; } = 0.25f; // distance to checkpoint at which it is considered that we have reached it
        public Vector3 vRangeToCheckPoint { get; set; } = new Vector3(0.5f, 0.5f, 0f); // distance to checkpoint at which it is considered that we have reached it
        public bool FollowPath { get; set; } = false;
        
        /// <summary>
        /// 是否正在执行巡逻任务
        /// Are patrols under way?
        /// 默认为 False
        /// The default is False
        /// </summary>
        public bool Running { get; set; } = true;

        /// <summary>
        /// 是否为循环
        /// Is it a cycle?
        /// 默认为 True
        /// Default is True
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// 循环间隔 毫秒
        /// Cyclic interval milliseconds
        /// </summary>
        public double LoopDelay { get; set; }

        /// <summary>
        /// 执行进度 0-100
        /// Progress of implementation 0-100
        /// </summary>
        public sbyte Step { get; set; }

        /// <summary>
        /// 中断 True
        /// Interrupt True
        /// 如被攻击或其他行为改变自身状态等 是否终止路线
        /// Whether or not to terminate a route, such as being attacked or changing one's own state by other acts
        /// </summary>
        public bool Interrupt { get; set; } = true;

        /// <summary>
        /// 执行顺序编号
        /// Execution Sequence Number
        /// 每次执行必须递增序号，否则重复序号的动作不被执行
        /// Each execution must increment the serial number, otherwise the action of repeating the serial number will not be executed.
        /// </summary>
        public uint Seq { get; set; } = 0;

        /// <summary>
        /// 当前执行次数
        /// Current execution times
        /// </summary>
        protected uint Count { get; set; } = 0;

        /// <summary>
        /// 暂停巡航点
        /// Suspension of cruise points
        /// </summary>
        protected Point PausePosition { get; set; }

        /// <summary>
        /// 上次任务
        /// Last mission
        /// </summary>
        public Patrol LastPatrol { get; set; }

        /// <summary>
        /// 放弃任务 / Abandon mission
        /// </summary>
        public bool Abandon { get; set; } = false;
        public const float tolerance = 0.5f;


        /// <summary>
        /// 执行巡逻任务
        /// Perform patrol missions
        /// </summary>
        /// <param name="npc"></param>
        public void Apply(BaseUnit unit)
        {
            //如果NPC不存在或不处于巡航模式或者当前执行次数不为0
            //If NPC does not exist or is not in cruise mode or the current number of executions is not zero
            if (unit.Patrol == null || (unit.Patrol.Running == false && this != unit.Patrol) || (unit.Patrol.Running == true && this == unit.Patrol))
            {
                //如果上次巡航模式处于暂停状态则保存上次巡航模式
                //If the last cruise mode is suspended, save the last cruise mode
                if (unit.Patrol != null && unit.Patrol != this && !unit.Patrol.Abandon)
                {
                    LastPatrol = unit.Patrol;
                }
                ++Count;
                Seq = (uint)(DateTime.UtcNow - DateTime.Today).TotalMilliseconds;
                Running = true;
                unit.Patrol = this;
                Execute(unit);
            }
        }

        /// <summary>
        /// 再次执行任务
        /// Perform the task again
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="time"></param>
        /// <param name="patrol"></param>
        public void Repeat(BaseUnit unit, double time = 100, Patrol patrol = null)
        {
            if (!(patrol ?? this).Abandon)
            {
                TaskManager.Instance.Schedule(new UnitMove(patrol ?? this, unit), TimeSpan.FromMilliseconds(time));
            }
        }

        public bool PauseAuto(BaseUnit unit)
        {
            if (Interrupt || !unit.Patrol.Running)
            {
                Pause(unit);
                return true;
            }
            return false;
        }

        public void Pause(BaseUnit unit)
        {
            Running = false;
            PausePosition = unit.Position.Clone();
        }

        public void Stop(BaseUnit unit)
        {
            Running = false;
            Abandon = true;

            Recovery(unit);
        }

        public void Recovery(BaseUnit unit)
        {
            // 如果当前巡航处于暂停状态则恢复当前巡航
            // Resume current cruise if current cruise is paused
            if (!Abandon && Running == false)
            {
                unit.Patrol.Running = true;
                Repeat(unit);
                return;
            }
            // 如果上次巡航不为null
            // If the last cruise is not null
            if (LastPatrol != null && Running == false)
            {
                if (unit.Position.X == LastPatrol.PausePosition.X && unit.Position.Y == LastPatrol.PausePosition.Y && unit.Position.Z == LastPatrol.PausePosition.Z)
                {
                    LastPatrol.Running = true;
                    unit.Patrol = LastPatrol;
                    // 恢复上次巡航
                    // Resume last cruise
                    Repeat(unit, 500, LastPatrol);
                }
                else
                {
                    // 创建直线巡航回归上次巡航暂停点
                    // Create a straight cruise to return to the last cruise pause
                    var line = new Line();
                    // 不可中断，不受外力及攻击影响 类似于处于脱战状态
                    // Uninterrupted, unaffected by external forces and attacks
                    line.Interrupt = true;
                    line.Loop = false;
                    line.LastPatrol = LastPatrol;
                    // 指定目标Point
                    // Specify target point
                    line.Position = LastPatrol.PausePosition;
                    // 恢复上次巡航
                    // Resume last cruise
                    Repeat(unit, 500, line);
                }
            }
        }

        public void LoopAuto(BaseUnit unit)
        {
            if (Loop)
            {
                Count = 0;
                //Seq = 0;
                Seq = (uint)(DateTime.UtcNow - DateTime.Today).TotalMilliseconds;
                Repeat(unit, LoopDelay);
            }
            else
            {
                // 非循环任务则终止本任务并尝试恢复上次任务
                // Acyclic tasks terminate this task and attempt to resume the last task
                Stop(unit);
            }
        }
        public SkillCastTarget GetSkillCastTarget(Unit caster, Skill skill)
        {
            SkillCastTarget targetType;
            switch (skill.Template.TargetType)
            {
                case SkillTargetType.Doodad:
                    targetType = SkillCastTarget.GetByType(SkillCastTargetType.Doodad);
                    targetType.ObjId = caster.CurrentTarget.ObjId;
                    break;
                case SkillTargetType.AnyUnit:
                case SkillTargetType.AnyUnitAlways:
                case SkillTargetType.Friendly:
                case SkillTargetType.FriendlyOthers:
                case SkillTargetType.GeneralUnit:
                case SkillTargetType.Hostile:
                case SkillTargetType.Others:
                    targetType = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
                    targetType.ObjId = caster.CurrentTarget.ObjId;
                    break;
                case SkillTargetType.Self:
                    targetType = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
                    targetType.ObjId = caster.CurrentTarget?.ObjId ?? caster.ObjId;
                    break;
                case SkillTargetType.ArtilleryPos:
                case SkillTargetType.BallisticPos:
                case SkillTargetType.CommanderPos:
                case SkillTargetType.CursorPos:
                case SkillTargetType.Pos:
                case SkillTargetType.RelativePos:
                case SkillTargetType.SourcePos:
                case SkillTargetType.SummonPos:
                    targetType = SkillCastTarget.GetByType(SkillCastTargetType.Position);
                    break;
                case SkillTargetType.Item:
                    targetType = SkillCastTarget.GetByType(SkillCastTargetType.Item);
                    targetType.ObjId = caster.CurrentTarget.ObjId;
                    break;
                case SkillTargetType.Party:
                case SkillTargetType.Raid:
                case SkillTargetType.Line:
                case SkillTargetType.Pet:
                case SkillTargetType.Parent:
                case SkillTargetType.ChildSlave:
                case SkillTargetType.PetOwner:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return targetType;
        }

        public abstract void Execute(BaseUnit unit);
        public abstract void Execute(Npc npc);
        public abstract void Execute(Transfer transfer);
        public abstract void Execute(Gimmick gimmick);
    }
}
