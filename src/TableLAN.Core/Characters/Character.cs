namespace TableLAN.Core.Characters;

using TableLAN.Core.Features;
using TableLAN.Core.Profile;
using TableLAN.Core.Sources;

/// <summary>
/// Il personaggio è, a tutti gli effetti, una lista di Fonti attive
/// (Capitolo 7). Le feature arrivano dalle Fonti; il rendering raggruppa
/// per Fonte e collassa. Non esiste codice dedicato al multiclasse: è solo
/// il caso in cui il filtro per tipo Fonte restituisce più di un elemento.
/// </summary>
public class Character
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public List<Source> Sources { get; init; } = [];

    public List<Feature> Features { get; init; } = [];

    /// <summary>Riserve di risorse (slot, punti…), vive anche fuori dal combattimento.</summary>
    public ResourceBook Resources { get; init; } = new();

    /// <summary>Inventario ordinabile (Capitolo 9): l'ordine è quello della lista.</summary>
    public List<InventoryItem> Inventory { get; init; } = [];

    public int MaxHp { get; set; }

    public int CurrentHp { get; set; }

    /// <summary>Statistiche dinamiche homebrew (es. "Sanità Mentale"), valori base.</summary>
    public IDictionary<string, object?> CustomStats { get; init; } =
        new Dictionary<string, object?>();

    /// <summary>
    /// Stato on/off delle feature con effetti "indossabili" (id feature →
    /// attiva). Le feature non-toggleable sono sempre attive; per le
    /// toggleable il default è attivo, salvo che qui sia impostato false.
    /// </summary>
    public IDictionary<string, bool> EffectStates { get; init; } =
        new Dictionary<string, bool>();

    /// <summary>
    /// Cosa occupa ogni slot esclusivo del profilo: id slot → id feature.
    /// Vuoto per chi non concentra niente. Dura fra i turni — è l'unico stato
    /// di gioco che non si azzera a fine turno.
    /// </summary>
    public IDictionary<string, string> Occupied { get; init; } =
        new Dictionary<string, string>();

    /// <summary>La feature che occupa questo slot, se ce n'è una ancora sulla scheda.</summary>
    public Feature? Occupant(string slotId) =>
        Occupied.TryGetValue(slotId, out var featureId)
            ? Features.FirstOrDefault(f => f.Id == featureId)
            : null;

    /// <summary>
    /// Mette la feature nello slot e restituisce chi ne è stato buttato fuori.
    /// Occupare non fallisce mai: è una sostituzione, non una spesa.
    /// </summary>
    public Feature? Occupy(string slotId, Feature feature)
    {
        var precedente = Occupant(slotId);
        Occupied[slotId] = feature.Id;
        return ReferenceEquals(precedente, feature) ? null : precedente;
    }

    /// <summary>Lascia lo slot: in 5e si smette di concentrarsi quando si vuole.</summary>
    public void Release(string slotId) => Occupied.Remove(slotId);

    public IEnumerable<Source> SourcesOfType(SourceType type) =>
        Sources.Where(s => s.Type == type);

    /// <summary>
    /// Feature raggruppate per Fonte, la primitiva della UI collassabile.
    /// Una feature multi-Fonte compare in ogni gruppo ma il testo resta
    /// deduplicato a monte tramite DescriptionId.
    /// </summary>
    public ILookup<Source, Feature> FeaturesBySource()
    {
        var byId = Sources.ToDictionary(s => s.Id);
        return Features
            .SelectMany(f => f.SourceIds
                .Where(byId.ContainsKey)
                .Select(sid => (Source: byId[sid], Feature: f)))
            .ToLookup(x => x.Source, x => x.Feature);
    }

    /// <summary>
    /// Applica un riposo del ciclo indicato: ricarica i contatori d'uso e i
    /// pool di risorse il cui ciclo di ricarica è coperto (rango ≤), secondo
    /// il profilo di sistema.
    /// </summary>
    public void CompleteRest(string cycleId, GameProfile profile)
    {
        var completed = profile.Cycle(cycleId);
        if (completed is null)
            return;

        foreach (var feature in Features)
            feature.Usage?.Restore(completed, profile);

        foreach (var pool in profile.Pools)
            if (profile.Cycle(pool.RechargeCycle) is { } poolCycle && completed.Rank >= poolCycle.Rank)
                Resources.Restore(pool);
    }

    /// <summary>Inizio di un nuovo turno: ricarica le risorse "una volta per turno".</summary>
    public void StartNewTurn(GameProfile profile)
    {
        foreach (var feature in Features)
            feature.Usage?.RestoreForNewTurn(profile);
    }

    /// <summary>Danno o cura: i PF restano nell'intervallo [0, PF massimi effettivi].</summary>
    public void AdjustHp(int delta) => CurrentHp = Math.Clamp(CurrentHp + delta, 0, EffectiveMaxHp());

    /// <summary>Una feature con effetti è attiva se non è toggleable o se è accesa.</summary>
    public bool IsEffectFeatureActive(Feature feature) =>
        !feature.Toggleable || !EffectStates.TryGetValue(feature.Id, out var on) || on;

    private IEnumerable<Effect> ActiveEffects() =>
        Features.Where(f => f.Effects.Count > 0 && IsEffectFeatureActive(f)).SelectMany(f => f.Effects);

    /// <summary>PF massimi con gli effetti attivi applicati (Set sovrascrive, poi Add somma).</summary>
    public int EffectiveMaxHp()
    {
        var effects = ActiveEffects().Where(e => e.TargetsMaxHp).ToList();
        var baseValue = LastSet(effects) ?? MaxHp;
        return baseValue + effects.Where(e => e.Op == EffectOp.Add).Sum(e => e.Value);
    }

    /// <summary>
    /// Statistiche effettive: le base con gli effetti applicati, più eventuali
    /// statistiche introdotte solo da un effetto (es. "CA" che appare quando
    /// indossi un oggetto). I valori non numerici restano invariati.
    /// </summary>
    public IReadOnlyDictionary<string, object?> EffectiveStats()
    {
        var effects = ActiveEffects().Where(e => !e.TargetsMaxHp).ToList();
        var result = new Dictionary<string, object?>(CustomStats);

        var keys = CustomStats.Keys.Concat(effects.Select(e => e.Target)).Distinct();
        foreach (var key in keys)
        {
            var forKey = effects.Where(e => e.Target == key).ToList();
            if (forKey.Count == 0)
                continue; // statistica base senza effetti: invariata

            CustomStats.TryGetValue(key, out var baseRaw);
            var baseValue = LastSet(forKey) ?? (TryNumber(baseRaw, out var n) ? n : 0);
            result[key] = baseValue + forKey.Where(e => e.Op == EffectOp.Add).Sum(e => e.Value);
        }
        return result;
    }

    /// <summary>
    /// Limiti massimi dell'economia di turno (Azioni, Reazioni...).
    /// Parte dal GameProfile e vi somma gli effetti attivi marcati come @turn:Id.
    /// In questo modo un personaggio può avere un limite maggiore rispetto al
    /// sistema (es. due Azioni invece di una).
    /// </summary>
    public IReadOnlyDictionary<string, int> EffectiveTurnLimits(GameProfile profile)
    {
        var effects = ActiveEffects().Where(e => e.Target.StartsWith("@turn:")).ToList();
        var result = new Dictionary<string, int>();

        foreach (var res in profile.TurnResources)
        {
            var forKey = effects.Where(e => e.Target == $"@turn:{res.Id}").ToList();
            var baseValue = LastSet(forKey) ?? res.PerTurn;
            result[res.Id] = baseValue + forKey.Where(e => e.Op == EffectOp.Add).Sum(e => e.Value);
        }

        return result;
    }

    private static int? LastSet(IEnumerable<Effect> effects)
    {
        int? value = null;
        foreach (var e in effects.Where(e => e.Op == EffectOp.Set))
            value = e.Value;
        return value;
    }

    private static bool TryNumber(object? value, out int result) => TryReadNumber(value, out result);

    /// <summary>
    /// Legge un valore di statistica come numero.
    ///
    /// Pubblico perché non lo usa solo la scheda: lo usa anche il motore dei
    /// dadi per risolvere i riferimenti <c>@statistica</c>. Deve restare una
    /// sola implementazione — il caso <c>JsonElement</c> qui sotto non è
    /// teorico: dopo il giro in SQLite un 15 torna così, e un cast diretto
    /// risolverebbe ogni riferimento a 0 <em>solo</em> su un server riavviato,
    /// passando tutti i test in memoria.
    /// </summary>
    public static bool TryReadNumber(object? value, out int result)
    {
        switch (value)
        {
            case null: result = 0; return false;
            case int i: result = i; return true;
            case long l: result = (int)l; return true;
            case double d: result = (int)Math.Round(d); return true;
            case System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number:
                result = je.TryGetInt32(out var iv) ? iv : (int)Math.Round(je.GetDouble());
                return true;
            case string s when int.TryParse(s, out var sv): result = sv; return true;
            default:
                result = 0;
                return false;
        }
    }
}
