namespace TableLAN.Server.Services;

using System.Text.Json;

/// <summary>Esito del controllo aggiornamenti, pronto per il client.</summary>
/// <param name="Current">La versione che sta girando (es. <c>v0.2+95b6ac5</c>, o <c>dev</c>).</param>
/// <param name="Latest">L'ultima release pubblicata, se il controllo è riuscito; null se offline.</param>
/// <param name="UpdateAvailable">Vero se <paramref name="Latest"/> è più recente di <paramref name="Current"/>.</param>
/// <param name="Url">La pagina della release da aprire, quando c'è un aggiornamento.</param>
public sealed record UpdateStatus(string Current, string? Latest, bool UpdateAvailable, string? Url);

/// <summary>
/// Dice se sta girando l'ultima versione, o se sul repo ce n'è una più nuova.
///
/// È il secondo uso previsto da <see cref="BuildInfo"/>: la propria versione
/// serve appunto a confrontarla con l'ultima release. Il confronto lo fa
/// GitHub, che per ogni repo espone <c>releases/latest</c> — nessun git, nessun
/// server nostro da tenere in piedi.
///
/// È best-effort e non deve mai disturbare: TableLAN vive in LAN, spesso senza
/// internet. Se la chiamata fallisce (offline, rate limit, timeout) l'esito è
/// "nessun aggiornamento noto", non un errore — il Master non se ne accorge.
/// Il risultato si tiene in cache per qualche ora, così aprire e chiudere la
/// console non martella GitHub, e la risposta è immediata dopo il primo giro.
/// </summary>
public sealed class UpdateService
{
    // La sorgente: le release pubbliche del repo. Hardcoded perché *è* questo il
    // progetto — un altro repo non avrebbe le stesse release da confrontare.
    private const string LatestReleaseApi = "https://api.github.com/repos/br1brown/TableLAN/releases/latest";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly string _current;
    private readonly Func<CancellationToken, Task<(string Tag, string? Url)?>> _fetchLatest;
    private readonly TimeSpan _ttl;

    private readonly object _gate = new();
    private UpdateStatus? _cached;
    private DateTime _checkedAtUtc = DateTime.MinValue;

    /// <param name="fetchLatest">
    /// Come si recupera l'ultima release (tag + url), iniettabile per i test;
    /// di default interroga GitHub. Torna null se non si è potuto sapere.
    /// </param>
    public UpdateService(
        string currentVersion,
        Func<CancellationToken, Task<(string Tag, string? Url)?>>? fetchLatest = null,
        TimeSpan? ttl = null)
    {
        _current = currentVersion;
        _fetchLatest = fetchLatest ?? FetchFromGitHubAsync;
        _ttl = ttl ?? TimeSpan.FromHours(6);
    }

    public async Task<UpdateStatus> CheckAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_cached is not null && DateTime.UtcNow - _checkedAtUtc < _ttl)
                return _cached;
        }

        UpdateStatus result;
        try
        {
            var latest = await _fetchLatest(ct);
            result = latest is null
                ? new UpdateStatus(_current, null, false, null)
                : new UpdateStatus(_current, latest.Value.Tag, IsNewer(_current, latest.Value.Tag), latest.Value.Url);
        }
        catch
        {
            // Offline o GitHub irraggiungibile: non è un errore da mostrare, è
            // semplicemente "non lo so". Si riproverà al prossimo controllo.
            result = new UpdateStatus(_current, null, false, null);
        }

        lock (_gate)
        {
            _cached = result;
            _checkedAtUtc = DateTime.UtcNow;
        }
        return result;
    }

    /// <summary>
    /// Vero se <paramref name="latest"/> è una versione più recente di
    /// <paramref name="current"/>. Confronta i tag tipo <c>v0.2</c> numero per
    /// numero (<c>v0.10</c> &gt; <c>v0.9</c>, che il confronto tra stringhe
    /// sbaglierebbe). Un build locale (<c>dev</c>) o un tag illeggibile non
    /// generano mai un falso allarme: si torna <c>false</c>.
    /// </summary>
    public static bool IsNewer(string? current, string? latest)
    {
        var cur = ParseVersion(current);
        var lat = ParseVersion(latest);
        if (cur is null || lat is null)
            return false;

        var len = Math.Max(cur.Length, lat.Length);
        for (var i = 0; i < len; i++)
        {
            var c = i < cur.Length ? cur[i] : 0;
            var l = i < lat.Length ? lat[i] : 0;
            if (l != c)
                return l > c;
        }
        return false; // identiche
    }

    /// <summary>
    /// I numeri di un tag di versione: <c>v0.2+95b6ac5</c> → <c>[0, 2]</c>. Si
    /// scarta la <c>v</c> iniziale e tutto ciò che segue un <c>+</c> (il commit).
    /// Null se non c'è niente di numerico da confrontare (es. <c>dev</c>).
    /// </summary>
    private static int[]? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var core = tag.Trim().Split('+', 2)[0].TrimStart('v', 'V');
        var parts = core.Split('.');
        var numbers = new List<int>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var n))
                numbers.Add(n);
            else
                break; // primo pezzo non numerico (es. "-rc1"): ci si ferma
        }
        return numbers.Count > 0 ? numbers.ToArray() : null;
    }

    private static async Task<(string Tag, string? Url)?> FetchFromGitHubAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        // GitHub rifiuta le richieste senza User-Agent.
        req.Headers.UserAgent.ParseAdd("TableLAN-update-check");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
        return (tag, url);
    }
}
