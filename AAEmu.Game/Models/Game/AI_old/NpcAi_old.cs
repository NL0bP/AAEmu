using System;
using System.Numerics;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI_old.Abstracts;
using AAEmu.Game.Models.Game.AI_old.Static;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;

/*
   Author:Sagara, NLObP
*/
namespace AAEmu.Game.Models.Game.AI_old
{
    public sealed class NpcAi_old : ACreatureAi
    {
        public double Angle { get; set; }
        public Vector3 Velocity { get; set; } = new Vector3();

        public NpcAi_old(GameObject owner, float visibleRange) : base(owner, visibleRange)
        {
        }

        protected override void IamSeeSomeone(GameObject someone)
        {
            switch (someone.UnitType)
            {
                case BaseUnitType.Character:
                    var chr = (Character)someone;
                    var npc = (Npc)Owner;
                    var target = (BaseUnit)someone;
                    if (!npc.IsInBattle && npc.Hp > 0)
                    {
                        if (npc.SimulationNpc != null && npc.SimulationNpc.FollowPath)
                        {
                            npc.IsInPatrol = true;
                            npc.SimulationNpc.MoveToPathEnabled = false;
                            npc.SimulationNpc.GoToPath(npc, true);
                        }

                        // Monstrosity & Hostile & Fish
                        if (npc.Faction.Id == 115 || npc.Faction.Id == 3 || npc.Faction.Id == 172)
                        {
                            // if the Npc is aggressive, he will look at us and attack if close to us, otherwise he just looks at us
                            if (npc.Template.Aggression && npc.Template.AggroLinkHelpDist * npc.Template.AttackStartRangeScale > Math.Abs(MathUtil.CalculateDistance(npc.Position, chr.Position, true)))
                            {
                                // NPC attacking us
                                //npc.Patrol = null;
                                //npc.Patrol?.Pause(npc);

                                // AiAggro(ai_commands = 4065, count=0)
                                chr.BroadcastPacket(new SCTargetChangedPacket(npc.ObjId, chr.ObjId), true);
                                chr.BroadcastPacket(new SCAiAggroPacket(npc.ObjId, 1, chr.ObjId), true);
                                chr.BroadcastPacket(new SCCombatEngagedPacket(npc.ObjId), true); // caster
                                chr.BroadcastPacket(new SCAggroTargetChangedPacket(npc.ObjId, chr.ObjId), true);
                                npc.CurrentTarget = target;
                                npc.SetForceAttack(true);
                                npc.IsAutoAttack = true;
                                npc.IsInBattle = true;
                                var combat = new Combat();
                                //npc.Patrol.UpdateTime = DateTime.Now;
                                combat.Execute(npc);
                            }
                            else if (Math.Abs(MathUtil.CalculateDistance(npc.Position, chr.Position, true)) < 10f) // preferredCombatDistance = 20
                            {
                                //npc.Patrol = null;
                                //npc.Patrol?.Pause(npc);

                                // Npc looks at us
                                if (npc.CurrentTarget != target)
                                {
                                    chr.BroadcastPacket(new SCTargetChangedPacket(npc.ObjId, chr.ObjId), true);
                                    npc.CurrentTarget = target;
                                }

                                npc.LookTowards(target.Position);

                                //var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);

                                //moveType.X = npc.Position.X;
                                //moveType.Y = npc.Position.Y;
                                //moveType.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                                //if (npc.Position.Z - moveType.Z > 2.0)
                                //{
                                //    moveType.Z = npc.Position.Z;
                                //}

                                //// looks in the direction of movement
                                //Angle = MathUtil.CalculateAngleFrom(npc, chr);
                                //var rotZ = MathUtil.ConvertDegreeToDirection(Angle);
                                //moveType.Rot = new Quaternion(0f, 0f, Helpers.ConvertDirectionToRadian(rotZ), 1f);
                                ////npc.Rot = moveType.Rot;
                                //moveType.RotationX = 0;
                                //moveType.RotationY = 0;
                                ////moveType.RotationZ = Position.RotationZ;

                                ////moveType.DeltaMovement = Vector3.Zero;
                                //moveType.DeltaMovement = new sbyte[3];
                                //moveType.DeltaMovement[0] = 0;
                                //moveType.DeltaMovement[1] = 0;
                                //moveType.DeltaMovement[2] = 0;

                                //Velocity = Vector3.Zero;

                                //moveType.ActorFlags = ActorMoveType.Walk; // 5-walk, 4-run, 3-stand still
                                //moveType.Stance = EStance.Idle; // COMBAT = 0x0, IDLE = 0x1
                                //moveType.Alertness = AiAlertness.Alert; // IDLE = 0x0, ALERT = 0x1, COMBAT = 0x2
                                //moveType.Time = (uint)(DateTime.UtcNow - DateTime.Today).TotalMilliseconds;
                                //chr.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);
                            }
                            else if (npc.Template.AiFileId == AiFilesType.Roaming ||
                                     npc.Template.AiFileId == AiFilesType.BigMonsterRoaming ||
                                     npc.Template.AiFileId == AiFilesType.ArcherRoaming ||
                                     npc.Template.AiFileId == AiFilesType.WildBoarRoaming
                                     || npc.TemplateId == 4200
                                     )
                            {
                                // Npc roams around the spawn point in random directions
                                if (npc.CurrentTarget != null)
                                {
                                    chr.BroadcastPacket(new SCTargetChangedPacket(npc.ObjId, 0), true);
                                    npc.CurrentTarget = null;
                                }

                                if (npc.Patrol == null)
                                {
                                    //npc.IsInBattle = false;
                                    //npc.Patrol = new Roaming { Interrupt = true, Loop = true, Abandon = false };
                                    //npc.Patrol.Interrupt = true; // можно прервать
                                    //npc.Patrol.Loop = true;      // повторять в цикле
                                    //npc.Patrol.Abandon = false;  // не прерванный
                                    //npc.Patrol.Pause(npc);
                                    //npc.Patrol.LastPatrol = null; // предыдущего патруля нет
                                    //npc.Patrol.Recovery(npc);     // запустим патруль
                                }
                                else
                                {
                                    npc.Patrol.Recovery(npc);
                                }
                            }
                        }
                    }

                    break;
                case BaseUnitType.Npc:
                    break;
                case BaseUnitType.Slave:
                    break;
                case BaseUnitType.Housing:
                    break;
                case BaseUnitType.Transfer:
                    break;
                case BaseUnitType.Mate:
                    break;
                case BaseUnitType.Shipyard:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void IamUnseeSomeone(GameObject someone)
        {
        }

        protected override void SomeoneSeeMe(GameObject someone)
        {
        }

        protected override void SomeoneUnseeMee(GameObject someone)
        {
        }

        protected override void SomeoneThatIamSeeWasMoved(GameObject someone, MovementAction action)
        {
        }

        protected override void SomeoneThatSeeMeWasMoved(GameObject someone, MovementAction action)
        {
        }
    }
}
