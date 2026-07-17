namespace TableLAN.Server.Import;

using System.Globalization;
using System.Text.RegularExpressions;
using TableLAN.Core.Features;
using TableLAN.Core.Profile;
using TableLAN.Server.Data;

/// <summary>Cosa è entrato, per dirlo a chi ha premuto il bottone.</summary>
public sealed record EsitoImport(
    string CharacterId,
    string Nome,
    int Feature,
    int Fonti,
    int Incantesimi,
    IReadOnlyList<string> Avvisi);

/// <summary>
/// Traduce una scheda di D&D Beyond in una scheda TableLAN.
///
/// Non tocca il database da sé: chiama le stesse operazioni di authoring che
/// userebbe il Master a mano (<see cref="GameRepository.AddCharacterAsync"/>,
/// <c>AddSourceAsync</c>, <c>AddFeatureAsync</c>). Una seconda porta sul
/// database vorrebbe dire due posti in cui una scheda può nascere, e uno dei
/// due invecchia.
///
/// Sa di D&D, e va bene: si chiama "importa da D&D Beyond". Ma non lo impone al
/// profilo attivo — quello che il profilo non dichiara non viene scritto, e
/// finisce fra gli avvisi.
/// </summary>
public sealed class DdbImporter(GameRepository repo)
{
    /// <summary>Come D&D Beyond scrive i tempi di lancio, e cosa costano da noi.</summary>
    private static readonly Dictionary<string, string> Tempi = new()
    {
        ["1A"] = "Action",
        ["1BA"] = "BonusAction",
        ["1R"] = "Reaction",
    };

    /// <summary>Le caratteristiche di D&D Beyond e l'id con cui il profilo le chiama.</summary>
    private static readonly (string Ddb, string StatId)[] Caratteristiche =
    [
        ("STR", "for"), ("DEX", "des"), ("CON", "cos"),
        ("INT", "int"), ("WIS", "sag"), ("CHA", "car"),
        ("AC", "ca"), ("ProfBonus", "competenza"),
    ];

    public async Task<EsitoImport> ImportaAsync(
        IReadOnlyList<(string Nome, string Valore)> campi, GameProfile profile)
    {
        if (campi.Count == 0)
            throw new InvalidDataException(
                "Questo PDF non ha campi compilati. Da D&D Beyond serve l'export della scheda, non il foglio vuoto da stampare.");

        var avvisi = new List<string>();
        var c = Primi(campi);

        var nome = Valore(c, "CharacterName") ?? "Senza nome";
        var classe = Valore(c, "CLASS  LEVEL") ?? "";
        var pf = Numero(Valore(c, "MaxHP")) ?? 10;

        var characterId = await repo.AddCharacterAsync(
            string.IsNullOrWhiteSpace(classe) ? nome : $"{nome} ({classe})",
            Math.Max(1, pf),
            profile.Stats);

        var fonti = 0;
        var feature = 0;
        var incantesimi = 0;

        // --- Fonti di identità: classe, specie, background.
        var fonteClasse = await AggiungiFonte(characterId, classe, "Class");
        var fonteSpecie = await AggiungiFonte(characterId, Valore(c, "RACE"), "Race");
        var fonteBackground = await AggiungiFonte(characterId, Valore(c, "BACKGROUND"), "Background");
        fonti += new[] { fonteClasse, fonteSpecie, fonteBackground }.Count(x => x is not null);

        // --- Armi: una Fonte a testa, con sotto attacco e danni.
        for (var n = 1; n <= 5; n++)
        {
            var nomeArma = Valore(c, n == 1 ? "Wpn Name" : $"Wpn Name {n}");
            if (nomeArma is null) continue;

            var fonteArma = await AggiungiFonte(characterId, nomeArma, "Item");
            fonti++;

            var attacco = Valore(c, $"Wpn{n} AtkBonus");
            var danni = Valore(c, $"Wpn{n} Damage");
            var note = Valore(c, $"Wpn Notes {n}") ?? "";

            if (attacco is not null)
            {
                await repo.AddFeatureAsync(characterId, new FeatureDraft(
                    ShortName: $"Attacco: {nomeArma}",
                    Costs: [new ActivationCost("Action", 1)],
                    Description: $"Tiro per colpire con {nomeArma}. {note}".Trim(),
                    SourceId: fonteArma,
                    Roll: TiroDaBonus(attacco)));
                feature++;
            }

            if (danni is not null)
            {
                // I danni non costano un'altra Azione: sono la conseguenza del
                // colpo, non un secondo colpo.
                await repo.AddFeatureAsync(characterId, new FeatureDraft(
                    ShortName: $"Danni: {nomeArma}",
                    Costs: [],
                    Description: $"Danni di {nomeArma}: {danni}.",
                    SourceId: fonteArma,
                    Roll: FormulaDanni(danni)));
                feature++;
            }
        }

        // --- Incantesimi: il livello lo dà l'ultima intestazione incontrata.
        var poolSlot = profile.Pools.FirstOrDefault(p => p.Kind == PoolKind.Leveled);
        var livello = 0;
        var fontiMagie = new Dictionary<string, string>();

        foreach (var (chiave, valore) in campi)
        {
            if (chiave.StartsWith("spellHeader", StringComparison.Ordinal))
            {
                livello = LivelloDaIntestazione(valore);
            }
            else if (chiave.StartsWith("spellSlotHeader", StringComparison.Ordinal))
            {
                var quanti = SlotDaIntestazione(valore);
                if (quanti > 0 && livello > 0 && poolSlot is not null)
                    await repo.SetPoolTierAsync(characterId, poolSlot.Id, livello, quanti, quanti);
            }
            else if (chiave.StartsWith("spellName", StringComparison.Ordinal))
            {
                var i = chiave["spellName".Length..];
                var costi = new List<ActivationCost>();

                if (Tempi.TryGetValue((Valore(c, $"spellCastingTime{i}") ?? "").Trim(), out var risorsa))
                    costi.Add(new ActivationCost(risorsa, 1));

                if (livello > 0)
                {
                    if (poolSlot is not null)
                        costi.Add(new ActivationCost(poolSlot.Id, livello));
                    else
                        avvisi.Add($"'{valore}': il profilo '{profile.Name}' non ha riserve a livelli, lo slot non è stato messo.");
                }

                var fonte = Valore(c, $"spellSource{i}") ?? classe;
                if (!fontiMagie.TryGetValue(fonte, out var fonteId))
                {
                    fonteId = (await AggiungiFonte(characterId, fonte, "Other"))!;
                    fontiMagie[fonte] = fonteId;
                    fonti++;
                }

                var colpo = Valore(c, $"spellSaveHit{i}");
                await repo.AddFeatureAsync(characterId, new FeatureDraft(
                    ShortName: valore,
                    Costs: costi,
                    Description: DescrizioneMagia(c, i, livello, colpo),
                    SourceId: fonteId,
                    Roll: TiroDaBonus(colpo)));
                feature++;
                incantesimi++;
            }
        }

        // --- Tratti di classe, specie, background: prosa, nient'altro.
        for (var n = 1; n <= 6; n++)
        {
            var blob = Valore(c, $"FeaturesTraits{n}");
            if (blob is null) continue;

            var intestazione = Intestazione(blob);
            var fonte = ScegliFonte(intestazione, classe, fonteClasse, fonteSpecie, fonteBackground)
                        ?? await AggiungiFonte(characterId, intestazione, "Other");
            if (fonte is null) continue;

            foreach (var (titolo, testo) in Tratti(blob))
            {
                await repo.AddFeatureAsync(characterId, new FeatureDraft(
                    ShortName: titolo,
                    Costs: [],
                    Description: testo,
                    SourceId: fonte));
                feature++;
            }
        }

        // --- Statistiche: si scrivono con l'etichetta che il profilo usa oggi.
        foreach (var (ddb, statId) in Caratteristiche)
        {
            var grezzo = Valore(c, ddb);
            if (grezzo is null) continue;

            if (profile.Stat(statId) is not { } def)
            {
                avvisi.Add($"Il profilo '{profile.Name}' non ha la statistica '{statId}': '{ddb}' non è stato importato.");
                continue;
            }

            if (Numero(grezzo) is { } n)
                await repo.SetCustomStatAsync(characterId, def.Label, n);
        }

        return new EsitoImport(characterId, nome, feature, fonti, incantesimi, avvisi);
    }

    private async Task<string?> AggiungiFonte(string characterId, string? nome, string tipo) =>
        string.IsNullOrWhiteSpace(nome) ? null : await repo.AddSourceAsync(characterId, nome.Trim(), tipo, null);

    /// <summary>Il primo valore per ogni nome di campo: la scheda ripete il nome del PG su ogni pagina.</summary>
    private static Dictionary<string, string> Primi(IReadOnlyList<(string Nome, string Valore)> campi)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (n, v) in campi)
            d.TryAdd(n, v);
        return d;
    }

    private static string? Valore(Dictionary<string, string> c, string chiave) =>
        c.TryGetValue(chiave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    /// <summary>«=== CANTRIPS ===» → 0; «=== 2nd LEVEL ===» → 2.</summary>
    internal static int LivelloDaIntestazione(string testo)
    {
        if (testo.Contains("CANTRIP", StringComparison.OrdinalIgnoreCase)) return 0;
        var m = Regex.Match(testo, @"\d+");
        return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>«4 Slots OOOO» → 4; «(At Will)» → 0.</summary>
    internal static int SlotDaIntestazione(string testo)
    {
        var m = Regex.Match(testo.Trim(), @"^(\d+)\s+Slot", RegexOptions.IgnoreCase);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>«+2» → «1d20+2». «CON 10» è una CD, non un tiro: niente dado.</summary>
    internal static string? TiroDaBonus(string? valore)
    {
        var v = valore?.Trim();
        return v is not null && Regex.IsMatch(v, @"^[+-]\d+$") ? $"1d20{v}" : null;
    }

    /// <summary>«1d8 Lightning» → «1d8». «2 Bludgeoning» non è un dado: niente tiro.</summary>
    internal static string? FormulaDanni(string danni)
    {
        var m = Regex.Match(danni.Trim(), @"^[\dd+\-\s]+", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var formula = m.Value.Replace(" ", "");
        return Regex.IsMatch(formula, @"^\d*d\d+") ? formula : null;
    }

    private static int? Numero(string? testo) =>
        testo is not null && int.TryParse(Regex.Replace(testo, @"[^\d-]", ""), out var n) ? n : null;

    private static string Intestazione(string blob)
    {
        var m = Regex.Match(blob, @"===\s*(.+?)\s*===");
        return m.Success ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(m.Groups[1].Value.ToLowerInvariant()) : "Tratti";
    }

    /// <summary>I tratti di classe stanno sotto la Fonte classe, non in una Fonte nuova che dice la stessa cosa.</summary>
    private static string? ScegliFonte(string intestazione, string classe, string? fonteClasse, string? fonteSpecie, string? fonteBackground)
    {
        var i = intestazione.ToLowerInvariant();
        var nomeClasse = Regex.Replace(classe, @"\s*\d+$", "").Trim().ToLowerInvariant();

        // Dal più specifico al più generico, e non è pignoleria: «Folk Hero
        // Background Feature» contiene sia "background" sia "feature", e col
        // primo controllo sulla classe i tratti del background finivano sotto
        // il Mago.
        if (fonteBackground is not null && i.Contains("background"))
            return fonteBackground;
        if (fonteSpecie is not null && (i.Contains("species") || i.Contains("trait")))
            return fonteSpecie;
        if (fonteClasse is not null && (i.Contains("feature") || (nomeClasse.Length > 0 && i.Contains(nomeClasse))))
            return fonteClasse;
        return null;
    }

    /// <summary>Spezza il blocco «* Nome - fonte\n testo» nei suoi tratti.</summary>
    internal static IReadOnlyList<(string Titolo, string Testo)> Tratti(string blob)
    {
        var fuori = new List<(string, string)>();
        foreach (var pezzo in blob.Split("\n*").Skip(1))
        {
            var righe = pezzo.Trim().Split('\n').Select(r => r.Trim()).ToList();
            if (righe.Count == 0) continue;

            // «Arcane Recovery • PHB-2024 166» → «Arcane Recovery»: il rimando
            // al manuale non è un nome, ed è la prima cosa che leggi sulla riga.
            //
            // Il separatore è un bullet, non un trattino — cercare solo il
            // trattino lasciava il nome intero, e sulla scheda si leggeva
            // «Core Druid Traits • PHB-2024 79».
            var titolo = Regex.Split(righe[0], @"\s+[-–—•*·]\s+")[0].Trim();
            var testo = string.Join(" ", righe.Skip(1).Where(r => r.Length > 0 && !r.StartsWith('|'))).Trim();
            if (titolo.Length > 0)
                fuori.Add((titolo, testo.Length > 0 ? testo : titolo));
        }
        return fuori;
    }

    private static string DescrizioneMagia(Dictionary<string, string> c, string i, int livello, string? colpo)
    {
        var pezzi = new List<string>
        {
            livello == 0 ? "Trucchetto." : $"Livello {livello}.",
        };
        if (Valore(c, $"spellCastingTime{i}") is { } t) pezzi.Add($"Lancio: {t}.");
        if (Valore(c, $"spellRange{i}") is { } r) pezzi.Add($"Gittata: {r}.");
        if (Valore(c, $"spellDuration{i}") is { } d) pezzi.Add($"Durata: {d}.");
        if (Valore(c, $"spellComponents{i}") is { } comp) pezzi.Add($"Componenti: {comp}.");
        if (colpo is not null && colpo != "--") pezzi.Add($"Tiro/CD: {colpo}.");
        if (Valore(c, $"spellPage{i}") is { } p) pezzi.Add($"({p})");
        return string.Join(" ", pezzi);
    }
}
