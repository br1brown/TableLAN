namespace TableLAN.Core.Features;

/// <summary>
/// Costo di attivazione di una feature. Dopo l'introduzione del profilo di
/// sistema, <see cref="Kind"/> non è più un enum cablato ma un id-stringa
/// interpretato dal profilo: può essere una risorsa di turno ("Action"),
/// una riserva ("SpellSlot", "Ki"), o "None" per le passive.
/// <see cref="Amount"/> è il livello dello slot (pool a livelli), la quantità
/// di punti (pool a punti), o il numero di risorse di turno; 0 = 1 implicito.
/// </summary>
public readonly record struct ActivationCost(string Kind, int Amount = 0)
{
    public const string NoneKind = "None";

    public static readonly ActivationCost None = new(NoneKind);

    public bool IsNone => string.IsNullOrEmpty(Kind) || Kind == NoneKind;

    /// <summary>Quantità effettiva richiesta: almeno 1.</summary>
    public int EffectiveAmount => Amount > 0 ? Amount : 1;
}
