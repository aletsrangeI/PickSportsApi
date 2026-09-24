using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DTO.Migration;
using Interface.UseCases;

namespace UseCases.Migration;

public class ClosedXmlParserService : IXlsxParserService
{
    public Task<XlsxMigrationData> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Copy stream into MemoryStream if needed because ClosedXML requires a seekable stream
        MemoryStream memoryStream;
        if (stream is MemoryStream ms)
        {
            memoryStream = ms;
        }
        else
        {
            memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
        }

        using var workbook = new XLWorkbook(memoryStream);
        var data = new XlsxMigrationData();

        // 1. Config sheet
        var configWs = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Config", StringComparison.OrdinalIgnoreCase));
        if (configWs != null)
        {
            var firstRow = configWs.FirstRowUsed()?.RowNumber() ?? 1;
            var lastRow = configWs.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = firstRow + 1; r <= lastRow; r++)
            {
                var row = configWs.Row(r);
                var key = row.Cell(1).GetString().Trim();
                var value = row.Cell(2).GetString().Trim();

                if (key.Equals("seasonYear", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int sy))
                {
                    data.Config.SeasonYear = sy;
                }
                else if (key.Equals("seasontype", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int st))
                {
                    data.Config.SeasonType = st;
                }
                else if (key.Equals("baseSiteUrl", StringComparison.OrdinalIgnoreCase))
                {
                    data.Config.BaseSiteUrl = value;
                }
                else if (key.Equals("currentWeek", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int cw))
                {
                    data.Config.CurrentWeek = cw;
                }
                else if (key.Equals("formId", StringComparison.OrdinalIgnoreCase))
                {
                    data.Config.FormId = value;
                }
            }
        }

        // 2. Weeks sheet
        var weeksWs = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Weeks", StringComparison.OrdinalIgnoreCase));
        if (weeksWs != null)
        {
            var headerRow = weeksWs.FirstRowUsed()?.RowNumber() ?? 1;
            var headers = GetHeaders(weeksWs, headerRow);
            var lastRow = weeksWs.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = weeksWs.Row(r);
                var weekStr = GetCellValue(row, headers, "week");
                if (int.TryParse(weekStr, out int weekNum))
                {
                    var startRaw = GetCellValue(row, headers, "startDate");
                    var endRaw = GetCellValue(row, headers, "endDate");

                    data.Weeks.Add(new XlsxWeek
                    {
                        WeekNumber = weekNum,
                        StartDateRaw = startRaw,
                        EndDateRaw = endRaw,
                        StartDate = ParseDate(startRaw),
                        EndDate = ParseDate(endRaw)
                    });
                }
            }
        }

        // 3. Control Jornadas sheet
        var cjWs = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Control Jornadas", StringComparison.OrdinalIgnoreCase));
        if (cjWs != null)
        {
            var headerRow = cjWs.FirstRowUsed()?.RowNumber() ?? 1;
            var headers = GetHeaders(cjWs, headerRow);
            var lastRow = cjWs.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = cjWs.Row(r);
                var weekStr = GetCellValue(row, headers, "week");
                if (int.TryParse(weekStr, out int weekNum))
                {
                    var status = GetCellValue(row, headers, "status");
                    var pubStr = GetCellValue(row, headers, "publishedAt");
                    var lockStr = GetCellValue(row, headers, "lockedAt");
                    var scoredStr = GetCellValue(row, headers, "scoredAt");

                    data.ControlJornadas.Add(new XlsxControlJornada
                    {
                        WeekNumber = weekNum,
                        Status = status.ToUpperInvariant(),
                        PublishedAt = ParseFlexibleDateTime(pubStr),
                        LockedAt = ParseFlexibleDateTime(lockStr),
                        ScoredAt = ParseFlexibleDateTime(scoredStr),
                        LastAction = GetCellValue(row, headers, "lastAction"),
                        LastNote = GetCellValue(row, headers, "lastNote")
                    });
                }
            }
        }

        // 4. Matches sheet
        var matchesWs = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Matches", StringComparison.OrdinalIgnoreCase));
        if (matchesWs != null)
        {
            var headerRow = matchesWs.FirstRowUsed()?.RowNumber() ?? 1;
            var headers = GetHeaders(matchesWs, headerRow);
            var lastRow = matchesWs.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = matchesWs.Row(r);
                var gameId = GetCellValue(row, headers, "gameId");
                if (string.IsNullOrWhiteSpace(gameId)) continue;

                int.TryParse(GetCellValue(row, headers, "seasonYear"), out int sYear);
                int.TryParse(GetCellValue(row, headers, "seasontype"), out int sType);
                int.TryParse(GetCellValue(row, headers, "week"), out int weekNum);
                int.TryParse(GetCellValue(row, headers, "homeScore"), out int homeScore);
                int.TryParse(GetCellValue(row, headers, "awayScore"), out int awayScore);

                var dateUtcStr = GetCellValue(row, headers, "dateUTC");
                var dateUtc = ParseFlexibleDateTime(dateUtcStr) ?? DateTime.UtcNow;

                var winnerAbbr = GetCellValue(row, headers, "winnerAbbr");
                if (string.Equals(winnerAbbr, "empate", StringComparison.OrdinalIgnoreCase))
                {
                    winnerAbbr = "EMPATE";
                }

                data.Matches.Add(new XlsxMatch
                {
                    SeasonYear = sYear > 0 ? sYear : data.Config.SeasonYear,
                    SeasonType = sType > 0 ? sType : data.Config.SeasonType,
                    WeekNumber = weekNum,
                    EspnGameId = gameId,
                    DateUtc = DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc),
                    HomeTeamEspnId = GetCellValue(row, headers, "homeTeamId"),
                    HomeAbbr = GetCellValue(row, headers, "homeAbbr").ToUpperInvariant(),
                    HomeName = GetCellValue(row, headers, "homeName"),
                    AwayTeamEspnId = GetCellValue(row, headers, "awayTeamId"),
                    AwayAbbr = GetCellValue(row, headers, "awayAbbr").ToUpperInvariant(),
                    AwayName = GetCellValue(row, headers, "awayName"),
                    StatusState = GetCellValue(row, headers, "statusState").ToLowerInvariant(),
                    StatusDesc = GetCellValue(row, headers, "statusDesc"),
                    HomeScore = homeScore,
                    AwayScore = awayScore,
                    WinnerAbbr = string.IsNullOrWhiteSpace(winnerAbbr) ? null : winnerAbbr,
                    Broadcast = GetCellValue(row, headers, "broadcast"),
                    Venue = GetCellValue(row, headers, "venue"),
                    City = GetCellValue(row, headers, "city"),
                    Country = GetCellValue(row, headers, "country")
                });
            }
        }

        // 5. Standings sheet
        var standingsWs = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Standings", StringComparison.OrdinalIgnoreCase));
        if (standingsWs != null)
        {
            var headerRow = standingsWs.FirstRowUsed()?.RowNumber() ?? 1;
            var headers = GetHeaders(standingsWs, headerRow);
            var lastRow = standingsWs.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = standingsWs.Row(r);
                var player = GetCellValue(row, headers, "player");
                if (string.IsNullOrWhiteSpace(player)) continue;

                int.TryParse(GetCellValue(row, headers, "hits"), out int hits);
                int.TryParse(GetCellValue(row, headers, "totalPicks"), out int totalPicks);
                decimal.TryParse(GetCellValue(row, headers, "pct"), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct);

                data.Standings.Add(new XlsxStanding
                {
                    PlayerAlias = NormalizeAlias(player),
                    Hits = hits,
                    TotalPicks = totalPicks,
                    Pct = pct
                });
            }
        }

        // 6. Picks sheet
        var picksWs = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Picks", StringComparison.OrdinalIgnoreCase));
        if (picksWs != null)
        {
            var headerRow = picksWs.FirstRowUsed()?.RowNumber() ?? 1;
            var headers = GetHeaders(picksWs, headerRow);
            var lastRow = picksWs.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = picksWs.Row(r);
                var player = GetCellValue(row, headers, "player");
                var gameId = GetCellValue(row, headers, "gameId");
                var pickAbbr = GetCellValue(row, headers, "pickAbbr");

                if (string.IsNullOrWhiteSpace(player) || string.IsNullOrWhiteSpace(gameId)) continue;

                var pickClean = pickAbbr.Trim().ToUpperInvariant();

                data.Picks.Add(new XlsxPick
                {
                    PlayerAlias = NormalizeAlias(player),
                    EspnGameId = gameId.Trim(),
                    PickAbbr = pickClean
                });
            }
        }

        return Task.FromResult(data);
    }

    private static Dictionary<string, int> GetHeaders(IXLWorksheet ws, int rowNumber)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var row = ws.Row(rowNumber);
        foreach (var cell in row.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header) && !dict.ContainsKey(header))
            {
                dict[header] = cell.Address.ColumnNumber;
            }
        }
        return dict;
    }

    private static string GetCellValue(IXLRow row, Dictionary<string, int> headers, string column)
    {
        if (!headers.TryGetValue(column, out int colIdx)) return string.Empty;
        var cell = row.Cell(colIdx);
        return cell.GetString().Trim();
    }

    public static string NormalizeAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return string.Empty;
        return alias.Trim();
    }

    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static DateTime? ParseDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
        {
            return DateTime.SpecifyKind(d2, DateTimeKind.Utc);
        }
        return null;
    }

    private static DateTime? ParseFlexibleDateTime(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d))
        {
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
        // Spanish formats e.g. "27/07/2026 09:57:52 a. m."
        var esCulture = CultureInfo.GetCultureInfo("es-MX");
        if (DateTime.TryParse(raw, esCulture, DateTimeStyles.None, out var dEs))
        {
            return DateTime.SpecifyKind(dEs, DateTimeKind.Utc);
        }
        return null;
    }
}
