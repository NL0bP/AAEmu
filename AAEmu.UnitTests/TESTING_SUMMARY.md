# AAEmu.UnitTests — Testing Summary

## Быстрый старт

Запуск всех тестов:
```bash
dotnet test AAEmu.UnitTests/AAEmu.UnitTests.csproj
```

Запуск с фильтром:
```bash
dotnet test --filter "FullyQualifiedName~BuffStacking"
```

## Структура тестов

### 1. Тесты менеджеров (Core/Managers)
- AccountManagerTests
- MailManagerTests
- IdManagerTests
- WorldManagerTests
- WorldManagerTestsId

### 2. Тесты GameData
- CommonFarmGameDataTests
- **SlaveMountSkillDataTests** — проверка связей mount skills с баффами транспорта

### 3. Тесты AI
- TestAi

### 4. Тесты семьи (Family)
- FamilyTests

### 5. Тесты жилья (Housing)
- HousingTests
- HousingBuilderTests

### 6. Тесты предметов (Items)
- ItemConversionTests
- ItemRequirementTests

### 7. Тесты навигации (NavMesh)
- NavMeshTests

### 8. Тесты квестов (Quest)
- QuestTests

### 9. Тесты скиллов (Skills)
- SkillModifiersTests
- **BuffStackingTests** — стекирование, BaseDuration, OverwriteWith
- **BuffsStackingTests** — интеграционные тесты через Buffs.AddBuff

### 10. Тесты юнитов (Units)
- UnitTests

### 11. Тесты JSON моделей
- ModelJsonTests

### 12. Тесты утилит
- SubTaskTests
- SubTaskLoopTests
- SubTaskLoopWaitTests

## BuffTestBuilder

```csharp
var buff = new BuffTestBuilder()
    .WithBuffId(1)
    .WithDuration(5000)
    .WithCaster(caster)
    .WithOwner(owner)
    .WithStackRule(BuffStackRule.Extend)
    .AsStackable(maxStacks: 5)
    .Build();
```

## Покрытие по областям

### 5. Тесты стекирования баффов (BuffStackingTests)

**9 тестов:**
- ✅ Стекирование до лимита
- ✅ Замена при превышении лимита
- ✅ Разные ID не влияют друг на друга
- ✅ Независимое удаление
- ✅ Удаление одного из стека
- ✅ Обработка истечения в стеке
- ✅ `BaseDuration` устанавливается при создании баффа
- ✅ `OverwriteWith` при `Extend` копирует `BaseDuration` и суммирует `Duration`
- ✅ `OverwriteWith` при `Refresh` заменяет `Duration`, сохраняя `BaseDuration`

### 6. Тесты таймера баффа (BuffTimerTests)

**4 теста:**
- ✅ Таймер запускается при создании
- ✅ Таймер останавливается при выходе
- ✅ Таймер корректно пересчитывает время
- ✅ Таймер обрабатывает пограничные значения
