namespace TableLAN.Server.Services;

using Microsoft.EntityFrameworkCore;
using TableLAN.Server.Data;

/// <summary>
/// Una campagna è un file <c>.db</c> (Capitolo: "La campagna è un file"). Il
/// Master ne ha più d'una nella stessa cartella e le cambia in corsa, senza
/// rinominare file e riavviare: questo servizio tiene qual è quella attiva,
/// elenca le altre accanto, e permette di passarci sopra o crearne una nuova.
///
/// È il perno del cambio-campagna a caldo: la <see cref="CampaignDbContextFactory"/>
/// costruisce ogni <see cref="AppDb"/> puntando al percorso corrente, quindi
/// spostarlo qui basta a far leggere e scrivere tutto il resto sulla campagna
/// nuova, dal prossimo contesto in poi.
/// </summary>
public sealed class CampaignService
{
    private readonly object _gate = new();
    private string _currentPath;

    public CampaignService(string initialPath)
    {
        _currentPath = Path.GetFullPath(initialPath);
        Directory.CreateDirectory(Folder);
    }

    /// <summary>Percorso assoluto del file della campagna attiva.</summary>
    public string CurrentPath
    {
        get { lock (_gate) { return _currentPath; } }
    }

    public string CurrentName => Path.GetFileName(CurrentPath);

    /// <summary>La cartella in cui vivono le campagne: quella del file attivo.</summary>
    public string Folder => Path.GetDirectoryName(CurrentPath) ?? ".";

    /// <summary>Le campagne accanto (tutti i <c>.db</c> nella cartella), più recenti prima.</summary>
    public IReadOnlyList<CampaignInfo> List()
    {
        var current = CurrentName;
        return Directory.EnumerateFiles(Folder, "*.db")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new CampaignInfo(f.Name, f.Name == current, f.Length, f.LastWriteTimeUtc))
            .ToList();
    }

    /// <summary>
    /// Il percorso di una campagna dal solo nome, dentro la cartella. Null se il
    /// nome è vuoto: <see cref="Path.GetFileName(string)"/> toglie ogni pezzo di
    /// cartella, così un nome non può uscire dalla cartella delle campagne
    /// (niente <c>../</c>). L'estensione <c>.db</c> si aggiunge da sé.
    /// </summary>
    public string? PathFor(string? name)
    {
        var safe = Path.GetFileName(name?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safe))
            return null;
        if (!safe.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            safe += ".db";
        return Path.Combine(Folder, safe);
    }

    /// <summary>
    /// Nome del puntatore all'ultima campagna usata, accanto ai .db. Non è un
    /// .db, quindi non compare mai fra le campagne; è la sola cosa che
    /// sopravvive al riavvio, per riaprire dove si era rimasti invece di tornare
    /// sempre a <c>tablelan.db</c>.
    /// </summary>
    public const string PointerFileName = ".tablelan-campagna";

    /// <summary>
    /// L'ultima campagna ricordata dal puntatore, se il file c'è ancora; null se
    /// il puntatore manca, è vuoto o illeggibile, o punta a una campagna
    /// archiviata — in tutti quei casi si riparte dal default, senza errori.
    /// <see cref="Path.GetFileName(string)"/> impedisce che il puntatore mandi
    /// fuori dalla cartella.
    /// </summary>
    public static string? RememberedCampaign(string folder)
    {
        try
        {
            var pointer = Path.Combine(folder, PointerFileName);
            if (!File.Exists(pointer))
                return null;
            var name = Path.GetFileName(File.ReadAllText(pointer).Trim());
            if (string.IsNullOrEmpty(name))
                return null;
            var candidate = Path.Combine(folder, name);
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    public void SetCurrent(string fullPath)
    {
        lock (_gate)
            _currentPath = fullPath;

        // Ricorda la scelta per il prossimo avvio. Vale per il caso normale: con
        // TABLELAN_DB il file è pinnato a mano e quello vince comunque (vedi
        // ServerBootstrap.ResolveDbPath).
        try
        {
            var folder = Path.GetDirectoryName(fullPath) ?? ".";
            File.WriteAllText(Path.Combine(folder, PointerFileName), Path.GetFileName(fullPath));
        }
        catch
        {
            // Best effort: se non si può scrivere il puntatore, si perde solo la
            // memoria dell'ultima campagna, non un dato di gioco.
        }
    }
}

/// <summary>Una campagna nell'elenco: nome file, se è quella attiva, dimensione e ultima modifica.</summary>
public sealed record CampaignInfo(string Name, bool Current, long SizeBytes, DateTime Modified);

/// <summary>
/// Costruisce ogni <see cref="AppDb"/> puntando alla campagna attiva secondo il
/// <see cref="CampaignService"/>. Sostituisce <c>AddDbContextFactory</c>, il cui
/// percorso è fissato una volta all'avvio: qui invece la stringa di connessione
/// si rilegge a ogni contesto, ed è ciò che rende possibile cambiare campagna a
/// caldo. Le connessioni sono di breve durata (un contesto per operazione),
/// quindi dopo lo switch le operazioni successive vanno da sole sul file nuovo.
/// </summary>
public sealed class CampaignDbContextFactory(CampaignService campaign) : IDbContextFactory<AppDb>
{
    public AppDb CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDb>()
            .UseSqlite($"Data Source={campaign.CurrentPath}")
            .Options;
        return new AppDb(options);
    }
}
