namespace TableLAN.Core.Engine;

/// <summary>
/// Esito della validazione di un intento.
///
/// Tre casi, non due: riuscito, riuscito <em>con una conseguenza da dire</em>,
/// respinto. Il caso di mezzo esiste per gli slot esclusivi — lanciare una
/// seconda magia di concentrazione riesce sempre, e proprio per questo il
/// giocatore deve sapere che la prima è appena caduta.
/// </summary>
/// <param name="IsValid">Falso solo se l'intento è stato respinto.</param>
/// <param name="Reason">Perché è stato respinto. Null se è andata.</param>
/// <param name="Notice">Cos'è successo per strada, a intento riuscito.</param>
public sealed record ValidationResult(bool IsValid, string? Reason = null, string? Notice = null)
{
    public static ValidationResult Ok() => new(true);

    /// <summary>Riuscito, ma con una conseguenza che il giocatore deve leggere.</summary>
    public static ValidationResult Ok(string notice) => new(true, null, notice);

    public static ValidationResult Fail(string reason) => new(false, reason);
}
