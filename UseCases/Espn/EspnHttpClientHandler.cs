namespace UseCases.Espn;

/// <summary>
/// DelegatingHandler que inyecta cabeceras reales de navegador Chrome
/// para evadir el WAF de ESPN que bloquea peticiones de bots con HTTP 403.
/// </summary>
public class EspnHttpClientHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Referer", "https://www.espn.com/");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,es;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Google Chrome\";v=\"125\", \"Chromium\";v=\"125\", \"Not.A/Brand\";v=\"24\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-site");

        return await base.SendAsync(request, cancellationToken);
    }
}
