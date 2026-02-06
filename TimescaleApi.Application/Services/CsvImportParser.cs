using System.Globalization;
using System.Text;
using TimescaleApi.Application.Exceptions;
using TimescaleApi.Domain.Entities;

namespace TimescaleApi.Application.Services;

public class CsvImportParser : ICsvImportParser
{
    private static readonly DateTime MinDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-ddTHH:mm:ss.ffff'Z'",
        "yyyy-MM-ddTHH:mm:ss.FFFF'Z'",
        "yyyy-MM-ddTHH:mm:ss.FFF'Z'",
        "O"
    ];

    public async Task<IReadOnlyList<ValueRecord>> ParseAsync(Stream csvStream, string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("Имя файла не задано.");
        }

        var values = new List<ValueRecord>();
        var errors = new List<string>();
        var utcNow = DateTime.UtcNow;
        var lineNumber = 0;
        var headerChecked = false;

        using var reader = new StreamReader(csvStream, Encoding.UTF8, true, 1024, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (!headerChecked)
            {
                headerChecked = true;
                if (line.Trim().StartsWith("Date;", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                errors.Add($"Строка {lineNumber}: пустая строка недопустима.");
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length != 3)
            {
                errors.Add($"Строка {lineNumber}: ожидается 3 поля, получено {parts.Length}.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
            {
                errors.Add($"Строка {lineNumber}: отсутствуют значения.");
                continue;
            }

            if (!TryParseDate(parts[0], out var date))
            {
                errors.Add($"Строка {lineNumber}: неверный формат даты.");
                continue;
            }

            if (date < MinDate || date > utcNow)
            {
                errors.Add($"Строка {lineNumber}: дата вне допустимого диапазона.");
            }

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var executionTime))
            {
                errors.Add($"Строка {lineNumber}: неверный формат времени выполнения.");
                continue;
            }

            if (executionTime < 0)
            {
                errors.Add($"Строка {lineNumber}: время выполнения меньше 0.");
            }

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                errors.Add($"Строка {lineNumber}: неверный формат значения.");
                continue;
            }

            if (value < 0)
            {
                errors.Add($"Строка {lineNumber}: значение меньше 0.");
            }

            if (errors.Count > 0)
            {
                continue;
            }

            values.Add(new ValueRecord
            {
                FileName = fileName,
                Date = date,
                ExecutionTimeSeconds = executionTime,
                Value = value
            });

            if (values.Count > 10000)
            {
                throw new ValidationException("Количество строк больше 10000.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (values.Count == 0)
        {
            throw new ValidationException("Файл не содержит строк.");
        }

        return values;
    }

    private static bool TryParseDate(string input, out DateTime date)
    {
        if (DateTime.TryParseExact(input, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            date = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        date = default;
        return false;
    }
}
