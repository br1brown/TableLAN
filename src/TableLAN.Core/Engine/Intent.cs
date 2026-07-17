namespace TableLAN.Core.Engine;

/// <summary>
/// Intento di gioco: "il personaggio X vuole attivare la feature Y".
/// Gli intenti sono l'unico modo in cui lo stato cambia; il motore li
/// valida come fonte di verità prima che qualunque cosa venga inoltrata
/// sul canale realtime (Capitolo 8).
/// </summary>
public sealed record Intent(string CharacterId, string FeatureId)
{
    /// <summary>
    /// Livello di slot scelto dal giocatore per l'upcasting; null lascia
    /// al motore lo slot minimo richiesto dalla feature.
    /// </summary>
    public int? SlotLevelOverride { get; init; }
}
