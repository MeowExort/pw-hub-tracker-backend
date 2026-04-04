using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController(TrackerDbContext db) : ControllerBase
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "hp", "defense", "resistance", "damage", "damageMagic",
        "attackDegree", "defendDegree", "vigour",
        "antiDefenseDegree", "antiResistanceDegree", "peakGrade"
    };

    private static string NormalizeHomoglyphs(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'a' => 'а', 'e' => 'е', 'o' => 'о', 'c' => 'с',
                'p' => 'р', 'x' => 'х', 'y' => 'у', 'k' => 'к',
                'A' => 'А', 'E' => 'Е', 'O' => 'О', 'C' => 'С',
                'P' => 'Р', 'X' => 'Х', 'Y' => 'У', 'K' => 'К',
                'H' => 'Н', 'B' => 'В', 'M' => 'М', 'T' => 'Т',
                'h' => 'н', 'b' => 'в', 'm' => 'м', 't' => 'т',
                _ => chars[i]
            };
        }
        return new string(chars);
    }

    private static readonly string LatinChars = "aeopcxykhbmtAEOPCXYKHBMT";
    private static readonly string CyrillicChars = "аеорсхукнвмтАЕОРСХУКНВМТ";

    [HttpGet]
    public async Task<IActionResult> GetPlayers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? server = null,
        [FromQuery] int? cls = null,
        [FromQuery] string sortBy = "hp",
        [FromQuery] string sortOrder = "desc",
        [FromQuery] long? hpMin = null,
        [FromQuery] long? hpMax = null,
        [FromQuery] long? defenseMin = null,
        [FromQuery] long? defenseMax = null,
        [FromQuery] long? resistanceMin = null,
        [FromQuery] long? resistanceMax = null,
        [FromQuery] long? damageLowMin = null,
        [FromQuery] long? damageHighMax = null,
        [FromQuery] long? damageMagicLowMin = null,
        [FromQuery] long? damageMagicHighMax = null,
        [FromQuery] int? attackDegreeMin = null,
        [FromQuery] int? attackDegreeMax = null,
        [FromQuery] int? defendDegreeMin = null,
        [FromQuery] int? defendDegreeMax = null,
        [FromQuery] long? vigourMin = null,
        [FromQuery] long? vigourMax = null,
        [FromQuery] int? antiDefenseDegreeMin = null,
        [FromQuery] int? antiDefenseDegreeMax = null,
        [FromQuery] int? antiResistanceDegreeMin = null,
        [FromQuery] int? antiResistanceDegreeMax = null,
        [FromQuery] int? peakGradeMin = null,
        [FromQuery] int? peakGradeMax = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (!AllowedSortFields.Contains(sortBy)) sortBy = "hp";
        var isDesc = !sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

        var parameters = new List<object>();
        var conditions = new List<string>();
        var paramIndex = 0;

        // Base query with LEFT JOINs using MAX-aggregated history
        var sql = """
            SELECT
                p."Id",
                p."Name",
                p."Cls",
                p."Server",
                tm."TeamId",
                t."Name" AS "TeamName",
                pp."Hp",
                pp."Mp",
                pp."DamageLow",
                pp."DamageHigh",
                pp."DamageMagicLow",
                pp."DamageMagicHigh",
                pp."Defense",
                pp."Resistance",
                pp."AttackDegree",
                pp."DefendDegree",
                pp."Vigour",
                pp."AntiDefenseDegree",
                pp."AntiResistanceDegree",
                pp."PeakGrade",
                pp."UpdatedAt" AS "PropertiesUpdatedAt"
            FROM players p
            LEFT JOIN LATERAL (
                SELECT
                    MAX(h."Hp") AS "Hp",
                    MAX(h."Mp") AS "Mp",
                    MAX(h."DamageLow") AS "DamageLow",
                    MAX(h."DamageHigh") AS "DamageHigh",
                    MAX(h."DamageMagicLow") AS "DamageMagicLow",
                    MAX(h."DamageMagicHigh") AS "DamageMagicHigh",
                    MAX(h."Defense") AS "Defense",
                    (SELECT h2."Resistance" FROM player_property_history h2
                     WHERE h2."PlayerId" = p."Id" AND h2."Server" = p."Server"
                     ORDER BY h2."RecordedAt" DESC LIMIT 1) AS "Resistance",
                    MAX(h."AttackDegree") AS "AttackDegree",
                    MAX(h."DefendDegree") AS "DefendDegree",
                    MAX(h."Vigour") AS "Vigour",
                    MAX(h."AntiDefenseDegree") AS "AntiDefenseDegree",
                    MAX(h."AntiResistanceDegree") AS "AntiResistanceDegree",
                    MAX(h."PeakGrade") AS "PeakGrade",
                    MAX(h."RecordedAt") AS "UpdatedAt"
                FROM player_property_history h
                WHERE h."PlayerId" = p."Id" AND h."Server" = p."Server"
                HAVING COUNT(*) > 0
            ) pp ON true
            LEFT JOIN arena_team_members tm ON tm."PlayerId" = p."Id" AND tm."PlayerServer" = p."Server"
            LEFT JOIN arena_teams t ON t."Id" = tm."TeamId"
            """;

        // Server filter
        if (!string.IsNullOrWhiteSpace(server))
        {
            conditions.Add($"p.\"Server\" = @p{paramIndex}");
            parameters.Add(new Npgsql.NpgsqlParameter($"p{paramIndex}", server));
            paramIndex++;
        }

        // Class filter
        if (cls.HasValue)
        {
            conditions.Add($"p.\"Cls\" = @p{paramIndex}");
            parameters.Add(new Npgsql.NpgsqlParameter($"p{paramIndex}", cls.Value));
            paramIndex++;
        }

        // Fuzzy search with homoglyph normalization using PostgreSQL translate()
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = NormalizeHomoglyphs(search).ToLower();
            conditions.Add($"lower(translate(p.\"Name\", '{LatinChars}', '{CyrillicChars}')) LIKE @p{paramIndex}");
            parameters.Add(new Npgsql.NpgsqlParameter($"p{paramIndex}", $"%{normalizedSearch}%"));
            paramIndex++;
        }

        // Property filters
        void AddFilter(string column, string op, object value)
        {
            conditions.Add($"pp.\"{column}\" {op} @p{paramIndex}");
            parameters.Add(new Npgsql.NpgsqlParameter($"p{paramIndex}", value));
            paramIndex++;
        }

        if (hpMin.HasValue) AddFilter("Hp", ">=", hpMin.Value);
        if (hpMax.HasValue) AddFilter("Hp", "<=", hpMax.Value);
        if (defenseMin.HasValue) AddFilter("Defense", ">=", defenseMin.Value);
        if (defenseMax.HasValue) AddFilter("Defense", "<=", defenseMax.Value);
        if (damageLowMin.HasValue) AddFilter("DamageLow", ">=", damageLowMin.Value);
        if (damageHighMax.HasValue) AddFilter("DamageHigh", "<=", damageHighMax.Value);
        if (damageMagicLowMin.HasValue) AddFilter("DamageMagicLow", ">=", damageMagicLowMin.Value);
        if (damageMagicHighMax.HasValue) AddFilter("DamageMagicHigh", "<=", damageMagicHighMax.Value);
        if (attackDegreeMin.HasValue) AddFilter("AttackDegree", ">=", attackDegreeMin.Value);
        if (attackDegreeMax.HasValue) AddFilter("AttackDegree", "<=", attackDegreeMax.Value);
        if (defendDegreeMin.HasValue) AddFilter("DefendDegree", ">=", defendDegreeMin.Value);
        if (defendDegreeMax.HasValue) AddFilter("DefendDegree", "<=", defendDegreeMax.Value);
        if (vigourMin.HasValue) AddFilter("Vigour", ">=", vigourMin.Value);
        if (vigourMax.HasValue) AddFilter("Vigour", "<=", vigourMax.Value);
        if (antiDefenseDegreeMin.HasValue) AddFilter("AntiDefenseDegree", ">=", antiDefenseDegreeMin.Value);
        if (antiDefenseDegreeMax.HasValue) AddFilter("AntiDefenseDegree", "<=", antiDefenseDegreeMax.Value);
        if (antiResistanceDegreeMin.HasValue) AddFilter("AntiResistanceDegree", ">=", antiResistanceDegreeMin.Value);
        if (antiResistanceDegreeMax.HasValue) AddFilter("AntiResistanceDegree", "<=", antiResistanceDegreeMax.Value);
        if (peakGradeMin.HasValue) AddFilter("PeakGrade", ">=", peakGradeMin.Value);
        if (peakGradeMax.HasValue) AddFilter("PeakGrade", "<=", peakGradeMax.Value);

        // Resistance filter by max element in array
        if (resistanceMin.HasValue)
        {
            conditions.Add($"(SELECT MAX(r) FROM unnest(pp.\"Resistance\") AS r) >= @p{paramIndex}");
            parameters.Add(new Npgsql.NpgsqlParameter($"p{paramIndex}", resistanceMin.Value));
            paramIndex++;
        }
        if (resistanceMax.HasValue)
        {
            conditions.Add($"(SELECT MAX(r) FROM unnest(pp.\"Resistance\") AS r) <= @p{paramIndex}");
            parameters.Add(new Npgsql.NpgsqlParameter($"p{paramIndex}", resistanceMax.Value));
            paramIndex++;
        }

        var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";

        // Sort expression
        var sortExpr = sortBy.ToLower() switch
        {
            "hp" => "pp.\"Hp\"",
            "defense" => "pp.\"Defense\"",
            "resistance" => "(SELECT MAX(r) FROM unnest(pp.\"Resistance\") AS r)",
            "damage" => "(pp.\"DamageLow\" + pp.\"DamageHigh\") / 2.0",
            "damagemagic" => "(pp.\"DamageMagicLow\" + pp.\"DamageMagicHigh\") / 2.0",
            "attackdegree" => "pp.\"AttackDegree\"",
            "defenddegree" => "pp.\"DefendDegree\"",
            "vigour" => "pp.\"Vigour\"",
            "antidefensedegree" => "pp.\"AntiDefenseDegree\"",
            "antiresistancedegree" => "pp.\"AntiResistanceDegree\"",
            "peakgrade" => "pp.\"PeakGrade\"",
            _ => "pp.\"Hp\""
        };

        var direction = isDesc ? "DESC NULLS LAST" : "ASC NULLS LAST";

        // Count query
        var countSql = $@"SELECT COUNT(*) FROM players p
            LEFT JOIN LATERAL (
                SELECT
                    MAX(h.""Hp"") AS ""Hp"", MAX(h.""Mp"") AS ""Mp"",
                    MAX(h.""DamageLow"") AS ""DamageLow"", MAX(h.""DamageHigh"") AS ""DamageHigh"",
                    MAX(h.""DamageMagicLow"") AS ""DamageMagicLow"", MAX(h.""DamageMagicHigh"") AS ""DamageMagicHigh"",
                    MAX(h.""Defense"") AS ""Defense"",
                    (SELECT h2.""Resistance"" FROM player_property_history h2
                     WHERE h2.""PlayerId"" = p.""Id"" AND h2.""Server"" = p.""Server""
                     ORDER BY h2.""RecordedAt"" DESC LIMIT 1) AS ""Resistance"",
                    MAX(h.""AttackDegree"") AS ""AttackDegree"", MAX(h.""DefendDegree"") AS ""DefendDegree"",
                    MAX(h.""Vigour"") AS ""Vigour"",
                    MAX(h.""AntiDefenseDegree"") AS ""AntiDefenseDegree"",
                    MAX(h.""AntiResistanceDegree"") AS ""AntiResistanceDegree"",
                    MAX(h.""PeakGrade"") AS ""PeakGrade"",
                    MAX(h.""RecordedAt"") AS ""UpdatedAt""
                FROM player_property_history h
                WHERE h.""PlayerId"" = p.""Id"" AND h.""Server"" = p.""Server""
                HAVING COUNT(*) > 0
            ) pp ON true
            LEFT JOIN arena_team_members tm ON tm.""PlayerId"" = p.""Id"" AND tm.""PlayerServer"" = p.""Server""
            LEFT JOIN arena_teams t ON t.""Id"" = tm.""TeamId""{whereClause}";

        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = countSql;
        foreach (var p in parameters)
            countCmd.Parameters.Add(CloneParameter((Npgsql.NpgsqlParameter)p));
        var total = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        // Data query
        var dataSql = $"{sql}{whereClause} ORDER BY {sortExpr} {direction} LIMIT @pLimit OFFSET @pOffset";

        await using var dataCmd = connection.CreateCommand();
        dataCmd.CommandText = dataSql;
        foreach (var p in parameters)
            dataCmd.Parameters.Add(CloneParameter((Npgsql.NpgsqlParameter)p));
        dataCmd.Parameters.Add(new Npgsql.NpgsqlParameter("pLimit", pageSize));
        dataCmd.Parameters.Add(new Npgsql.NpgsqlParameter("pOffset", (page - 1) * pageSize));

        var items = new List<object>();
        await using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var hasProperties = !reader.IsDBNull(reader.GetOrdinal("Hp"));
            object? properties = null;
            if (hasProperties)
            {
                properties = new
                {
                    hp = reader.GetInt64(reader.GetOrdinal("Hp")),
                    mp = reader.GetInt64(reader.GetOrdinal("Mp")),
                    damageLow = reader.GetInt64(reader.GetOrdinal("DamageLow")),
                    damageHigh = reader.GetInt64(reader.GetOrdinal("DamageHigh")),
                    damageMagicLow = reader.GetInt64(reader.GetOrdinal("DamageMagicLow")),
                    damageMagicHigh = reader.GetInt64(reader.GetOrdinal("DamageMagicHigh")),
                    defense = reader.GetInt64(reader.GetOrdinal("Defense")),
                    resistance = reader.IsDBNull(reader.GetOrdinal("Resistance"))
                        ? Array.Empty<long>()
                        : (long[])reader.GetValue(reader.GetOrdinal("Resistance")),
                    attackDegree = reader.GetInt32(reader.GetOrdinal("AttackDegree")),
                    defendDegree = reader.GetInt32(reader.GetOrdinal("DefendDegree")),
                    vigour = reader.GetInt64(reader.GetOrdinal("Vigour")),
                    antiDefenseDegree = reader.GetInt32(reader.GetOrdinal("AntiDefenseDegree")),
                    antiResistanceDegree = reader.GetInt32(reader.GetOrdinal("AntiResistanceDegree")),
                    peakGrade = reader.GetInt32(reader.GetOrdinal("PeakGrade")),
                    updatedAt = reader.GetDateTime(reader.GetOrdinal("PropertiesUpdatedAt"))
                };
            }

            items.Add(new
            {
                id = reader.GetInt64(reader.GetOrdinal("Id")),
                name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader.GetString(reader.GetOrdinal("Name")),
                cls = reader.GetInt32(reader.GetOrdinal("Cls")),
                server = reader.IsDBNull(reader.GetOrdinal("Server")) ? null : reader.GetString(reader.GetOrdinal("Server")),
                teamId = reader.IsDBNull(reader.GetOrdinal("TeamId")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("TeamId")),
                teamName = reader.IsDBNull(reader.GetOrdinal("TeamName")) ? null : reader.GetString(reader.GetOrdinal("TeamName")),
                properties
            });
        }

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    private static Npgsql.NpgsqlParameter CloneParameter(Npgsql.NpgsqlParameter source)
    {
        return new Npgsql.NpgsqlParameter(source.ParameterName, source.NpgsqlDbType)
        {
            Value = source.Value
        };
    }
}
