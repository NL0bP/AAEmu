# Описание исправления проблемы вращения дома

## Проблема
При вращении дома через `CSRotateHousePacket` изменялись только свойства `Transform.World.Rotation` и `Transform.World.Position` (Z-координата) самого дома. Однако, прикрепленные к дому предметы (doodads) не обновляли свои мировые координаты корректно из-за ошибки в математических вычислениях преобразования координат.

## Корневая причина
В файле `AAEmu.Game/Models/Game/World/Transform/Transform.cs`, метод `GetWorldPosition()` (строка 366) использовал некорректный порядок умножения кватернионов для преобразования локальных координат дочерних объектов в мировые:

**Было (некорректно):**
```csharp
var localTranslatedPos = Quaternion.Inverse(parentQuatRotation) * localQuatPos * parentQuatRotation;
```

Формула вращения точки `p` кватернионом `q` должна быть: `p' = q * p * q^-1`. Использование обратного порядка (`q^-1 * p * q`) приводило к некорректному преобразованию координат, из-за чего дочерние объекты (предметы внутри дома) получали неправильные мировые позиции после вращения родителя.

## Исправление

### 1. Исправление Transform.cs (строка 366)
```csharp
// Было:
var localTranslatedPos = Quaternion.Inverse(parentQuatRotation) * localQuatPos * parentQuatRotation;

// Стало:
var localTranslatedPos = parentQuatRotation * localQuatPos * Quaternion.Inverse(parentQuatRotation);
```

Это исправление обеспечивает корректное математическое преобразование локальных координат дочерних объектов при вращении родителя.

### 2. Добавление вызова FinalizeTransform() в CSRotateHousePacket.cs
В файле `AAEmu.Game/Core/Packets/C2G/CSRotateHousePacket.cs` добавлен вызов `house.Transform.FinalizeTransform()` после изменения вращения и высоты дома:

```csharp
house.Transform.World.Rotation = house.Transform.World.Rotation with { Z = zRot };
house.Transform.World.Position = house.Transform.World.Position with { Z = height };
house.IsDirty = true;
// Обновляем мировые координаты всех прикрепленных объектов
house.Transform.FinalizeTransform();
```

Метод `FinalizeTransform()` принудительно пересчитывает мировые координаты всех прикрепленных объектов (дочерних элементов и sticky-детей) и обновляет их видимость через `WorldManager.Instance.AddVisibleObject()`.

**Примечание:** В предварительной документации предлагалась отправка `SCDoodadStatePacket` для каждого прикрепленного предмета. Однако такого типа пакета в текущей кодовой базе не существует. Вместо этого используется существующий механизм `FinalizeTransform()`, который уже обеспечивает необходимое обновление позиций через систему видимости и стандартные пакеты обновления состояния объектов.

## Ожидаемый результат
- При вращении дома все прикрепленные предметы (двери, окна, мебель) корректно вращаются вместе с домом.
- Мировые координаты дочерних объектов вычисляются математически верно.
- Взаимодействие с предметами внутри дома работает корректно.
- Клиент получает обновленные позиции объектов через стандартные механизмы синхронизации.

## Проверка
Для проверки необходимо:
1. Создать дом без вращения, разместить внутри предметы, убедиться что взаимодействие работает.
2. Повернуть дом на разные углы (90°, 180°, 270°), проверить что предметы вращаются вместе с домом и их мировые координаты вычисляются верно.
3. Попробовать взаимодействовать с предметами после вращения - должно работать корректно.
4. Проверить, что при изменении высоты дома предметы поднимаются/опускаются вместе с ним.

## Результаты тестирования
Все 7 unit-тестов в `HousingRotationTests.cs` проходят успешно:
- ✅ HouseRotation_WithoutRotation_Should_KeepDoodadPosition
- ✅ HouseRotation_With90Degrees_Should_UpdateDoodadWorldPosition
- ✅ HouseRotation_With180Degrees_Should_UpdateDoodadWorldPosition
- ✅ HouseRotation_MultipleDoodads_Should_UpdateAll
- ✅ HouseRotation_With270Degrees_Should_UpdateDoodadWorldPosition
- ✅ HouseRotation_HeightChange_Should_MoveDoodadWithHouse
- ✅ HouseRotation_RotationShouldNotAffectDoodadRotationIfNotSet
