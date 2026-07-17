namespace TableLAN.Core.Sources;

/// <summary>
/// Entità Fonte (Capitolo 7 del documento master).
/// Ogni cosa che "genera" feature — classe, sottoclasse, razza, background,
/// oggetto, regola homebrew — è una Fonte. Il personaggio è una lista di
/// Fonti attive: il multiclasse non è un caso speciale, è solo il caso in
/// cui esistono più Fonti dello stesso tipo.
/// </summary>
public sealed class Source
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required SourceType Type { get; init; }

    /// <summary>
    /// Fonte genitore opzionale: una sottoclasse referenzia la sua classe,
    /// una variante referenzia la razza base, e così via.
    /// </summary>
    public string? ParentSourceId { get; init; }
}
