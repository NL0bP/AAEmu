# 🚨 Руководство по откату миграции .NET 10.0

> **⚠️ ВАЖНО:** Этот документ предназначен для экстренного использования при критических проблемах в production. Сохраните его в безопасном месте и убедитесь, что вся команда знает о его существовании.

---

## 📋 Содержание

1. [Предварительные требования](#1-предварительные-требования)
2. [Быстрый откат (Emergency Rollback)](#2-быстрый-откат-emergency-rollback)
3. [Пошаговый откат вручную](#3-пошаговый-откат-вручную)
4. [Откат базы данных](#4-откат-базы-данных)
5. [Проверка после отката](#5-проверка-после-отката)
6. [Известные проблемы при откате](#6-известные-проблемы-при-откате)
7. [Превентивные меры](#7-превентивные-меры)
8. [Контакты экстренной помощи](#8-контакты-экстренной-помощи)

---

## 1. Предварительные требования

### 1.1. Что нужно иметь перед откатом

| Ресурс | Описание | Где находится |
|--------|----------|---------------|
| Git-репозиторий | Доступ к исходному коду | Локально / GitHub |
| Тег бэкапа | `before-dotnet-10-migration` или аналогичный | Git |
| Бэкап базы данных | Дамп БД на момент до миграции | `/backups/db/` |
| Бэкап конфигураций | Config.json, NLog.config | `/backups/configs/` |
| .NET 6.0 SDK | Для сборки старой версии | Установлен локально |
| Docker-образы | Бэкап старых образов | Docker Registry |

### 1.2. Быстрая проверка наличия бэкапов

```bash
# Проверить наличие Git-тега
git tag | grep before-dotnet-10-migration

# Проверить бэкапы базы данных
ls -la /backups/db/*.sql 2>/dev/null || echo "❌ Бэкапы БД не найдены!"

# Проверить установленные SDK
dotnet --list-sdks | grep "6\."
```

### 1.3. Обязательные бэкапы перед миграцией

Если миграция еще не выполнена, создайте эти бэкапы:

```bash
# 1. Git-тег
git tag before-dotnet-10-migration
git push origin before-dotnet-10-migration

# 2. Бэкап базы данных
mysqldump -u root -p aaemu > /backups/db/aaemu-before-migration-$(date +%Y%m%d).sql

# 3. Бэкап конфигураций
cp AAEmu.Login/Config.json /backups/configs/login-config-$(date +%Y%m%d).json
cp AAEmu.Game/Config.json /backups/configs/game-config-$(date +%Y%m%d).json
cp AAEmu.Login/NLog.config /backups/configs/nlog-login-$(date +%Y%m%d).config
cp AAEmu.Game/NLog.config /backups/configs/nlog-game-$(date +%Y%m%d).config

# 4. Docker-образы
docker save aaemu-login:latest > /backups/docker/login-before-migration.tar
docker save aaemu-game:latest > /backups/docker/game-before-migration.tar
```

---

## 2. Быстрый откат (Emergency Rollback)

> **⏱️ Время выполнения:** 5-15 минут  
> **⚠️ Риск:** Низкий (при наличии тега)  
> **📍 Когда использовать:** Критические ошибки в production

### 2.1. Git-based откат (РЕКОМЕНДУЕТСЯ)

```bash
# === ШАГ 1: Остановить сервисы ===
# Linux/macOS:
sudo systemctl stop aaemu-login aaemu-game
# Или Docker:
docker-compose down

# === ШАГ 2: Откат Git ===
# Вариант A: Полный откат к тегу (если нет важных изменений)
git checkout before-dotnet-10-migration

# Вариант B: Создать ветку отката (если нужно сохранить изменения)
git checkout -b rollback/dotnet-10-before-dotnet-10-migration

# === ШАГ 3: Очистка артефактов сборки ===
rm -rf AAEmu.*/bin AAEmu.*/obj
rm -rf publish/

# === ШАГ 4: Восстановление пакетов ===
dotnet restore

# === ШАГ 5: Сборка ===
dotnet build -c Release

# === ШАГ 6: Запуск ===
# Linux/macOS:
sudo systemctl start aaemu-login aaemu-game
# Или Docker:
docker-compose up -d
```

### 2.2. Быстрый откат Docker

```bash
# === ШАГ 1: Остановить контейнеры ===
docker-compose down

# === ШАГ 2: Откатить Git ===
git checkout before-dotnet-10-migration

# === ШАГ 3: Пересобрать образы ===
docker-compose build --no-cache

# === ШАГ 4: Запустить ===
docker-compose up -d

# === ШАГ 5: Проверить статус ===
docker-compose ps
docker-compose logs -f
```

### 2.3. Откат через Git revert (если миграция в main)

```bash
# Найти коммит миграции
COMMIT_HASH=$(git log --oneline --grep="migration\|dotnet 10\|.NET 10" -i | head -1 | cut -d' ' -f1)

# Откатить коммит миграции
git revert $COMMIT_HASH --no-edit

# Или откатить несколько коммитов
git revert HEAD~3..HEAD --no-edit

# Отправить изменения
git push origin main
```

---

## 3. Пошаговый откат вручную

> **⏱️ Время выполнения:** 30-60 минут  
> **⚠️ Риск:** Средний (возможны ошибки при ручном редактировании)  
> **📍 Когда использовать:** Git-история повреждена или нужен частичный откат

### 3.1. Откат файлов проектов

#### 3.1.1. global.json

```json
{
  "sdk": {
    "version": "6.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**Команда для отката:**
```bash
# Получить старую версию из Git
git show before-dotnet-10-migration:global.json > global.json
```

#### 3.1.2. Directory.Build.props (УДАЛИТЬ)

```bash
# Удалить новый файл
rm Directory.Build.props
rm Directory.Packages.props
```

**⚠️ ВНИМАНИЕ:** После удаления каждый .csproj должен иметь свои настройки TargetFramework.

#### 3.1.3. AAEmu.Commons.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <VersionPrefix>0.0.1</VersionPrefix>
    <VersionSuffix>alpha</VersionSuffix>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MySql.Data" Version="8.0.13" />
    <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
    <PackageReference Include="NLog" Version="4.5.11" />
    <PackageReference Include="NetCoreServer" Version="2.0.3" />
  </ItemGroup>

</Project>
```

#### 3.1.4. AAEmu.Game.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <VersionPrefix>0.0.2</VersionPrefix>
    <VersionSuffix>alpha</VersionSuffix>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <LangVersion>9.0</LangVersion>
    <RuntimeIdentifiers>
      win7-x64;win7-x86;win8-x64;win8-x86;win81-x64;win81-x86;
      win10-x64;win10-x86;centos.7-x64;debian.9-x64;ubuntu.18.04-x64;
      sles-x64;sles.12-x64;sles.12.1-x64;sles.12.2-x64;sles.12.3-x64;
      alpine-x64;alpine.3.7-x64
    </RuntimeIdentifiers>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AAEmu.Commons\AAEmu.Commons.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="NLog.config">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <None Remove="ExampleConfig.xml" />
    <Content Include="ExampleConfig.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="Configurations\AccessLevels.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <None Update="Configurations\Expedition.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="Configurations\World.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="Configurations\CharacterDeleteSettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="Configurations\ClientData.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <ItemGroup>
    <Compile Remove="Scripts\**\**" />
    <None Include="Scripts\**\**" CopyToOutputDirectory="PreserveNewest" LinkBase="Scripts\" />
    <None Include="Configs\**\**" CopyToOutputDirectory="PreserveNewest" LinkBase="Configs\" />
    <None Include="Data\**\**" CopyToOutputDirectory="PreserveNewest" LinkBase="Data\" />
    <None Include="ClientData\**\**" CopyToOutputDirectory="PreserveNewest" LinkBase="ClientData\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Discord.Net" Version="2.2.0" />
    <PackageReference Include="Jace" Version="0.9.2" />
    <PackageReference Include="JitterPhysics" Version="0.2.0.20" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="2.10.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="2.2.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.CommandLine" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="2.2.0" />
    <PackageReference Include="NLog" Version="4.5.11" />
    <PackageReference Include="NLua" Version="1.5.6" />
    <PackageReference Include="Quartz" Version="3.2.3" />
    <PackageReference Include="Ionic.Zlib" Version="1.9.1.5" />
    <PackageReference Include="System.Numerics.Vectors" Version="4.5.0" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="Models\ClientData" />
  </ItemGroup>

</Project>
```

#### 3.1.5. AAEmu.Login.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <VersionPrefix>0.0.2</VersionPrefix>
    <VersionSuffix>alpha</VersionSuffix>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AAEmu.Commons\AAEmu.Commons.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.CommandLine" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="2.2.0" />
    <PackageReference Include="NLog" Version="4.5.11" />
    <PackageReference Include="System.Runtime.Loader" Version="4.3.0" />
  </ItemGroup>

  <ItemGroup>
    <None Remove="ExampleConfig.json" />
    <Content Include="ExampleConfig.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <None Remove="NLog.config" />
    <Content Include="NLog.config">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

</Project>
```

#### 3.1.6. AAEmu.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <VersionPrefix>0.0.2</VersionPrefix>
    <VersionSuffix>alpha</VersionSuffix>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AAEmu.Commons\AAEmu.Commons.csproj" />
    <ProjectReference Include="..\AAEmu.Game\AAEmu.Game.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="1.3.0" />
    <PackageReference Include="coverlet.msbuild" Version="2.9.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="15.9.0" />
    <PackageReference Include="xunit" Version="2.4.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.2" />
  </ItemGroup>

</Project>
```

### 3.2. Откат исходного кода

#### 3.2.1. AAEmu.Commons/Network/Core/Session.cs

Проблема: В .NET 10 `RemoteEndPoint` имеет другой тип.

**Текущий код (.NET 10):**
```csharp
protected override void OnConnected()
{
    RemoteEndPoint = (IPEndPoint)Socket.RemoteEndPoint;
    // ...
}
```

**Код для .NET Core 3.1:**
```bash
# Получить старую версию
git show before-dotnet-10-migration:AAEmu.Commons/Network/Core/Session.cs > AAEmu.Commons/Network/Core/Session.cs
```

#### 3.2.2. AAEmu.Commons/Cryptography/EncryptionManager.cs

Проблема: `RijndaelManaged` → `Aes`

**Текущий код (.NET 10):**
```csharp
private static Aes CryptAes(byte[] aesKey, byte[] iv)
{
    var aes = Aes.Create();
    // ...
}
```

**Код для .NET Core 3.1:**
```bash
git show before-dotnet-10-migration:AAEmu.Commons/Cryptography/EncryptionManager.cs > AAEmu.Commons/Cryptography/EncryptionManager.cs
```

#### 3.2.3. AAEmu.Commons/Utils/AAPak/AAPak.cs

Проблема: 4 метода используют новый API `Aes`.

**Методы для отката:**
- `EncryptAES`
- `EncryptStreamAes`
- `EncryptStreamAESWithIV`
- `EncryptAESUsingIV`

```bash
git show before-dotnet-10-migration:AAEmu.Commons/Utils/AAPak/AAPak.cs > AAEmu.Commons/Utils/AAPak/AAPak.cs
```

#### 3.2.4. AAEmu.Commons/Utils/MersenneTwister.cs

Проблема: Метод `NextSingle()` добавлен в .NET 10.

**Текущий код (.NET 10):**
```csharp
public new Single NextSingle()
{
    return (Single) NextDouble();
}
```

**Для .NET Core 3.1:** Удалить или закомментировать метод `NextSingle()` - он не существует в старой версии.

```bash
git show before-dotnet-10-migration:AAEmu.Commons/Utils/MersenneTwister.cs > AAEmu.Commons/Utils/MersenneTwister.cs
```

#### 3.2.5. AAEmu.Commons/IO/FileManager.cs

Проблема: `Assembly.CodeBase` → `Assembly.Location`

**Текущий код (.NET 10):**
```csharp
_appPath = Path.GetDirectoryName(new Uri(assembly.Location).LocalPath);
```

**Код для .NET Core 3.1:**
```bash
git show before-dotnet-10-migration:AAEmu.Commons/IO/FileManager.cs > AAEmu.Commons/IO/FileManager.cs
```

#### 3.2.6. AAEmu.Game/Core/Packets/CompressedGamePackets.cs

Проблема: `Ionic.Zlib` → `ZLibStream`

**Текущий код (.NET 10):**
```csharp
using (var zlibStream = new ZLibStream(outputStream, CompressionMode.Compress, true))
{
    inputStream.CopyTo(zlibStream);
}
```

**Код для .NET Core 3.1:**
```bash
git show before-dotnet-10-migration:AAEmu.Game/Core/Packets/CompressedGamePackets.cs > AAEmu.Game/Core/Packets/CompressedGamePackets.cs
```

### 3.3. Откат Docker

#### 3.3.1. AAEmu.Login/Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:3.1-focal as builder
ARG CONFIGURATION
ARG RUNTIME

WORKDIR app
COPY ./AAEmu.Commons ./AAEmu.Commons
COPY ./AAEmu.Login ./AAEmu.Login
RUN dotnet publish ./AAEmu.Login/AAEmu.Login.csproj -c $CONFIGURATION -r $RUNTIME --self-contained true

FROM ubuntu:20.04
ARG CONFIGURATION
ARG FRAMEWORK
ARG RUNTIME
ARG DB_HOST
ARG DB_PORT
ARG DB_USER
ARG DB_PASSWORD

RUN apt update && apt install openssl -y
WORKDIR app
COPY --from=builder app/AAEmu.Login/bin/$CONFIGURATION/$FRAMEWORK/$RUNTIME/publish ./
RUN cp ./ExampleConfig.json ./Config.json
RUN sed -i "s/%db_host%/$DB_HOST/" ./Config.json
RUN sed -i "s/%db_port%/$DB_PORT/" ./Config.json
RUN sed -i "s/%db_user%/$DB_USER/" ./Config.json
RUN sed -i "s/%db_password%/$DB_PASSWORD/" ./Config.json

EXPOSE 1234 1237
ENTRYPOINT ["./AAEmu.Login"]
```

#### 3.3.2. AAEmu.Game/Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:3.1-focal as builder
ARG CONFIGURATION
ARG RUNTIME
ARG GAME_DB_URL

RUN apt update && apt install xz-utils -y
WORKDIR app
COPY ./AAEmu.Commons ./AAEmu.Commons
COPY ./AAEmu.Game ./AAEmu.Game
RUN test -f ./AAEmu.Game/Data/compact.sqlite3 || wget "$GAME_DB_URL" -qO - | tar -xJf - -C ./AAEmu.Game/Data
RUN dotnet publish ./AAEmu.Game/AAEmu.Game.csproj -c $CONFIGURATION -r $RUNTIME --self-contained true

FROM ubuntu:20.04
ARG CONFIGURATION
ARG FRAMEWORK
ARG RUNTIME
ARG LOGIN_HOST
ARG LOGIN_PORT
ARG DB_HOST
ARG DB_PORT
ARG DB_USER
ARG DB_PASSWORD

RUN apt update && apt install openssl -y
WORKDIR app
COPY --from=builder app/AAEmu.Game/bin/$CONFIGURATION/$FRAMEWORK/$RUNTIME/publish ./
RUN cp ./ExampleConfig.json ./Config.json
RUN sed -i "s/%login_host%/$LOGIN_HOST/" ./Config.json
RUN sed -i "s/%login_port%/$LOGIN_PORT/" ./Config.json
RUN sed -i "s/%db_host%/$DB_HOST/" ./Config.json
RUN sed -i "s/%db_port%/$DB_PORT/" ./Config.json
RUN sed -i "s/%db_user%/$DB_USER/" ./Config.json
RUN sed -i "s/%db_password%/$DB_PASSWORD/" ./Config.json

EXPOSE 1239 1250
ENTRYPOINT ["./AAEmu.Game"]
```

#### 3.3.3. .dockerignore

```bash
# Удалить новый файл
rm .dockerignore
```

### 3.4. Откат скриптов

#### 3.4.1. publish.sh

```bash
# Получить старую версию
git show before-dotnet-10-migration:publish.sh > publish.sh
chmod +x publish.sh
```

---

## 4. Откат базы данных

### 4.1. Схема БД

> **✅ Хорошая новость:** Миграция .NET 10.0 НЕ требует изменений в схеме базы данных. Все изменения только в коде приложения.

**Если были сделаны изменения в БД:**

```bash
# Восстановление из бэкапа
mysql -u root -p aaemu < /backups/db/aaemu-before-migration-YYYYMMDD.sql
```

### 4.2. Данные

```bash
# Бэкап текущих данных (на всякий случай)
mysqldump -u root -p aaemu > /backups/db/aaemu-emergency-backup-$(date +%Y%m%d-%H%M%S).sql

# Восстановление старых данных
mysql -u root -p aaemu < /backups/db/aaemu-before-migration-YYYYMMDD.sql
```

### 4.3. Проверка БД после отката

```sql
-- Проверить подключение
SELECT 1;

-- Проверить таблицы пользователей
SELECT COUNT(*) FROM users;

-- Проверить таблицы персонажей
SELECT COUNT(*) FROM characters;
```

---

## 5. Проверка после отката

### 5.1. Чек-лист проверок

#### 5.1.1. Сборка

```bash
# Очистка
dotnet clean

# Восстановление пакетов
dotnet restore

# Сборка
dotnet build -c Release

# Проверка на ошибки
if [ $? -eq 0 ]; then
    echo "✅ Сборка успешна"
else
    echo "❌ Ошибка сборки"
    exit 1
fi
```

#### 5.1.2. Тесты

```bash
# Запуск тестов
dotnet test AAEmu.Tests

# Проверка результатов
if [ $? -eq 0 ]; then
    echo "✅ Тесты пройдены"
else
    echo "⚠️ Некоторые тесты не пройдены"
fi
```

#### 5.1.3. Запуск сервисов

```bash
# Login Server
cd AAEmu.Login
dotnet run --configuration Release &
LOGIN_PID=$!
sleep 5

# Проверка процесса
if ps -p $LOGIN_PID > /dev/null; then
    echo "✅ Login Server запущен (PID: $LOGIN_PID)"
else
    echo "❌ Login Server не запустился"
    exit 1
fi

# Game Server
cd ../AAEmu.Game
dotnet run --configuration Release &
GAME_PID=$!
sleep 10

# Проверка процесса
if ps -p $GAME_PID > /dev/null; then
    echo "✅ Game Server запущен (PID: $GAME_PID)"
else
    echo "❌ Game Server не запустился"
    kill $LOGIN_PID 2>/dev/null
    exit 1
fi
```

#### 5.1.4. Проверка API

```bash
# Проверка порта Login Server
nc -zv localhost 1234
nc -zv localhost 1237

# Проверка порта Game Server
nc -zv localhost 1239
nc -zv localhost 1250
```

### 5.2. Полный чек-лист

| Проверка | Ожидаемый результат | Статус |
|----------|---------------------|--------|
| Сборка без ошибок | ✅ Нет ошибок | [ ] |
| Тесты | ✅ Все пройдены или допустимые ошибки | [ ] |
| Login Server запуск | ✅ Без исключений | [ ] |
| Game Server запуск | ✅ Без исключений | [ ] |
| Подключение к БД | ✅ Успешное подключение | [ ] |
| Порты прослушиваются | ✅ 1234, 1237, 1239, 1250 | [ ] |
| Логирование | ✅ Файлы создаются | [ ] |
| Конфигурации | ✅ Загружаются без ошибок | [ ] |

---

## 6. Известные проблемы при откате

### 6.1. Проблема: Не найден SDK 6.0

**Симптом:**
```
Could not execute because the specified command or file was not found.
The .NET SDK 6.0.100 was not found.
```

**Решение:**
```bash
# Установить .NET 6.0 SDK
# Windows:
winget install Microsoft.DotNet.SDK.6

# Linux (Ubuntu):
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-6.0

# macOS:
brew install dotnet@6
```

### 6.2. Проблема: Конфликты NuGet пакетов

**Симптом:**
```
error NU1605: Detected package downgrade
```

**Решение:**
```bash
# Очистка кэша NuGet
dotnet nuget locals all --clear

# Восстановление
dotnet restore --force
```

### 6.3. Проблема: Ошибки компиляции после отката

**Симптом:**
```
error CS1061: 'Type' does not contain a definition for 'Method'
```

**Решение:**
```bash
# Полная очистка
rm -rf AAEmu.*/bin AAEmu.*/obj
rm -rf ~/.nuget/packages/*
dotnet restore
dotnet build
```

### 6.4. Проблема: Docker образы не собираются

**Симптом:**
```
manifest for mcr.microsoft.com/dotnet/sdk:3.1-focal not found
```

**Решение:**
```bash
# Обновить Docker
sudo apt-get update
sudo apt-get install docker-ce docker-ce-cli containerd.io

# Или использовать старый образ
# Заменить в Dockerfile:
# FROM mcr.microsoft.com/dotnet/sdk:3.1-focal
# на:
# FROM mcr.microsoft.com/dotnet/sdk:3.1
```

### 6.5. Проблема: NLog не работает

**Симптом:**
```
NLog configuration error
```

**Решение:**
```bash
# Восстановить старый NLog.config
git show before-dotnet-10-migration:AAEmu.Login/NLog.config > AAEmu.Login/NLog.config
git show before-dotnet-10-migration:AAEmu.Game/NLog.config > AAEmu.Game/NLog.config
```

### 6.6. Проблема: База данных несовместима

**Симптом:**
```
MySqlException: Table 'aaemu.users' doesn't exist
```

**Решение:**
```bash
# Восстановить из бэкапа
mysql -u root -p aaemu < /backups/db/aaemu-before-migration-YYYYMMDD.sql
```

---

## 7. Превентивные меры

### 7.1. Подготовка к откату заранее

#### 7.1.1. Feature Flags

Внедрите feature flags для критических изменений:

```csharp
// Пример feature flag
public static class FeatureFlags
{
    public static bool UseDotNet10 { get; } = 
        Environment.GetEnvironmentVariable("USE_DOTNET_10") == "true";
}
```

#### 7.1.2. Canary Deployment

```yaml
# docker-compose.canary.yaml
version: '3.8'
services:
  login-canary:
    image: aaemu-login:dotnet10-canary
    ports:
      - "2234:1234"  # Другой порт
    environment:
      - CANARY=true
    
  game-canary:
    image: aaemu-game:dotnet10-canary
    ports:
      - "2239:1239"  # Другой порт
    environment:
      - CANARY=true
```

#### 7.1.3. Blue-Green Deployment

```bash
# Скрипт для blue-green deployment
#!/bin/bash

# Запустить green версию
docker-compose -f docker-compose.yaml -f docker-compose.green.yaml up -d

# Проверить health check
sleep 30
curl -f http://localhost:8080/health || exit 1

# Переключить трафик
docker-compose exec nginx nginx -s reload

# Остановить blue версию
docker-compose stop login-blue game-blue
```

### 7.2. Автоматические бэкапы

#### 7.2.1. Git pre-push hook

```bash
#!/bin/bash
# .git/hooks/pre-push

echo "🔄 Создание бэкапа перед push..."
git tag backup-before-push-$(date +%Y%m%d-%H%M%S)
exit 0
```

#### 7.2.2. Автоматический бэкап БД

```bash
#!/bin/bash
# backup-db.sh

BACKUP_DIR="/backups/db"
DATE=$(date +%Y%m%d-%H%M%S)

mysqldump -u root -p$DB_PASSWORD aaemu > $BACKUP_DIR/aaemu-auto-$DATE.sql

# Удалить старые бэкапы (оставить последние 7)
ls -t $BACKUP_DIR/aaemu-auto-*.sql | tail -n +8 | xargs -r rm
```

### 7.3. Мониторинг

#### 7.3.1. Health Checks

```csharp
// Добавить в Startup/Program
public class HealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Проверка подключения к БД
        // Проверка доступности сервисов
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
```

#### 7.3.2. Alerting

```yaml
# alerting-rules.yaml
groups:
- name: aaemu
  rules:
  - alert: AAEmuServiceDown
    expr: up{job="aaemu"} == 0
    for: 1m
    labels:
      severity: critical
    annotations:
      summary: "AAEmu service is down"
```

---

## 8. Контакты экстренной помощи

### 8.1. Команда

| Роль | Имя | Контакт | Время ответа |
|------|-----|---------|--------------|
| Tech Lead | [Имя] | [Email/Phone] | 24/7 |
| DevOps | [Имя] | [Email/Phone] | 24/7 |
| DBA | [Имя] | [Email/Phone] | Рабочее время |
| QA Lead | [Имя] | [Email/Phone] | Рабочее время |

### 8.2. Экстренные контакты

| Сервис | Контакт | Назначение |
|--------|---------|------------|
| Git Hosting | support@github.com | Проблемы с репозиторием |
| Docker Hub | support@docker.com | Проблемы с образами |
| NuGet | nugetgallery@microsoft.com | Проблемы с пакетами |
| MySQL | bugs.mysql.com | Проблемы с БД |

### 8.3. Полезные ресурсы

- **Документация .NET:** https://docs.microsoft.com/dotnet/
- **NLog Wiki:** https://github.com/NLog/NLog/wiki
- **MySQL Connector:** https://dev.mysql.com/doc/connector-net/en/
- **Docker Docs:** https://docs.docker.com/

---

## 9. Чек-лист экстренного отката

Используйте этот чек-лист при экстренном откате:

```
□ Остановить production сервисы
□ Уведомить команду об откате
□ Выполнить Git-based откат
□ Проверить версию кода
□ Очистить артефакты сборки
► Восстановить пакеты NuGet
► Выполнить сборку
► Проверить тесты
► Запустить сервисы
► Проверить логи
► Проверить подключение к БД
► Проверить порты
□ Уведомить о завершении отката
□ Создать post-mortem
```

---

## 10. История изменений

| Версия | Дата | Автор | Изменения |
|--------|------|-------|-----------|
| 1.0 | 2026-03-01 | Kilo Code | Начальная версия |

---

> **⚠️ ВАЖНО:** Этот документ должен регулярно обновляться по мере изменения архитектуры проекта. Последнее обновление: 2026-03-01
