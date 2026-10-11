using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Common;
using DTO.Bulletin;
using Interface.UseCases;
using Microsoft.Extensions.Configuration;

namespace UseCases.Bulletin;

public class GeminiBulletinNarrativeService : IBulletinNarrativeService
{
    private const string DefaultModel = "gemini-3.5-flash";
    private static readonly string[] FallbackCandidateModels = new[]
    {
        "gemini-3.5-flash",
        "gemini-3.5-flash-lite",
        "gemini-3.8-flash"
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppLogger<GeminiBulletinNarrativeService> _logger;

    public GeminiBulletinNarrativeService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IAppLogger<GeminiBulletinNarrativeService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<BulletinNarrativeResultDto> GenerateChronicleAsync(BulletinNarrativeContextDto context, CancellationToken ct = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        var configuredModel = _configuration["Gemini:Model"];
        var preferredModel = string.IsNullOrWhiteSpace(configuredModel) ? DefaultModel : configuredModel.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("[GeminiNarrative] Gemini:ApiKey no configurada. Usando generador procedural de respaldo.");
            return GenerateProceduralChronicle(context, "Procedural-Fallback (Sin API Key)");
        }

        var candidateModels = new List<string> { preferredModel };
        foreach (var fallback in FallbackCandidateModels)
        {
            if (!candidateModels.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                candidateModels.Add(fallback);
        }

        var prompt = BuildPrompt(context);

        foreach (var candidate in candidateModels)
        {
            try
            {
                var result = await CallGeminiApiAsync(apiKey, candidate, prompt, ct);
                if (result != null && !string.IsNullOrWhiteSpace(result.Chronicle))
                {
                    _logger.LogInformation("[GeminiNarrative] Crónica generada exitosamente con modelo {0} para Jornada {1}.", candidate, context.WeekNumber);
                    return new BulletinNarrativeResultDto
                    {
                        Headline = result.Headline ?? $"Crónica de la Jornada {context.WeekNumber}",
                        Chronicle = result.Chronicle,
                        GeneratedByAi = true,
                        NarrativeModel = candidate
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GeminiNarrative] Error intentando modelo {0}: {1}", candidate, ex.Message);
            }
        }

        _logger.LogError("[GeminiNarrative] Ningún modelo de Gemini respondió exitosamente para Jornada {0}. Activando fallback procedural.", context.WeekNumber);
        return GenerateProceduralChronicle(context, $"Procedural-Fallback ({preferredModel} offline)");
    }

    private async Task<GeminiParsedResponse?> CallGeminiApiAsync(string apiKey, string model, string prompt, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.8
            }
        };

        var response = await client.PostAsJsonAsync(url, requestBody, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("[GeminiNarrative] Gemini API retornó HTTP {0}: {1}", (int)response.StatusCode, err);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
            return null;

        var textContent = parts[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(textContent)) return null;

        // Limpiar backticks si el modelo los incluyó
        var cleanJson = textContent.Trim();
        if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            cleanJson = cleanJson[7..];
        if (cleanJson.StartsWith("```"))
            cleanJson = cleanJson[3..];
        if (cleanJson.EndsWith("```"))
            cleanJson = cleanJson[..^3];
        cleanJson = cleanJson.Trim();

        return JsonSerializer.Deserialize<GeminiParsedResponse>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static string BuildPrompt(BulletinNarrativeContextDto ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Eres el cronista deportivo y editorialista estrella de PickSports Weekly, la quiniela de amigos más apasionada y picante de la Liga MX.");
        sb.AppendLine("Tu misión es escribir la CRÓNICA DE PRIMERA PLANA de la jornada combinando de forma orgánica y divertida DOS dimensiones:");
        sb.AppendLine();
        sb.AppendLine("1. LA REALIDAD DEL FÚTBOL MEXICANO (LIGA MX):");
        sb.AppendLine("- Usa tu conocimiento futbolero sobre los equipos involucrados, sus apodos populares (Águilas, Chivas, Felinos, Cementeros, Tuzos, Gallos, etc.), estadios, momentos futboleros, crisis, técnicos en riesgo, polémicas o cómo llegaron a esta jornada.");
        sb.AppendLine("- Menciona qué significan los resultados en la cancha (si un grande goleó, si hubo sufrimiento, si un penal pesó o si un DT respira o tiembla).");
        sb.AppendLine("- Dale sabor y mística de periodismo deportivo mexicano (estilo de debate ardiente nacional: Christian Martinoli, Faitelson, etc.: ironía fina, hipérboles y pura pasión balompédica).");
        sb.AppendLine();
        sb.AppendLine("2. EL DRAMA Y CARRILLA DE LA QUINIELA ENTRE AMIGOS:");
        sb.AppendLine("- Búrlate cariñosamente o ensalza a los protagonistas del grupo:");
        sb.AppendLine("  * El MVP / Campeón de la semana (quien se llevó la gloria).");
        sb.AppendLine("  * El Consentido de la fecha (si el equipo más votado cumplió o si los dejó vestidos y alborotados).");
        sb.AppendLine("  * El Rompe-Quinielas (el partido donde casi todos se fueron con la finta y solo un valiente o nadie acertó).");
        sb.AppendLine("  * La Sala de la Infamia: el más Humillado de la fecha, los somníferos con empates a cero, y los empates fallidos.");
        sb.AppendLine("  * El Trepa Cerros (quien dio el salto en la tabla general) vs la Caída Libre (quien se fue al abismo).");
        sb.AppendLine();
        sb.AppendLine("REGLAS EDITORIALES Y DE FORMATO:");
        sb.AppendLine("- PROHIBIDO hacer un simple resumen o lista de estadísticas una por una (el lector ya ve la tabla y tarjetas abajo).");
        sb.AppendLine("- Escribe una auténtica NARRATIVA periodística en prosa viva que cuente la historia del fin de semana (3 a 5 párrafos sustanciosos).");
        sb.AppendLine("- Usa Markdown con **negritas** para destacar participantes y equipos, y _cursivas_ para ironías o términos futboleros.");
        sb.AppendLine("- Comienza directo con un titular potente estilo portada y arranca con la atmósfera de la jornada sin saludos genéricos ni despedidas burocráticas.");
        sb.AppendLine();
        sb.AppendLine($"DATOS DE LA JORNADA {ctx.WeekNumber} ({ctx.SeasonName}):");
        sb.AppendLine($"- Efectividad comunitaria de la quiniela: {ctx.CommunityAccuracyPct}%");

        if (ctx.Podium.Any())
        {
            var p1 = ctx.Podium[0];
            sb.AppendLine($"- 1er Lugar / MVP: {p1.Alias} con {p1.Hits} aciertos ({p1.AccuracyPct}%), {p1.UpsetHits} sorpresas y {p1.Humillaciones} humillaciones.");
            if (ctx.Podium.Count > 1)
            {
                var p2 = ctx.Podium[1];
                sb.AppendLine($"- 2do Lugar: {p2.Alias} con {p2.Hits} aciertos.");
            }
        }

        if (ctx.Favorite != null)
        {
            sb.AppendLine($"- El Consentido: {ctx.Favorite.PickPct}% de la quiniela confió en {ctx.Favorite.TeamName} ({ctx.Favorite.MatchLabel}). Resultado: {(ctx.Favorite.Won ? "CUMPLIÓ con la victoria" : "LOS DEJÓ COLGADOS y no ganó")}.");
        }

        if (ctx.RompeQuinielas != null)
        {
            sb.AppendLine($"- El Rompe-Quinielas: {ctx.RompeQuinielas.MatchLabel} donde solo el {ctx.RompeQuinielas.AccuracyPct}% acertó ({ctx.RompeQuinielas.CorrectPicks} de {ctx.RompeQuinielas.TotalPicks}). Ganador: {ctx.RompeQuinielas.WinnerAbbr}.");
        }

        if (ctx.SurpriseKing.Any())
        {
            sb.AppendLine($"- Rey de las Sorpresas: {string.Join(", ", ctx.SurpriseKing.Select(s => $"{s.Alias} ({s.Value})"))}");
        }

        if (ctx.Humillado.Any())
        {
            sb.AppendLine($"- El Humillado (Sala de la Infamia): {string.Join(", ", ctx.Humillado.Select(h => $"{h.Alias} ({h.Value})"))}");
        }

        if (ctx.Somnifero.Any())
        {
            sb.AppendLine($"- Víctima del Somnífero: {string.Join(", ", ctx.Somnifero.Select(s => $"{s.Alias} ({s.Value})"))}");
        }

        if (ctx.EmpateFallido.Any())
        {
            sb.AppendLine($"- Rey del Empate Fallido: {string.Join(", ", ctx.EmpateFallido.Select(e => $"{e.Alias} ({e.Value})"))}");
        }

        if (ctx.Climber != null)
        {
            sb.AppendLine($"- El Trepa Cerros (Radar General): {ctx.Climber.Alias} subió +{ctx.Climber.PositionsDelta} posiciones (de {ctx.Climber.PreviousRank}° a {ctx.Climber.CurrentRank}°).");
        }

        if (ctx.Faller != null)
        {
            sb.AppendLine($"- Caída Libre (Radar General): {ctx.Faller.Alias} cayó {ctx.Faller.PositionsDelta} posiciones (de {ctx.Faller.PreviousRank}° a {ctx.Faller.CurrentRank}°).");
        }

        if (ctx.Derbies.Any())
        {
            sb.AppendLine();
            sb.AppendLine("CLÁSICOS DISPUTADOS:");
            foreach (var d in ctx.Derbies)
            {
                sb.AppendLine($"- {d.DerbyName}: {d.ScoreLabel} (Ganador: {d.WinnerAbbr ?? "EMPATE"})");
            }
        }

        if (ctx.Streaks.Any())
        {
            sb.AppendLine();
            sb.AppendLine("RACHAS DE EQUIPOS EN LA TEMPORADA:");
            foreach (var s in ctx.Streaks)
            {
                sb.AppendLine($"- {s.Description}");
            }
        }

        if (ctx.FinishedMatchSummaries.Any())
        {
            sb.AppendLine();
            sb.AppendLine("MARCADORES DE LA FECHA:");
            foreach (var m in ctx.FinishedMatchSummaries)
            {
                sb.AppendLine($"  * {m}");
            }
        }

        if (ctx.NextWeekNumber.HasValue && ctx.NextWeekMatchSummaries.Any())
        {
            sb.AppendLine();
            sb.AppendLine($"CARTELERA DE LA SIGUIENTE JORNADA (Jornada {ctx.NextWeekNumber.Value}):");
            foreach (var m in ctx.NextWeekMatchSummaries)
            {
                sb.AppendLine($"  * {m}");
            }
            sb.AppendLine();
            sb.AppendLine("SECCIÓN OBLIGATORIA AL FINAL DE LA CRÓNICA:");
            sb.AppendLine($"Al terminar la crónica de la jornada jugada, DEBES incluir obligatoriamente una sección final con el encabezado exacto: '### 🔭 A tener en cuenta para la Jornada {ctx.NextWeekNumber.Value}:'");
            sb.AppendLine("- Destaca y analiza 2 o 3 partidos clave de esa cartelera (por ejemplo: clásicos, duelos en la cima, choque de sotaneros o visitas trampa).");
            sb.AppendLine("- Explica qué está en juego deportivamente para esos equipos y da una advertencia o consejo futbolero a los quinieleros para armar sus picks.");
        }

        sb.AppendLine();
        sb.AppendLine("RESPONDE EXCLUSIVAMENTE con un JSON válido con esta estructura:");
        sb.AppendLine("{");
        sb.AppendLine("  \"headline\": \"Titular de portada con gancho periodístico\",");
        sb.AppendLine("  \"chronicle\": \"Texto completo de la crónica en Markdown\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static BulletinNarrativeResultDto GenerateProceduralChronicle(BulletinNarrativeContextDto ctx, string modelLabel)
    {
        var mvpAlias = ctx.Podium.FirstOrDefault()?.Alias ?? "El puntero";
        var mvpHits = ctx.Podium.FirstOrDefault()?.Hits ?? 0;
        var accuracy = ctx.CommunityAccuracyPct;

        var sbHeadline = new StringBuilder();
        var sbBody = new StringBuilder();

        // Titular
        if (ctx.Derbies.Any())
        {
            var derby = ctx.Derbies[0];
            sbHeadline.Append($"Fiesta grande en {derby.DerbyName}: {mvpAlias} asalta el liderato de la Jornada {ctx.WeekNumber}");
        }
        else if (ctx.Favorite != null && !ctx.Favorite.Won)
        {
            sbHeadline.Append($"Traición en la Jornada {ctx.WeekNumber}: {ctx.Favorite.TeamName} deja colgada a la quiniela y {mvpAlias} reina en el caos");
        }
        else
        {
            sbHeadline.Append($"Golpe de autoridad en la Jornada {ctx.WeekNumber}: {mvpAlias} se corona en una fecha de {accuracy}% de efectividad");
        }

        // Párrafo 1: El clima general y la efectividad
        sbBody.AppendLine($"La **Jornada {ctx.WeekNumber}** de la quiniela cerró el telón dejando un campo minado de emociones y una efectividad comunitaria del **{accuracy}%**. Con partidos intensos y marcadores que sacudieron la tabla, los participantes vivieron un fin de semana donde cada gol cambió destinos.");
        sbBody.AppendLine();

        // Párrafo 2: Clásicos o favorito traicionado
        if (ctx.Derbies.Any())
        {
            var derby = ctx.Derbies[0];
            sbBody.AppendLine($"El foco de la fecha estuvo en **{derby.DerbyName}** ({derby.ScoreLabel}), un duelo de alto voltaje que dividió lealtades en el grupo y definió puntos vitales.");
        }
        else if (ctx.Favorite != null)
        {
            if (ctx.Favorite.Won)
            {
                sbBody.AppendLine($"El gran consentido de la semana fue **{ctx.Favorite.TeamName}**, respaldado por el **{ctx.Favorite.PickPct}%** de los pronósticos, y esta vez no falló a su fanaticada sellando una victoria que dio respiro a la mayoría.");
            }
            else
            {
                sbBody.AppendLine($"El drama estalló con **{ctx.Favorite.TeamName}**: el **{ctx.Favorite.PickPct}%** confió a ojos cerrados en su triunfo, pero la cancha dictó otra sentencia, dejando a gran parte de los quinieleros con las manos vacías.");
            }
        }
        sbBody.AppendLine();

        // Párrafo 3: El MVP y las sorpresas
        sbBody.AppendLine($"En medio de la batalla, el gran vencedor fue **{mvpAlias}**, alzándose con el trofeo de **MVP de la jornada** con una cosecha de **{mvpHits} aciertos**.");
        if (ctx.SurpriseKing.Any())
        {
            var sk = ctx.SurpriseKing[0];
            sbBody.AppendLine($"Mención de honor para **{sk.Alias}**, proclamado el *Rey de las Sorpresas*, al descifrar el batacazo que la mayoría dio por perdido.");
        }
        sbBody.AppendLine();

        // Párrafo 4: Infamia y Radar
        if (ctx.Humillado.Any() || ctx.Climber != null || ctx.Faller != null)
        {
            var notes = new List<string>();
            if (ctx.Climber != null)
                notes.Add($"**{ctx.Climber.Alias}** protagonizó la gran escalada (+{ctx.Climber.PositionsDelta} posiciones en la General)");
            if (ctx.Faller != null)
                notes.Add($"mientras que **{ctx.Faller.Alias}** sufrió una dolorosa caída libre ({ctx.Faller.PositionsDelta} lugares)");
            if (ctx.Humillado.Any())
                notes.Add($"y **{ctx.Humillado[0].Alias}** inauguró la Sala de la Infamia con una amarga goleada");

            sbBody.AppendLine($"En los movimientos de la tabla, {string.Join(", ", notes)}. La mesa queda servida para la próxima fecha donde nadie tiene el puesto asegurado.");
        }

        // Párrafo 5: A tener en cuenta para la siguiente jornada
        if (ctx.NextWeekNumber.HasValue && ctx.NextWeekMatchSummaries.Any())
        {
            sbBody.AppendLine();
            sbBody.AppendLine($"### 🔭 A tener en cuenta para la Jornada {ctx.NextWeekNumber.Value}:");
            var previewMatches = ctx.NextWeekMatchSummaries.Take(3).ToList();
            foreach (var match in previewMatches)
            {
                sbBody.AppendLine($"- **{match}**: Choque de alta tensión que pondrá a prueba la intuición de la quiniela.");
            }
            sbBody.AppendLine("- ¡Asegúrate de registrar tus pronósticos antes del silbatazo inicial del primer encuentro!");
        }

        return new BulletinNarrativeResultDto
        {
            Headline = sbHeadline.ToString(),
            Chronicle = sbBody.ToString().TrimEnd(),
            GeneratedByAi = false,
            NarrativeModel = modelLabel
        };
    }

    private class GeminiParsedResponse
    {
        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        [JsonPropertyName("chronicle")]
        public string? Chronicle { get; set; }
    }
}
