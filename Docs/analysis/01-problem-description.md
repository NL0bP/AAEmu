# Описание проблемы с вращением домов

## Симптомы
- При создании дома без вращения взаимодействие с домом и предметами внутри работает корректно: предметы можно размещать и поднимать.
- При повороте дома на любой угол возникают проблемы с взаимодействием и предметами: предметы отображаются в неправильных позициях, взаимодействие с ними не работает.

## Предварительный анализ
Изучены следующие файлы:
1. `AAEmu.Game/Models/Game/Housing/House.cs` - основной класс дома.
2. `AAEmu.Game/Core/Managers/HousingManager.cs` - менеджер управления домами.
3. `AAEmu.Game/Core/Packets/C2G/CSRotateHousePacket.cs` - пакет вращения дома.
4. `AAEmu.Game/Models/Game/World/Transform/Transform.cs` - система трансформаций.

## Механизм вращения
В `CSRotateHousePacket.cs` (строки 27-33) при вращении дома выполняется:

```csharp
house.Transform.World.Rotation = house.Transform.World.Rotation with { Z = zRot };
house.Transform.World.Position = house.Transform.World.Position with { Z = height };
house.IsDirty = true;
```

При этом для прикрепленных предметов (doodads) в `House.cs` (строки 103-105) устанавливается родительский Transform:

```csharp
doodad.Transform = this.Transform.CloneDetached(doodad);
doodad.Transform.Parent = this.Transform;
doodad.Transform.Local.ApplyWorldSpawnPositionWithDeg(bindingDoodad.Position);
```

## Возможная причина проблемы
При вращении дома через `CSRotateHousePacket` изменяется только `Transform.World.Rotation` и `Transform.World.Position` (только Z-координата). Однако:

1. **Математическая ошибка в преобразовании координат** - система Transform использует некорректный порядок умножения кватернионов при вычислении мировых координат дочерних объектов. Это приводит к тому, что предметы внутри дома получают абсолютно неверные позиции при вращении родителя.

2. **Не вызывается обновление дочерних объектов** - даже с правильной математикой, необходимо явно вызвать `FinalizeTransform()` для принудительного пересчета мировых координат всех прикрепленных объектов.

## Следующие шаги
1. ✅ Проверить реализацию Transform и найти ошибку в вычислении координат дочерних объектов.
2. ✅ Исправить порядок умножения кватернионов в `GetWorldPosition()`.
3. ✅ Добавить вызов `FinalizeTransform()` в `CSRotateHousePacket`.
4. ✅ Написать unit-тесты для проверки корректности вращения.
5. Провести интеграционное тестирование с клиентом.
