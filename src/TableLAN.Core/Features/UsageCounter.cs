namespace TableLAN.Core.Features;

using TableLAN.Core.Profile;

/// <summary>
/// Contatore di utilizzo di una feature: usi residui e ciclo di ripristino.
/// Il ciclo è un id-stringa (es. "PerTurn", "ShortRest", "LongRest") definito
/// dal profilo di sistema, non più un enum: un sistema può definire cicli
/// propri ("Scena", "Sessione") senza toccare il codice.
/// </summary>
public sealed class UsageCounter
{
    public required int MaxUses { get; init; }

    public int RemainingUses { get; set; }

    /// <summary>Id del ciclo di ricarica nel profilo (es. "LongRest").</summary>
    public required string Recharge { get; init; }

    public bool HasUsesLeft => RemainingUses > 0;

    public void Consume()
    {
        if (RemainingUses <= 0)
            throw new InvalidOperationException("Nessun uso residuo.");
        RemainingUses--;
    }

    /// <summary>
    /// Ripristina gli usi se il riposo completato copre il ciclo del contatore
    /// (rango maggiore o uguale nel profilo).
    /// </summary>
    public void Restore(RestCycle completedRest, GameProfile profile)
    {
        var mine = profile.Cycle(Recharge);
        if (mine is not null && completedRest.Rank >= mine.Rank)
            RemainingUses = MaxUses;
    }

    /// <summary>Ripristina gli usi che si ricaricano a ogni nuovo turno.</summary>
    public void RestoreForNewTurn(GameProfile profile)
    {
        if (profile.Cycle(Recharge)?.PerTurn == true)
            RemainingUses = MaxUses;
    }
}
