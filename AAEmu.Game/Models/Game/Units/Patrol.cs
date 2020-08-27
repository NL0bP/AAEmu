﻿using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units.Route;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.UnitMove;

namespace AAEmu.Game.Models.Game.Units
{
    /// <summary>
    /// Unit Patrol Class
    /// </summary>
    public abstract class Patrol
    {
        /// <summary>
        /// Are patrols under way?
        /// The default is False
        /// </summary>
        public bool Running { get; set; } = true;

        /// <summary>
        /// Is it a cycle?
        /// Default is True
        /// </summary>
        public bool Loop { get; set; } = true;
        /// <summary>
        /// Cyclic interval milliseconds
        /// </summary>
        public double LoopDelay { get; set; }
        /// <summary>
        /// Progress of implementation 0-100
        /// </summary>
        public sbyte Step { get; set; }
        /// <summary>
        /// Interrupt True
        /// Whether or not to terminate a route, such as being attacked or changing one's own state by other acts
        /// </summary>
        public bool Interrupt { get; set; } = true;
        /// <summary>
        /// Execution Sequence Number
        /// Each execution must increment the serial number, otherwise the action of repeating the serial number will not be executed.
        /// </summary>
        public uint Seq { get; set; } = 0;
        /// <summary>
        /// Current execution times
        /// </summary>
        protected uint Count { get; set; } = 0;
        /// <summary>
        /// Suspension of cruise points
        /// </summary>
        protected Point PausePosition { get; set; }
        /// <summary>
        /// Last mission
        /// </summary>
        public Patrol LastPatrol { get; set; }
        /// <summary>
        /// Abandon mission
        /// </summary>
        public bool Abandon { get; set; } = false;

        protected const float Tolerance = 1.401298E-45f;	// Погрешность

        /// <summary>
        /// Perform patrol missions
        /// </summary>
        /// <param name="unit"></param>
        public void Apply(BaseUnit unit)
        {
            // If NPC does not exist or is not in cruise mode or the current number of executions is not zero
            if (unit.Patrol != null && 
               (unit.Patrol.Running || this == unit.Patrol) &&
               (!unit.Patrol.Running || this != unit.Patrol))
            {
                return;
            }

            // If the last cruise mode is suspended, save the last cruise mode
            if (unit.Patrol != null && unit.Patrol != this && !unit.Patrol.Abandon)
            {
                LastPatrol = unit.Patrol;
            }
            ++Count;
            Seq = (uint)(DateTime.Now - GameService.StartTime).TotalMilliseconds;
            Running = true;
            unit.Patrol = this;
            switch (unit)
            {
                case Gimmick gimmick:
                    Execute(gimmick);
                    break;
                case Transfer transfer:
                    Execute(transfer);
                    break;
                default:
                    Execute(unit);
                    break;
            }
        }

        public abstract void Execute(BaseUnit unit);
        public abstract void Execute(Transfer transfer);
        public abstract void Execute(Gimmick gimmick);

        /// <summary>
        /// 再次执行任务
        /// Perform the task again
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="time"></param>
        /// <param name="patrol"></param>
        public void Repeat(BaseUnit unit, double time = 100, Patrol patrol = null)
        {
            switch (unit)
            {
                case Npc npc:
                    if (!(patrol ?? this).Abandon)
                        TaskManager.Instance.Schedule(new UnitMove(patrol ?? this, npc), TimeSpan.FromMilliseconds(time));
                    break;
                case Gimmick gimmick:
                    if (!(patrol ?? this).Abandon)
                        TaskManager.Instance.Schedule(new UnitMove(patrol ?? this, gimmick), TimeSpan.FromMilliseconds(time));
                    break;
                case Transfer transfer:
                    if (!(patrol ?? this).Abandon)
                        TaskManager.Instance.Schedule(new UnitMove(patrol ?? this, transfer), TimeSpan.FromMilliseconds(time));
                    break;
            }
        }

        public bool PauseAuto(BaseUnit unit)
        {
            if (!Interrupt && unit.Patrol.Running) { return false; }

            Pause(unit);
            return true;
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
            // Resume current cruise if current cruise is paused
            if (!Abandon && Running == false)
            {
                unit.Patrol.Running = true;
                Repeat(unit);
                return;
            }
            // If the last cruise is not null
            if (LastPatrol == null || Running) { return; }

            if (Math.Abs(unit.Position.X - LastPatrol.PausePosition.X) < Tolerance && Math.Abs(unit.Position.Y - LastPatrol.PausePosition.Y) < Tolerance && Math.Abs(unit.Position.Z - LastPatrol.PausePosition.Z) < Tolerance)
            {
                LastPatrol.Running = true;
                unit.Patrol = LastPatrol;
                // Resume last cruise
                Repeat(unit, 500, LastPatrol);
            }
            else
            {
                // Create a straight cruise to return to the last cruise pause
                var line = new Line();
                // Uninterrupted, unaffected by external forces and attacks
                line.Interrupt = false;
                line.Loop = false;
                line.LastPatrol = LastPatrol;
                // Specify target point
                line.Position = LastPatrol.PausePosition;
                // Resume last cruise
                Repeat(unit, 500, line);
            }
        }

        public void LoopAuto(BaseUnit unit)
        {
            if (Loop)
            {
                Count = 0;
                Seq = (uint)(DateTime.Now - GameService.StartTime).TotalMilliseconds;
                Repeat(unit, LoopDelay);
            }
            else
            {
                // Acyclic tasks terminate this task and attempt to resume the last task
                Stop(unit);
            }
        }
    }
}
