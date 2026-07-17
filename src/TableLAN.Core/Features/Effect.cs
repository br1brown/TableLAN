namespace TableLAN.Core.Features;

using System.Text.Json.Serialization;

/// <summary>
/// Effetto di una feature: un modificatore che il motore applica a un valore
/// della scheda finché la feature è attiva. È così che una mutazione o un
/// oggetto "modificano" davvero la scheda (es. Forza +2, CA a 15, PF max +5,
/// Contaminazione +1), non solo aggiungendo abilità.
///
/// <see cref="Target"/> è la chiave del valore colpito: una statistica per
/// nome (es. "Forza", "CA", "Contaminazione") oppure il valore riservato
/// <see cref="MaxHpTarget"/> per i punti ferita massimi.
/// </summary>
public sealed record Effect(
    string Target,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EffectOp Op,
    int Value)
{
    /// <summary>Bersaglio riservato: i punti ferita massimi.</summary>
    public const string MaxHpTarget = "@maxhp";

    public bool TargetsMaxHp => Target == MaxHpTarget;
}

public enum EffectOp
{
    /// <summary>Somma (usa un valore negativo per sottrarre).</summary>
    Add,

    /// <summary>Imposta il valore (sovrascrive la base).</summary>
    Set,
}
