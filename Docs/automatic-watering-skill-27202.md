# Навык 27202 "Автоматический полив"

## Контекст

Проблема проявлялась при использовании навыка полива растений водой из цистерны/трактора.
Клиент запускал навык `27202`, сервер принимал физическим caster-ом транспорт, но вложенный навык полива `15601` получал цель как unit-target на тот же транспорт.

Фрагмент из `AAEmu.Game/bin/Debug/net10.0/Logs/Server.log`:

```text
StartSkill: Id 27202, flag 0, caster=273, target=273
MOUNT SKILL: Player Zzz is mounted at Driver, skillId=27202, caster=273, casterType=Mount
Created SkillTlId 11 for Skill 27202, Caster Red Farm Hauler (146:273) with target Red Farm Hauler (146:273)
SpecialEffect, Special: SkillUse, Value1: 15601
Created SkillTlId 12 for Skill 15601, Caster Red Farm Hauler (146:273) with target Red Farm Hauler (146:273)
```

В более свежем запуске после первого исправления сервер уже рассчитывал позиционную цель для применения эффектов, но в клиентский `SCSkillFiredPacket` все еще уходил target транспорта:

```text
SCSkillFiredPacket - Id 15601, TlId 12, Caster 24380, Target 24380, Skill 15601
```

Это совпадает с наблюдением в клиенте: струя воды не видна, а зеленый круг рисуется вокруг трактора.

Следующий запуск показал улучшение: зеленый круг начал рисоваться перед трактором, но визуальная струя воды от `15601` все еще не появлялась.
В логе это выглядело так:

```text
SCSkillFiredPacket - Id 15601, TlId 17, Caster 115998, Target 0, Skill 15601
```

`Target 0` здесь означает не unit-target, а позиционный target с нулевым `ObjId`, поэтому геометрия уже стала правильной.
Оставшийся дефект был в дополнительных полях `SCSkillFiredPacket`: для `source_mount` proxy-навыков с `channeling_time=0` текущий код все равно добавлял базовые `+10` к `effectDelay` и `channelingTime`.

## Данные навыков

По `compact.sqlite3`:

```text
27202 -> skill_effects -> SpecialEffect type 33 (SkillUse) -> value1=15601
15601 -> target_type_id=14, target_selection_id=2, target_offset_distance=13.5, target_area_radius=9, target_area_count=50
```

То есть `27202` является управляющим навыком, который через `SpecialEffect.SkillUse` запускает фактический навык полива `15601`.
Для `15601` важен `target_offset_distance`: область эффекта должна строиться не вокруг самого транспорта, а вокруг позиционной точки перед цистерной.

## Причина дефекта

В `AAEmu.Game/Models/Game/Skills/Effects/SpecialEffects/SkillUse.cs` вложенный навык всегда получал:

```csharp
targetObj = new SkillCastUnitTarget(target?.ObjId ?? 0);
```

Для навыков без позиционного offset это допустимый fallback. Но для `15601` такая цель ломает геометрию эффекта:

- caster остается транспортом, что правильно;
- target становится самим транспортом, что неверно для фронтального полива;
- `Skill.Use` и `ApplyEffects` ищут AoE вокруг unit-target транспорта, а не вокруг расчетной точки струи воды;
- растения перед цистерной не попадают в ожидаемую область действия.

## Исправление target для вложенного навыка

Добавлен общий helper `CreateTargetForTriggeredSkill`.
Он смотрит на шаблон вложенного навыка:

- если `TargetOffsetDistance > 0`, создает `SkillCastPositionTarget` впереди физического caster-а;
- учитывает `TargetOffsetAngle`;
- сохраняет прежний `SkillCastUnitTarget` для навыков без offset.

Это не добавляет условий по ID `27202` или `15601` и сохраняет data-driven подход skill-системы.

Ключевая логика:

```csharp
if (template?.TargetOffsetDistance > 0 && caster?.Transform?.World != null)
{
    var worldTransform = caster.Transform.World;
    var angle = worldTransform.Rotation.Z + (MathF.PI / 2f) + (template.TargetOffsetAngle * MathF.PI / 180f);
    var (x, y) = MathUtil.AddDistanceToFront(template.TargetOffsetDistance, worldTransform.Position.X, worldTransform.Position.Y, angle);

    return new SkillCastPositionTarget
    {
        Type = SkillCastTargetType.Position,
        PosX = x,
        PosY = y,
        PosZ = worldTransform.Position.Z,
        PosRot = worldTransform.Rotation.Z
    };
}
```

## Исправление fired target для клиента

В `AAEmu.Game/Models/Game/Skills/Skill.cs` логика отправки `SCSkillFiredPacket` приводила любой не-unit target к `SkillCastUnitTarget(caster.ObjId)`.
Это было нужно для item/doodad-навыков, но ломало позиционные навыки, включая `15601`.

Добавлен helper `CreateFiredTarget`:

- `SkillCastUnitTarget` отправляется как есть;
- `SkillCastPositionTarget`, `SkillCastPosition2Target`, `SkillCastPosition3Target` отправляются как есть;
- для doodad/item target сохраняется прежний fallback на unit self.

Так сервер продолжает вести себя совместимо для item/doodad-навыков, но клиент получает позиционную цель полива и может корректно отрисовать область/струю перед цистерной.

## Исправление timing-полей для mount proxy

Для `15601` данные шаблона:

```text
id=15601, fire_anim_id=0, start_anim_id=0, channeling_time=0, source_mount=1
```

В `AAEmu.Game/Core/Packets/G2C/SCSkillFiredPacket.cs` добавлен helper `UseMountedSkillZeroDelayFields`.
Если fired skill имеет `SourceMount` или `SourceMountMate`, packet writer больше не добавляет искусственную базу `+10` к delay/channeling-полям.

Для `15601` это дает ожидаемые значения:

```text
effectDelay=0, channelingTime=0, anim=0
```

Такой формат ближе к эталонному поведению mount proxy-навыков и нужен клиенту для корректного запуска визуального FX полива.

## Сопоставление с packet capture

Дополнительно изучен пакетный лог:

```text
d:\DEVELOPMENT\archeage\SRC\_PDEC\PacketDecodeUniversal.PCAP\PDec\bin\x86\Debug\PJson\Лог поливалки трактора.json
```

В capture управляющий навык полива приходит как `15450`, а не `27202`, но в текущих данных сервера эти шаблоны имеют одинаковую роль: оба являются mount-навыками с `fire_anim_id=46`, `start_anim_id=41`, тремя повторами и запуском `15601`.

Официальная последовательность `SCSkillFired`:

```text
15450: casterType=Unit, casterObjId=tractor, targetType=Unit, targetObjId=tractor, msec=0/0, anim=46, flag=0
15601: casterType=Unit, casterObjId=tractor, targetType=Position, targetObjId=0/0/0, msec=0/0, anim=0, flag=0
15450/15601 повторяются с flag=2
```

Критичное отличие от текущей реализации было не в координатах, а в типе caster-а для серверного fired-пакета. Клиент отправляет старт mount-навыка с `SkillCasterMount`, но live-сервер отвечает в `SCSkillFired` как `SkillCasterUnit` с objId транспорта. Если сервер переиспользует клиентский `SkillCasterMount`, в packet stream добавляется `MountSkillTemplateId`, которого нет в capture. Из-за этого следующие поля пакета читаются клиентом со смещением, и визуальная струя воды не запускается даже при правильной позиционной цели.

В `CSStartSkillPacket.HandleMountedSkill` серверный caster для `skill.Use` теперь создается как:

```csharp
var serverCaster = new SkillCasterUnit(caster.ObjId);
```

Проверка mount-template и выбор attached-skill остаются в `HandleMountedSkill` до запуска навыка. Меняется только форма server-to-client `SCSkillFired`, чтобы она соответствовала capture.

## Исправление repeat timeline

Свежий серверный лог после исправления caster/target показал, что пакетная форма `27202` и `15601` уже совпадает с capture по caster type, target type, delay/channeling и fire animation.
Оставшееся отличие было в жизненном цикле timeline:

```text
AAEmu: SCSkillFired 27202 -> SCSkillEnded 27202 -> SCSkillFired 15601 ...
Live:  SCSkillFired 15450(flag=0) -> SCSkillFired 15601 -> SCSkillFired 15450(flag=2) -> ... -> SCSkillEnded 15450
```

Для `27202`/`15450` данные шаблона содержат `repeat_count=3` и `repeat_tick=1000`.
Live-сервер не завершает управляющий mount skill сразу после первого fired-пакета: он держит timeline на время repeat-серии и отправляет повторные fired-пакеты с финальным `flag=2`.

В `SCSkillFiredPacket` добавлено поле `FiredFlag`, по умолчанию `0`.
Для mount-навыков с `RepeatCount > 1` и `RepeatTick > 0` `Skill.ScheduleEffects` теперь:

- отправляет первый `SCSkillFired` как раньше, с `FiredFlag=0`;
- не вызывает `EndSkill` немедленно;
- планирует `RepeatSkillFiredTask`;
- задача отправляет оставшиеся fired-пакеты с `FiredFlag=2`;
- после последнего repeat закрывает исходный `TlId` через `SCSkillEndedPacket`.

Обычные instant skills без mount-repeat продолжают завершаться прежним путем.

## Ожидаемое поведение

После исправления последовательность остается прежней:

1. Игрок верхом запускает `27202`.
2. Сервер авторитетно выбирает транспорт/цистерну физическим caster-ом.
3. `SkillUse` запускает `15601`.
4. Для `15601` строится позиционная цель на `13.5` м впереди транспорта.
5. AoE радиусом `9` м применяется вокруг этой точки и может затронуть растения перед цистерной.
6. Управляющий mount skill повторяет fired-пакет по `repeat_tick` с `flag=2` и закрывает timeline только после repeat-серии.

## Тесты

Добавлены xUnit-тесты в:

```text
AAEmu.UnitTests/Game/Models/Game/Skills/SkillUseSpecialEffectTests.cs
```

Покрытие:

- вложенный skill с `TargetOffsetDistance` создает `SkillCastPositionTarget` впереди caster-а;
- `TargetOffsetAngle` влияет на расчет позиции;
- skill без offset сохраняет прежний `SkillCastUnitTarget`.
- `SCSkillFiredPacket` сохраняет позиционный target вместо замены на caster-а;
- doodad target по-прежнему заменяется на caster unit target для совместимости.
- fired packet для mount-triggered `15601` сериализует нулевые delay/channeling-поля.
- fired packet для mount-triggered `15601` сериализует caster-а как `SkillCasterType.Unit`, без поля `MountSkillTemplateId`.
- repeat fired packet сериализует финальный `FiredFlag=2`.

Проверочная команда:

```powershell
dotnet test AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-restore --filter FullyQualifiedName~SkillUseSpecialEffectTests
```

Результат последней проверки:

```text
Passed: 7, Failed: 0, Skipped: 0
```
