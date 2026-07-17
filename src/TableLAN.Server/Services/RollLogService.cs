namespace TableLAN.Server.Services;

using TableLAN.Core.Dice;

/// <summary>Un tiro avvenuto, pronto da mostrare.</summary>
/// <param name="Id">Distingue due tiri identici, e permette il "solo i nuovi".</param>
public sealed record RollEntry(
    long Id,
    string CharacterId,
    string CharacterName,
    string Label,
    string Formula,
    int Total,
    bool Spent,
    IReadOnlyList<RollAttemptView> Attempts,
    int KeptIndex);

/// <param name="Dice">Facce e valore di ogni dado, per mostrarli davvero.</param>
public sealed record RollAttemptView(IReadOnlyList<int[]> Dice, int Modifier, int Total);

/// <summary>
/// Cronologia dei tiri della sessione.
///
/// Vive in memoria, e non su disco, deliberatamente. Il precedente in casa è
/// <c>IntentLogRow</c>: viene scritto a ogni intento e non è letto da nessuna
/// parte — una tabella morta. Un RollLogRow farebbe la stessa fine, in più con
/// una modifica di schema.
///
/// L'argomento del Capitolo 11 ("l'intento validato è scritto prima del
/// broadcast") riguarda l'atomicità della <em>mutazione di stato</em>: e la
/// mutazione di un tiro — il costo speso — è già persistita da
/// PersistIntentAsync. L'esito del dado non muta nulla: è un evento, e gli
/// eventi di una serata al tavolo non sopravvivono alla serata.
///
/// Il buffer serve anche alla riconnessione: chi arriva tardi vede gli ultimi
/// tiri invece del vuoto.
/// </summary>
public sealed class RollLogService
{
    private const int Capacity = 50;

    private readonly object _gate = new();
    private readonly LinkedList<RollEntry> _entries = new();
    private long _nextId = 1;

    public RollEntry Add(string characterId, string characterName, string label, bool spent, RollResult result)
    {
        lock (_gate)
        {
            var entry = new RollEntry(
                _nextId++,
                characterId,
                characterName,
                label,
                result.Formula,
                result.Total,
                spent,
                result.Attempts
                    .Select(a => new RollAttemptView(
                        a.Dice.Select(d => new[] { d.Sides, d.Value }).ToList(),
                        a.Modifier,
                        a.Total))
                    .ToList(),
                result.KeptIndex);

            _entries.AddLast(entry);
            while (_entries.Count > Capacity)
                _entries.RemoveFirst();

            return entry;
        }
    }

    /// <summary>Gli ultimi tiri, dal più vecchio al più recente.</summary>
    public IReadOnlyList<RollEntry> Recent()
    {
        lock (_gate)
            return [.. _entries];
    }
}
