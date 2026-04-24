# Анализ первопричины проблемы вращения дома

## Выявленная проблема
При вращении дома через `CSRotateHousePacket` изменяется только `Transform.World.Rotation` и `Transform.World.Position` (Z-координата) самого дома:

```csharp
house.Transform.World.Rotation = house.Transform.World.Rotation with { Z = zRot };
house.Transform.World.Position = house.Transform.World.Position with { Z = height };
```

Прикрепленные к дому предметы (doodads) имеют установленный `Parent = house.Transform`. Их мировые координаты должны вычисляться автоматически через цепочку трансформаций, но из-за ошибки в математических вычислениях они получались некорректными.

## Почему это ломает взаимодействие?
1. **Предметы внутри дома** - при вращении дома их мировые координаты (X, Y) должны измениться, так как они вращаются вместе с домом. Из-за ошибки в преобразовании координат:
   - Клиент отображает предметы в неправильных позициях
   - Проверка расстояния для взаимодействия (`GetDistanceTo`) использует неверные координаты
   - Клик по предмету не попадает в правильную область

2. **Коллизия и границы** - коллижн-меш предметов находится в неверных позициях, что нарушает физику и взаимодействие.

## Технический анализ
В `House.cs` при создании дома, для прикрепленных предметов (binding doodads) устанавливается:
```csharp
doodad.Transform.Parent = this.Transform; // Родительский Transform
doodad.Transform.Local.ApplyWorldSpawnPositionWithDeg(bindingDoodad.Position);
```

В `HousingManager.DecorateHouse` для размещаемых игроком предметов:
```csharp
doodad.Transform.Parent = house.Transform;
doodad.Transform.Local.SetPosition(pos.X, pos.Y, pos.Z);
doodad.Transform.Local.ApplyFromQuaternion(quat);
```

Система Transform вычисляет мировые координаты дочерних объектов через рекурсивный метод `GetWorldPosition()`, который преобразует локальные координаты через вращение родителя. Формула преобразования точки `p` кватернионом `q`: `p' = q * p * q^-1`.

## Корневая причина
В файле `AAEmu.Game/Models/Game/World/Transform/Transform.cs`, метод `GetWorldPosition()` (строка 366) использовал **обратный порядок умножения кватернионов**:

```csharp
var localTranslatedPos = Quaternion.Inverse(parentQuatRotation) * localQuatPos * parentQuatRotation;
```

Это соответствует формуле `q^-1 * p * q`, что является **некорректным** преобразованием вращения. Правильная формула: `q * p * q^-1`.

Из-за этого дочерние объекты получали абсолютно неверные мировые координаты при вращении родителя. Например, при 90° вращении дома предмет, находящийся справа от дома (локально X=10), оказывался не впереди (Y=10), а позади (Y=-10) или в других неправильных позициях.

## Исправление
### 1. Исправление Transform.cs (строка 366)
```csharp
var localTranslatedPos = parentQuatRotation * localQuatPos * Quaternion.Inverse(parentQuatRotation);
```

### 2. Добавление вызова FinalizeTransform() в CSRotateHousePacket.cs
```csharp
house.Transform.FinalizeTransform();
```

Это принудительно обновляет мировые координаты всех прикрепленных объектов и делает их видимыми через `WorldManager.Instance.AddVisibleObject()`.

## Следующие шаги
1. ✅ Исправить Transform.cs
2. ✅ Добавить FinalizeTransform() в CSRotateHousePacket.cs
3. ✅ Написать и проверить unit-тесты
4. Провести интеграционное тестирование с клиентом
5. Проверить производительность при большом количестве предметов
