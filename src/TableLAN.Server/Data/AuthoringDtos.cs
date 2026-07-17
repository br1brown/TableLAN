namespace TableLAN.Server.Data;

using TableLAN.Core.Dice;
using TableLAN.Core.Features;

/// <summary>
/// Dati per creare una nuova feature via authoring (Master o giocatore).
/// Tutti i campi meccanici sono opzionali: una feature può essere una
/// magia da oggetto (costo Azione, cariche a riposo lungo), una mutazione
/// passiva (costo Nessuno), un tratto una-volta-per-turno, ecc. Può anche
/// portare effetti (modificatori) ed essere indossabile (on/off).
/// </summary>
public sealed record FeatureDraft(
    string ShortName,
    /// <summary>Costi di attivazione. Vuoto = passiva. Una magia ne ha due.</summary>
    IReadOnlyList<ActivationCost>? Costs = null,
    int? MaxUses = null,
    string? Recharge = null,
    string? Description = null,
    string? SourceId = null,
    string? CustomJson = null,
    IReadOnlyList<Effect>? Effects = null,
    bool Toggleable = false,
    // In coda: è un record posizionale, e i chiamanti esistenti passano per posizione.
    string? Roll = null,
    /// <summary>Risorse di turno accreditate dall'uso (Action Surge, Attacco Extra).</summary>
    IReadOnlyList<ActivationCost>? Grants = null,
    /// <summary>Slot esclusivo occupato finché è attiva (in 5e: "Concentration").</summary>
    string? Occupies = null);

/// <summary>Accende/spegne gli effetti di una feature indossabile su un personaggio.</summary>
public sealed record ToggleRequest(bool Active);

/// <summary>
/// Richiesta di tiro.
///
/// <paramref name="Spend"/> separa il tiro dal costo, perché non coincidono: il
/// Dardo Incantato spende uno slot e tira tre volte; l'Attacco Furtivo si somma
/// a un colpo già tirato. Il tap normale spende, la pressione lunga no.
/// </summary>
public sealed record RollRequest(
    string FeatureId,
    bool Spend = true,
    int Times = 1,
    string Keep = "Sum",
    int? SlotLevelOverride = null)
{
    /// <summary>Sum | Highest (vantaggio) | Lowest (svantaggio). Ignoto ⇒ Sum.</summary>
    // Nome pienamente qualificato: la property Keep maschera il tipo omonimo.
    public TableLAN.Core.Dice.Keep KeepMode =>
        Enum.TryParse<TableLAN.Core.Dice.Keep>(Keep, ignoreCase: true, out var mode)
            ? mode
            : TableLAN.Core.Dice.Keep.Sum;
}

/// <summary>Una nuova scheda, o il rinomino di una esistente.</summary>
public sealed record NewCharacterRequest(string? Name, int MaxHp = 10);

public sealed record AddSourceRequest(string Name, string Type, string? ParentSourceId = null);

public sealed record SetStatRequest(string Name, object? Value);

/// <summary>
/// Un livello di una riserva: slot di livello <paramref name="Level"/> per i
/// pool a livelli, <paramref name="Level"/> 0 per quelli a punti (ki, mana).
/// <paramref name="Max"/> nullo o ≤ 0 rimuove il livello; <paramref name="Remaining"/>
/// nullo lo riempie.
/// </summary>
public sealed record SetPoolRequest(string PoolId, int Level, int? Max, int? Remaining = null);

public sealed record HpDeltaRequest(int Delta);

/// <summary>
/// Imposta i PF: authoring, non gioco. Entrambi opzionali — si sale di livello
/// (solo il massimo) o si corregge un numero digitato male.
/// </summary>
public sealed record SetHpRequest(int? MaxHp, int? CurrentHp);

public sealed record IntentRequest(string FeatureId, int? SlotLevelOverride = null);

public sealed record RestRequest(string Cycle);

/// <summary>
/// Inventario completo e ordinato. Il client invia l'intera lista: aggiunta,
/// modifica quantità e riordino sono la stessa operazione (rimpiazzo), che
/// per un tavolo di poche persone è la scelta più semplice e senza conflitti.
/// </summary>
public sealed record InventoryPayload(List<InventoryItemDto> Items);

public sealed record InventoryItemDto(string? Id, string Name, int Quantity, string? Notes);

public sealed record MonsterDraft(string Name, int MaxHp, int ArmorClass, string? Notes);

public sealed record MonsterHpDelta(int Delta);

/// <summary>Riassegna il costo di una feature (rimappatura dopo un cambio di profilo).</summary>
