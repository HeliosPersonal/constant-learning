using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;
using NpgsqlTypes;

namespace ConstantLearning.Features.Content.Search.Forms;

public sealed record PolishMorphologyImportResult(string Source, long Forms, bool Changed);

/// <summary>Завантажує локальний UTF-8 PoliMorf TSV: форма, лема, граматичний тег; решта колонок необов'язкова.</summary>
public sealed class PolishMorphologyImporter(AppDbContext db, ContentPublicationService publication,
    SearchFormsProjection projection, IClock clock, ILogger<PolishMorphologyImporter> logger)
{
    public async Task<PolishMorphologyImportResult> ImportAsync(string path, string? expectedSha256 = null,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Імпорт морфології має власну транзакцію.");
        await using var file = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
        if (expectedSha256 is not null && !string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Контрольна сума польського лексикону не збігається.");
        var source = "polimorf:" + hash[..20];
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // PoliMorf — польський лексикон; код мови беремо з профілю нормалізації, а не з літерала.
            var lang = NormalizationProfiles.Polish.Code;
            await publication.AcquireLockAsync(cancellationToken);
            var existing = await db.MorphForms.LongCountAsync(form => form.Lang == lang && form.Source == source, cancellationToken);
            if (existing > 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(source, existing, false);
            }
            await db.MorphForms.Where(form => form.Lang == lang && form.Source.StartsWith("polimorf:"))
                .ExecuteDeleteAsync(cancellationToken);
            file.Position = 0;
            await using Stream input = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? new GZipStream(file, CompressionMode.Decompress, leaveOpen: true) : file;
            using var reader = new StreamReader(input, new UTF8Encoding(false, true));
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            long count = 0;
            await using (var writer = await connection.BeginBinaryImportAsync(
                "COPY morph_forms (id, lang, source, lemma, form, tags, created_at) FROM STDIN (FORMAT BINARY)", cancellationToken))
            {
                var now = clock.GetCurrentInstant().ToDateTimeUtc();
                long lineNumber = 0;
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    lineNumber++;
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    var fields = line.Split('\t');
                    if (fields.Length < 3) throw new InvalidDataException($"Некоректний TSV-рядок морфології {lineNumber}.");
                    var form = fields[0].Trim().Normalize();
                    // Суфікс після двокрапки у Morfeusz позначає омонім, а не частину написання леми.
                    var lemma = SearchTextNormalizer.Normalize(fields[1].Split(':')[0]);
                    var tags = fields[2].Trim();
                    if (form.Length is 0 or > 200 || lemma.Length is 0 or > 200 || tags.Length is 0 or > 64)
                        throw new InvalidDataException($"Поля морфології виходять за допустимі межі, рядок {lineNumber}.");
                    await writer.StartRowAsync(cancellationToken);
                    await writer.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid, cancellationToken);
                    await writer.WriteAsync(lang, NpgsqlDbType.Text, cancellationToken);
                    await writer.WriteAsync(source, NpgsqlDbType.Text, cancellationToken);
                    await writer.WriteAsync(lemma, NpgsqlDbType.Text, cancellationToken);
                    await writer.WriteAsync(form, NpgsqlDbType.Text, cancellationToken);
                    await writer.WriteAsync(tags, NpgsqlDbType.Text, cancellationToken);
                    await writer.WriteAsync(now, NpgsqlDbType.TimestampTz, cancellationToken);
                    count++;
                }
                if (count == 0) throw new InvalidDataException("Польський лексикон порожній.");
                await writer.CompleteAsync(cancellationToken);
            }
            await projection.RebuildInTransactionAsync(onlyMissing: false, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Імпортовано польську морфологію {Source}: {Forms} рядків", source, count);
            return new(source, count, true);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
