# Doodad Persistence Summary — 2026-03-27

## Контекст проблемы

Посаженные/установленные doodad-объекты корректно меняют состояние во время работы сервера, но после рестарта часть таких объектов возвращается в стартовое состояние:

- лимонное дерево снова становится sapling;
- drill / majestic tree снова становятся `10/10`;
- world yams в Rokhala тоже ресетятся.

## Что было сделано

Исправление разбито на две части:

1. Базовый persistence-bug для doodad phase/state.
2. Отдельная persistence-ветка для static world doodads.

## Изменённые файлы

| Файл | Описание |
|------|----------|
| `AAEmu.Game/Models/Game/DoodadObj/Doodad.cs` | Полная переработка системы сохранения |
| `AAEmu.Game/Models/Game/DoodadObj/DoodadSpawner.cs` | Добавлен `AttachPersistentDoodad()` |
| `AAEmu.Game/Core/Managers/World/SpawnManager.cs` | World doodad persistence, подавление дублей |
| `AAEmu.Game/Core/Managers/Id/DoodadIdManager.cs` | Добавлена таблица `world_doodads` |
| `AAEmu.Game/GameService.cs` | Периодический flush + flush на shutdown |
| `AAEmu.Game/SQL/updates/2026-03-27_aaemu_game_3503_doodad_spawner_state.sql` | DDL для `world_doodads` |

## Основные исправления

### 1. Исправлен откат persisted doodad в стартовую фазу после рестарта

**Файл:** `Doodad.cs`

**Проблема:** При загрузке persisted doodad из БД читается `current_phase_id` и присваивается `doodad.FuncGroupId`, но затем вызывается `InitDoodad()` → `PerformPhaseChange()`, который делает `FuncGroupId = GetFuncGroupId()` — перезаписывая загруженную фазу стартовой.

**Решение:** В `PerformPhaseChange()` добавлена проверка:

```csharp
if (FuncGroupId == 0)
    FuncGroupId = GetFuncGroupId();
```

Если `FuncGroupId` уже загружен из БД (не 0), стартовая фаза больше не перезаписывается. Это устраняет основной reset в sapling / 10/10 для doodads, которые уже были persistent.

### 2. Добавлена batched save система (ScheduleSave)

**Файл:** `Doodad.cs`

**Проблема:** Ранее `FuncGroupId` setter и `Data` setter вызывали `Save()` напрямую — блокируя игровой тик при каждой записи в БД.

**Решение:** Введена отложенная система сохранения:

- `_dirtyDoodads` — `ConcurrentDictionary<uint, Doodad>` для батчинга
- `ScheduleSave()` — кладёт doodad в очередь, выдавая `DbId` до помещения (решает проблему конфликта по ключу `0`)
- `FlushSaves()` — периодически (раз в 5 сек) сохраняет dirty doodads через `ThreadPool`
- `ProcessPendingOperations(forceSave)` — точка входа для вызова из `TickManager` и shutdown
- `FuncGroupId` и `Data` setters теперь вызывают `ScheduleSave()` вместо `Save()`

### 3. Исправлен batched save для новых persistent doodads

**Файл:** `Doodad.cs`

**Проблема:** `ScheduleSave()` кладёт doodad в `_dirtyDoodads` по ключу `DbId`. У нового doodad до первого `Save()` — `DbId == 0`. Несколько новых doodads конфликтуют на одном ключе `0`, `TryAdd(0, this)` оставляет в очереди только первый объект.

**Решение:** В `ScheduleSave()` добавлена ранняя выдача `DbId`:

```csharp
if (DbId == 0)
    DbId = DoodadIdManager.Instance.GetNextId();
```

### 4. Добавлена отдельная таблица для мировых doodads

**Файл:** `2026-03-27_aaemu_game_3503_doodad_spawner_state.sql`

```sql
CREATE TABLE `world_doodads` (
  `id` int unsigned NOT NULL AUTO_INCREMENT,
  `source_template_id` int unsigned NOT NULL,
  `template_id` int unsigned NOT NULL,
  `x` decimal(13,3) NOT NULL,
  `y` decimal(13,3) NOT NULL,
  `z` decimal(13,3) NOT NULL,
  `roll` float NOT NULL DEFAULT '0',
  `pitch` float NOT NULL DEFAULT '0',
  `yaw` float NOT NULL DEFAULT '0',
  `current_phase_id` int unsigned NOT NULL,
  `plant_time` datetime NOT NULL,
  `growth_time` datetime NOT NULL,
  `phase_time` datetime NOT NULL,
  `freshness_time` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `scale` float NOT NULL DEFAULT '1',
  `data` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `ux_world_doodads_anchor` (`source_template_id`, `x`, `y`, `z`)
);
```

- обычные persistent doodads продолжают жить в `doodads`;
- static world doodads теперь сохраняются отдельно в `world_doodads`.

### 5. Переведено восстановление world doodads на anchor по template + coordinates

**Файлы:** `Doodad.cs`, `SpawnManager.cs`

**Проблема:** `spawner_id` в JSON не всегда надёжен как единственный ключ (встречаются `0`, `null` и повторы). Голые координаты тоже не подходят (есть коллизии x + y + z).

**Решение:** Ключ восстановления world doodad:

- `source_template_id` — исходный шаблон статического doodad
- `x`, `y`, `z` — квантизованные координаты до `0.001`
- Комбинация `template_id + x + y + z` на данных проекта коллизий не даёт.

Реализовано через `WorldDoodadKey` record struct:

```csharp
private readonly record struct WorldDoodadKey(uint SourceTemplateId, long X, long Y, long Z);

private static long QuantizeWorldCoord(float value)
{
    return decimal.ToInt64(decimal.Round((decimal)value * 1000m, 0, MidpointRounding.AwayFromZero));
}
```

### 6. Подавление дефолтного static spawn при наличии persisted override

**Файл:** `SpawnManager.cs`

При старте мира persisted world doodads загружаются из `world_doodads`, и их ключи сохраняются в `_persistedWorldDoodadKeys`. При обычном спавне doodads из spawn-файлов проверяется:

```csharp
if (_persistedWorldDoodadKeys.Contains(BuildWorldDoodadKey(spawner)))
    continue;
```

Это предотвращает двойной спавн: persisted override + дефолтная static копия.

### 7. Повторная привязка persisted doodad к DoodadSpawner

**Файлы:** `DoodadSpawner.cs`, `SpawnManager.cs`

`DoodadFuncFinal` (и `DoodadFuncPulse`, `DoodadFuncRatioRespawn`) требуют `owner.Spawner != null` для корректного respawn. Без привязки к spawner'у doodad просто удаляется без respawn.

Решение:

- `DoodadSpawner.AttachPersistentDoodad(doodad)` — привязывает doodad к spawner'у
- `SpawnManager.TryBindPersistentWorldDoodad(doodad)` — ищет подходящий spawner по `WorldDoodadKey` и вызывает `AttachPersistentDoodad`

### 8. Добавлен real flush очереди doodad save

**Файлы:** `GameService.cs`, `Doodad.cs`

**Проблема:** `_dirtyDoodads` раньше вообще не flush'ился — `ProcessPendingOperations()` нигде не вызывался.

**Решение:**

- В `GameService.StartAsync()` добавлена подписка на `TickManager`:
  ```csharp
  TickManager.Instance.OnTick.Subscribe(_ => Doodad.ProcessPendingOperations(), TimeSpan.FromSeconds(1), true);
  ```
- В `GameService.StopAsync()` добавлен принудительный flush перед остановкой мира:
  ```csharp
  Doodad.ProcessPendingOperations(true);
  ```

### 9. DoodadIdManager учитывает обе таблицы

**Файл:** `DoodadIdManager.cs`

```csharp
private static readonly string[,] ObjTables =
{
    { "doodads", "id" },
    { "world_doodads", "id" }
};
```

При выдаче `DbId` менеджер проверяет максимальный id в обеих таблицах, исключая коллизии.

### 10. EnsurePersistentWorldState — автоматическое включение persistence

**Файл:** `Doodad.cs`

При взаимодействии персонажа с world doodad (`Use`, `DoChangePhase`, `OnSkillHit`) вызывается `EnsurePersistentWorldState(caster)`. Если:

- doodad ещё не persistent,
- caster — Character,
- OwnerType — System,
- Spawner не null,

то автоматически выставляются `IsPersistent = true` и `SourceTemplateId`. Это позволяет корректно сохранять состояние world doodad при первом же взаимодействии.

## Как теперь работает периодическое сохранение

- `TickManager` вызывает `Doodad.ProcessPendingOperations()` раз в секунду;
- обычный `FlushSaves()` ограничен интервалом 5 секунд;
- в БД идут не все doodad'ы, а только те, что есть в `_dirtyDoodads`;
- в `_dirtyDoodads` doodad попадает только после изменения состояния (`FuncGroupId`, `Data`);
- если один и тот же doodad меняется несколько раз до flush, в БД уходит только последнее актуальное состояние;
- отдельный `forceSave` используется только на shutdown.

## Что нужно проверить руками

После применения SQL update нужно прогнать runtime-сценарии:

- `lemon tree` — изменить состояние, рестарт сервера, проверить что phase не откатилась
- `majestic tree` — аналогично
- `mining drill` — аналогично
- `Rokhala yams` — аналогично

Цель: убедиться, что phase/data/state больше не откатываются после рестарта.

## Результат сборки

```
dotnet build AAEmu.Game/AAEmu.Game.csproj
Ошибок: 0
Предупреждений: 69 (предсуществующие, не связаны с изменениями)
```
