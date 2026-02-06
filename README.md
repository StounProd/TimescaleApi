# TimescaleApi

WebAPI-приложение для загрузки, обработки и анализа timescale-данных результатов обработки.

## Стек

- .NET 8
- ASP.NET Core WebAPI
- Entity Framework Core 8
- PostgreSQL (Npgsql)
- Swagger (Swashbuckle)
- xUnit + Testcontainers (интеграционные тесты)

## Структура проекта

```
TimescaleApi.sln
├── TimescaleApi/                    — WebAPI (контроллеры, middleware, Program.cs)
├── TimescaleApi.Application/        — бизнес-логика (парсер, агрегация, DTO, интерфейсы)
├── TimescaleApi.Domain/             — доменные сущности (ValueRecord, ResultRecord)
├── TimescaleApi.Infrastructure/     — реализация сервисов, EF Core DbContext, миграции
├── TimescaleApi.UnitTests/          — юнит-тесты (валидация, агрегация, сервисы)
└── TimescaleApi.IntegrationTests/   — интеграционные тесты (Testcontainers + PostgreSQL)
```

## Запуск

### Предварительные требования

- .NET 8 SDK
- PostgreSQL (по умолчанию `localhost:5432`, БД `timescale`, пользователь `postgres`/`postgres`)

### Применить миграции и запустить

```bash
dotnet ef database update --project TimescaleApi.Infrastructure --startup-project TimescaleApi
dotnet run --project TimescaleApi
```

Swagger UI доступен по адресу: `http://localhost:5091/swagger`

### Строка подключения

Настраивается в `TimescaleApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=timescale;Username=postgres;Password=postgres"
  }
}
```

## API

### 1. POST /api/import — загрузка CSV

Принимает `multipart/form-data` с полем `file` (CSV-файл).

Формат CSV:
```
Date;ExecutionTime;Value
2024-01-01T10:00:00.0000Z;1.5;42.7
```

Валидация:
- дата в диапазоне `[01.01.2000, текущая]`
- `ExecutionTime` ≥ 0
- `Value` ≥ 0
- от 1 до 10 000 строк
- все поля обязательны и должны соответствовать типам

При повторной загрузке файла с тем же именем данные перезаписываются.

### 2. GET /api/results — список результатов с фильтрами

Параметры (все опциональные):
- `fileName` — фильтр по имени файла
- `firstStartFrom` / `firstStartTo` — диапазон по времени первой операции
- `avgValueFrom` / `avgValueTo` — диапазон по среднему значению
- `avgExecutionTimeFrom` / `avgExecutionTimeTo` — диапазон по среднему времени выполнения
- `page`, `pageSize` — пагинация

### 3. GET /api/values/last10 — последние 10 значений

Параметры:
- `fileName` (обязательный) — имя файла

Возвращает последние 10 записей из таблицы Values, отсортированных по дате.

## Тесты

```bash
# Юнит-тесты (без внешних зависимостей)
dotnet test TimescaleApi.UnitTests

# Интеграционные тесты (требуется Docker)
dotnet test TimescaleApi.IntegrationTests
```
