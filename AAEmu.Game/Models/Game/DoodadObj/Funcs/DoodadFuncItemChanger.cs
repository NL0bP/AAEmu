using System;
using System.Diagnostics;
using System.Linq;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Doodads;
using AAEmu.Game.Models.Tasks.Skills;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

// Специальная задача для отложенной визуальной смены фазы дудада
public class DelayedPhaseChangeTask : DoodadFuncTask
{
    private readonly BaseUnit _caster;
    private readonly Doodad _owner;
    private readonly int _nextPhase;

    public DelayedPhaseChangeTask(BaseUnit caster, Doodad owner, int nextPhase) : base(caster, owner, 0)
    {
        _caster = caster;
        _owner = owner;
        _nextPhase = nextPhase;
    }

    public override void Execute()
    {
        // ИСПРАВЛЕНИЕ: Используем IsVisible вместо приватного IsDeleted
        if (_owner != null && _owner.IsVisible)
        {
            _owner.DoChangePhase(_caster, _nextPhase);
        }
    }
}

public class DoodadFuncItemChanger : DoodadPhaseFuncTemplate
{
    // doodad_phase_funcs
    public int ItemCount { get; set; }
    public int ItemId { get; set; }
    public int NextPhase { get; set; }
    public int SkillId { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner, ref Doodad.PhaseRollContext ctx)
    {
        if (caster is not Character character)
        {
            Logger.Trace($"DoodadFuncItemChanger: Id={Id}, ItemCount={ItemCount}, ItemId={ItemId}, NextPhase={NextPhase}, SkillId={SkillId}");
            return false;
        }

        Logger.Debug($"DoodadFuncItemChanger: Id={Id}, ItemCount={ItemCount}, ItemId={ItemId}, NextPhase={NextPhase}, SkillId={SkillId}");

        // РЕШЕНИЕ 1: Блокируем авто-посадку для клумб с выбором семян
        var isAutomaticLoop = new StackTrace().GetFrames().Any(f => f.GetMethod().Name == "DoPhaseFuncs");
        if (isAutomaticLoop)
        {
            var changerCount = owner.CurrentPhaseFuncs?.Count(pf => pf.FuncType == "DoodadFuncItemChanger") ?? 0;

            if (changerCount > 1)
            {
                return false;
            }
        }

        // 2. Стандартная проверка и списание семян
        if (ItemId > 0 && ItemCount > 0)
        {
            character.Inventory.Bag.GetAllItemsByTemplate((uint)ItemId, -1, out _, out var availableItemCount);
            if (availableItemCount < ItemCount)
            {
                return false;
            }

            var consumedItemCount = character.Inventory.Bag.ConsumeItem(ItemTaskType.DoodadItemChanger, (uint)ItemId, ItemCount, null);
            if (consumedItemCount < ItemCount)
            {
                return false;
            }
        }

        // РЕШЕНИЕ 2: Запуск анимации и отложенная смена визуальной фазы
        if (SkillId > 0)
        {
            var skillTemplate = SkillManager.Instance.GetSkillTemplate((uint)SkillId);
            if (skillTemplate != null)
            {
                var useSkill = new Skill(skillTemplate);

                // Мгновенно запускаем анимацию каста у персонажа
                TaskManager.Instance.Schedule(new UseSkillTask(useSkill, caster, new SkillCasterUnit(caster.ObjId), owner, new SkillCastDoodadTarget { ObjId = owner.ObjId }, null), TimeSpan.Zero);

                // ЗДЕСЬ МЕНЯЕТСЯ ВРЕМЯ ЗАДЕРЖКИ:
                // Сейчас установлено 3500 миллисекунд (3,5 секунды).
                TaskManager.Instance.Schedule(new DelayedPhaseChangeTask(caster, owner, NextPhase), TimeSpan.FromMilliseconds(3500));

                owner.ToNextPhase = false;
                return true;
            }
        }

        // Резервный вариант: если у действия нет анимации
        owner.ToNextPhase = true;
        owner.OverridePhase = NextPhase;
        return true;
    }
}
