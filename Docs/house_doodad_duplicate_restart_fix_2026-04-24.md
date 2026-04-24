# House Doodad Duplicate Restart Fix — 2026-04-24

## Контекст проблемы

После рестарта сервера таблица `doodads` могла получать повторные записи для структурных doodad-объектов домов: дверей, окон, клумб и других объектов, привязанных к `attach_point`.

С каждым последующим рестартом количество таких записей росло. Это приводило к деградации старта и лишней работе при загрузке persistent doodads.

## Корневая причина

Порядок старта сервера:

1. `HousingManager.Load()` загружает дома из таблицы `housings`.
2. При установке `House.CurrentStep = -1` для уже построенного дома сеттер создаёт binding doodads из `Template.HousingBindingDoodad`.
3. Эти doodads были помечены как `IsPersistent = true`.
4. Позже `InitDoodad()` / смена фазы могли поставить временный doodad в очередь сохранения.
5. Так как временный doodad был создан не из БД, у него `DbId = 0`; при сохранении ему выдавался новый id.
6. `REPLACE INTO doodads` не находил существующую строку по новому id и вставлял новую запись.

При этом настоящие persistent doodads загружались позже через `SpawnManager.SpawnPersistentDoodads(DoodadOwnerType.Housing)` и заменяли временные объекты в `house.AttachedDoodads` по `attach_point`. Проблема была именно в том, что временные placeholders успевали стать persistent.

## Исправление в коде

Добавлен флаг `House.IsLoadingFromDatabase`.

Во время загрузки `CurrentStep` из БД:

```csharp
try
{
    house.IsLoadingFromDatabase = true;
    house.CurrentStep = reader.GetInt32("current_step");
}
finally
{
    house.IsLoadingFromDatabase = false;
}
```

В `House.CurrentStep` binding doodads теперь получают:

```csharp
doodad.IsPersistent = !IsLoadingFromDatabase;
```

Это сохраняет нужное поведение:

- при рестарте сервера создаются только временные visual placeholders, они не сохраняются в `doodads`;
- при обычном создании или завершении строительства дома binding doodads остаются persistent и сохраняются как раньше.

## Исправление наложения открытой и закрытой двери/окна

После первой правки дубликаты в таблице `doodads` больше не создавались, но при рестарте мог оставаться визуальный дубль: временный binding doodad из `House.CurrentStep` показывал стартовую фазу, а persistent doodad из БД показывал сохранённую фазу. Для двери или окна это выглядело как два объекта одновременно: закрытый placeholder и открытый persisted doodad.

Причина была в `SpawnManager.SpawnPlayerDoodads()`: при чтении housing doodad из БД `SpawnPersistentDoodads()` уже выставлял `ParentObjId = owningHouse.ObjId`. Позже код замены structural placeholder выполнялся только при `doodad.ParentObjId <= 0`, поэтому для уже привязанного persistent doodad ветка замены пропускалась.

Добавлен метод `House.ReplaceAttachedDoodadByAttachPoint(Doodad doodad)`.

Он:

- ищет текущий attached doodad дома с тем же `AttachPoint`;
- переносит persisted doodad на transform placeholder, чтобы сохранить корректную локальную матрицу привязки к дому;
- заменяет объект в `house.AttachedDoodads`;
- удаляет только non-persistent placeholder, не затрагивая запись БД;
- сохраняет loaded `FuncGroupId`, поэтому открытая дверь/окно после рестарта остаётся только в сохранённой фазе.

`SpawnManager.SpawnPlayerDoodads()` теперь вызывает эту замену для housing doodads с `AttachPoint != None` независимо от `ParentObjId`.

## Защита на уровне БД

Добавлена миграция:

`SQL/updates/2026-04-24_aaemu_game_doodads_house_structural_unique.sql`

Миграция:

1. Удаляет уже накопленные дубликаты структурных doodads домов, оставляя минимальный `id` для каждой группы.
2. Добавляет generated column `structural_attach_point`.
3. Добавляет уникальный ключ `ux_doodads_house_structural_attach_point`.

Ограничение применяется только к структурным doodads домов:

```sql
owner_type = 3
house_id > 0
attach_point <> 0
```

Обычная мебель в доме не затрагивается, потому что она имеет `attach_point = 0`; generated column для неё равен `NULL`, а уникальный ключ допускает несколько `NULL`.

## Проверка дублей

Перед миграцией или после подозрительного рестарта можно проверить дубли:

```sql
SELECT
  house_id,
  attach_point,
  COUNT(*) AS duplicate_count,
  MIN(id) AS keep_id,
  GROUP_CONCAT(id ORDER BY id) AS doodad_ids
FROM doodads
WHERE owner_type = 3
  AND house_id > 0
  AND attach_point <> 0
GROUP BY house_id, attach_point
HAVING COUNT(*) > 1;
```

После применения миграции запрос должен возвращать пустой результат.

## Проверка после рестарта

1. Выполнить SQL-проверку дублей.
2. Запустить сервер.
3. Остановить сервер штатно.
4. Повторно выполнить SQL-проверку дублей.

Ожидаемый результат: число структурных doodads домов не увеличивается, запрос дублей остаётся пустым.

## Тесты

Добавлены xUnit-тесты:

`AAEmu.UnitTests/Game/Models/Game/Housing/HousingPersistenceTests.cs`

Покрытые сценарии:

- `CurrentStep_WhenLoadedFromDatabase_ShouldCreateNonPersistentBindingDoodads`
- `CurrentStep_WhenNotLoadedFromDatabase_ShouldCreatePersistentBindingDoodads`
- `ReplaceAttachedDoodadByAttachPoint_ShouldRemoveLoadedPlaceholderAndKeepPersistedState`

Команда проверки:

```powershell
dotnet test AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-restore
```

Ожидаемый результат на момент исправления: `198` passed, `0` failed.
