#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Update;

/// <summary>What a fold of the log's lines says about one <c>updateId</c>, read only from the three fields
/// every schema version carries (contract § Update log migration note) — never a full <see cref="UpdateRecord"/>
/// deserialization, so an unknown <c>recordSchemaVersion</c> does not itself make a line unreadable.</summary>
public sealed record LogRecordSummary(Guid UpdateId, string ContainerName, bool HasOutcome);

/// <summary>The result of reading the whole log once. <see cref="IsCorrupt"/> is set by a line that cannot be
/// parsed at all (S2.19) — it makes every other field meaningless, because that state refuses every update on
/// the host until an operator clears it, not just updates on one container.</summary>
public sealed record UpdateLogState(bool IsCorrupt, int? CorruptLineNumber, IReadOnlyList<LogRecordSummary> OpenRecords)
{
    public static UpdateLogState Corrupt(int lineNumber1Based) => new(true, lineNumber1Based, Array.Empty<LogRecordSummary>());

    public static UpdateLogState Ok(IReadOnlyList<LogRecordSummary> latestPerUpdateId) =>
        new(false, null, latestPerUpdateId.Where(r => !r.HasOutcome).ToList());
}

/// <summary>The per-user append-only update log (contract § Update log). One JSON object per line, flushed and
/// synced before the first change to the target (I36). The later line for an <c>updateId</c> wins.</summary>
public sealed class UpdateLogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Path { get; }

    public UpdateLogStore(string? pathOverride = null)
    {
        Path = pathOverride ?? ResolveDefaultPath();
    }

    public static string ResolveDefaultPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(localAppData, "Docker-BuildAgent", "updates.jsonl");
        }

        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrEmpty(stateHome))
        {
            stateHome = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        }

        return System.IO.Path.Combine(stateHome, "docker-buildagent", "updates.jsonl");
    }

    /// <summary>Appends one line, flushed and fsync'd before returning. Throws when the write cannot be made
    /// durable, for the caller to raise as <see cref="UpdateErrorCode.LogUnwritable"/> (I36).</summary>
    public void Append(UpdateRecord record)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(record, SerializerOptions);
        using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
        var bytes = new UTF8Encoding(false).GetBytes(json + "\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    /// <summary>Reads every line, folding repeated <c>updateId</c>s to their latest line, without ever binding
    /// the full record type — a record at an unknown schema version is still read for the three fields residue
    /// detection needs (contract § Update log migration note).</summary>
    public UpdateLogState ReadState()
    {
        if (!File.Exists(Path))
        {
            return UpdateLogState.Ok(Array.Empty<LogRecordSummary>());
        }

        var latestByUpdateId = new Dictionary<Guid, LogRecordSummary>();
        var lines = File.ReadAllLines(Path);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var summary = TryReadSummary(line);
            if (summary == null)
            {
                return UpdateLogState.Corrupt(i + 1);
            }

            latestByUpdateId[summary.UpdateId] = summary;
        }

        return UpdateLogState.Ok(latestByUpdateId.Values.ToList());
    }

    private static LogRecordSummary? TryReadSummary(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("updateId", out var idProperty) ||
                !root.TryGetProperty("containerName", out var nameProperty) ||
                idProperty.ValueKind != JsonValueKind.String ||
                nameProperty.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var hasOutcome = root.TryGetProperty("outcome", out var outcomeProperty) &&
                              outcomeProperty.ValueKind != JsonValueKind.Null;

            return new LogRecordSummary(Guid.Parse(idProperty.GetString()!), nameProperty.GetString()!, hasOutcome);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
