using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

/// <summary>
/// Explicit audit events (authentication, security, conversions). Entity create/update/delete
/// events are written automatically by ApplicationDbContext.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? recordId = null, object? details = null,
        bool success = true, string? userId = null, string? userName = null);

    Task<List<HistoryEntry>> GetHistoryAsync(string entityName, int recordId);
    Task<Dictionary<long, List<FieldChange>>> DescribeChangesAsync(IEnumerable<AuditLog> logs);
    Task<PagedList<AuditLog>> SearchAsync(AuditLogFilter filter);
    Task<List<string>> GetEntityNamesAsync();
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _accessor;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor accessor)
    {
        _db = db;
        _accessor = accessor;
    }

    public async Task LogAsync(string action, string entityName, string? recordId = null, object? details = null,
        bool success = true, string? userId = null, string? userName = null)
    {
        var ctx = _accessor.HttpContext;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = userName ?? ctx?.User.Identity?.Name,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            NewValue = details == null ? null : JsonSerializer.Serialize(details),
            Result = success ? "Success" : "Failure",
            CreatedDate = DateTime.UtcNow,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<HistoryEntry>> GetHistoryAsync(string entityName, int recordId)
    {
        var id = recordId.ToString();
        var logs = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == entityName && a.RecordId == id)
            .OrderByDescending(a => a.CreatedDate)
            .Take(50)
            .ToListAsync();
        var changes = await DescribeChangesAsync(logs);
        return logs.Select(l => new HistoryEntry(l, changes[l.AuditLogId])).ToList();
    }

    /// <summary>
    /// Turns stored JSON snapshots into readable field changes: friendly labels, enum names
    /// (including rows written before enums were stored by name), dates, money and user names.
    /// </summary>
    public async Task<Dictionary<long, List<FieldChange>>> DescribeChangesAsync(IEnumerable<AuditLog> logs)
    {
        var list = logs.ToList();
        var parsed = list.ToDictionary(l => l.AuditLogId, l => (Old: Parse(l.OldValue), New: Parse(l.NewValue)));

        var userIds = parsed.Values
            .SelectMany(p => p.Old.Concat(p.New))
            .Where(kv => UserFields.Contains(kv.Key) && kv.Value.ValueKind == JsonValueKind.String)
            .Select(kv => kv.Value.GetString()!)
            .Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        var result = new Dictionary<long, List<FieldChange>>();
        foreach (var log in list)
        {
            var (oldValues, newValues) = parsed[log.AuditLogId];
            var type = typeof(AuditLog).Assembly.GetType($"{typeof(AuditLog).Namespace}.{log.EntityName}");
            var changes = new List<FieldChange>();

            foreach (var key in oldValues.Keys.Union(newValues.Keys))
            {
                if (key.EndsWith("Code") && log.Action == AuditActions.Update) continue;
                var from = oldValues.TryGetValue(key, out var o) ? Format(type, key, o, names) : null;
                var to = newValues.TryGetValue(key, out var n) ? Format(type, key, n, names) : null;
                if (from == to) continue;
                if (log.Action is AuditActions.Create && to == null) continue;   // skip empty fields on create
                if (log.Action is AuditActions.Delete && from == null) continue;
                changes.Add(new FieldChange(Label(key), from, to));
            }
            result[log.AuditLogId] = changes;
        }
        return result;
    }

    private static readonly HashSet<string> UserFields = new() { "AssignedTo", "CreatedBy", "ModifiedBy", "ManagerId", "UserId" };

    private static Dictionary<string, JsonElement> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static string Label(string key)
    {
        var label = Regex.Replace(key, "(?<!^)([A-Z])", " $1");
        return label.EndsWith(" Id") && label != "Id" ? label[..^3] : label;
    }

    private static string? Format(Type? entityType, string key, JsonElement value, IReadOnlyDictionary<string, string> userNames)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;

        if (UserFields.Contains(key) && value.ValueKind == JsonValueKind.String)
            return userNames.GetValueOrDefault(value.GetString()!, "(removed user)");

        var propType = entityType?.GetProperty(key)?.PropertyType;
        if (propType != null) propType = Nullable.GetUnderlyingType(propType) ?? propType;

        if (propType is { IsEnum: true })
        {
            object? enumValue = value.ValueKind == JsonValueKind.Number
                ? Enum.ToObject(propType, value.GetInt32())
                : Enum.TryParse(propType, value.GetString(), out var parsedEnum) ? parsedEnum : null;
            return enumValue is Enum e ? e.DisplayName() : value.ToString();
        }

        if (propType == typeof(DateTime) && value.TryGetDateTime(out var dt))
        {
            if (dt.TimeOfDay == TimeSpan.Zero && dt.Kind != DateTimeKind.Utc) return Ui.Date(dt);   // date-only field
            return dt.Kind == DateTimeKind.Local ? dt.ToString("dd MMM yyyy, HH:mm") : Ui.Timestamp(dt);
        }

        if (propType == typeof(decimal) && value.TryGetDecimal(out var money)) return Ui.Money(money);
        if (key == "Probability") return value + "%";

        return value.ValueKind switch
        {
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.String => string.IsNullOrEmpty(value.GetString()) ? null : value.GetString(),
            _ => value.ToString()
        };
    }

    public async Task<PagedList<AuditLog>> SearchAsync(AuditLogFilter f)
    {
        var q = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.UserName)) q = q.Where(a => a.UserName != null && a.UserName.Contains(f.UserName.Trim()));
        if (!string.IsNullOrWhiteSpace(f.EntityName)) q = q.Where(a => a.EntityName == f.EntityName);
        if (!string.IsNullOrWhiteSpace(f.Action)) q = q.Where(a => a.Action == f.Action);
        if (f.From.HasValue)
        {
            var from = f.From.Value.Date.ToUniversalTime();
            q = q.Where(a => a.CreatedDate >= from);
        }
        if (f.To.HasValue)
        {
            var to = f.To.Value.Date.AddDays(1).ToUniversalTime();
            q = q.Where(a => a.CreatedDate < to);
        }
        return await PagedList<AuditLog>.CreateAsync(q.OrderByDescending(a => a.AuditLogId), f.Page, f.PageSize);
    }

    public Task<List<string>> GetEntityNamesAsync() =>
        _db.AuditLogs.Select(a => a.EntityName).Distinct().OrderBy(n => n).ToListAsync();
}

public record FieldChange(string Field, string? From, string? To);

public record HistoryEntry(AuditLog Log, IReadOnlyList<FieldChange> Changes);
