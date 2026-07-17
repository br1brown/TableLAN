namespace TableLAN.Core.Engine;

using TableLAN.Core.Characters;
using TableLAN.Core.Features;
using TableLAN.Core.Profile;

/// <summary>
/// Motore di validazione degli intenti — la fonte di verità (Capitolo 8).
/// Non conosce più le meccaniche di D&D: interpreta il costo di ogni feature
/// tramite il <see cref="GameProfile"/> (risorsa di turno, pool a livelli o a
/// punti, passiva). Validate è pura; Apply consuma le risorse solo dopo una
/// validazione riuscita, così stato e broadcast non divergono mai.
/// </summary>
public sealed class RuleEngine
{
    public ValidationResult Validate(Character character, Feature feature, TurnState turn, GameProfile profile)
    {
        if (!character.Features.Contains(feature))
            return ValidationResult.Fail(
                $"'{character.Name}' non possiede la feature '{feature.ShortName}'.");

        if (feature.Usage is { HasUsesLeft: false } usage)
            return ValidationResult.Fail(
                $"'{feature.ShortName}' non ha usi residui {RechargeHint(usage.Recharge, profile)}.");

        // Tutti i costi devono essere pagabili, non solo il primo: un
        // incantesimo che costa Azione e Slot non si lancia se hai lo slot ma
        // hai già agito. Si riporta il primo ostacolo, che è quello che il
        // giocatore deve risolvere per primo.
        foreach (var cost in feature.RealCosts)
        {
            var check = CanPay(character, cost, turn, profile);
            if (!check.IsValid)
                return check;
        }

        // Un credito verso una risorsa che il profilo non ha non è un dettaglio
        // da ignorare: la feature prometterebbe un'Azione in più e non darebbe
        // niente, in silenzio. Meglio fermarla e dirlo.
        foreach (var grant in feature.RealGrants)
        {
            if (profile.TurnResource(grant.Kind) is null)
                return ValidationResult.Fail(
                    $"'{feature.ShortName}' concede '{grant.Kind}', che non è una risorsa di turno del profilo '{profile.Name}'.");
        }

        return ValidationResult.Ok();
    }

    private static ValidationResult CanPay(Character character, ActivationCost cost, TurnState turn, GameProfile profile)
    {
        if (profile.TurnResource(cost.Kind) is { } turnResource)
        {
            return turn.CanPay(turnResource.Id, cost.EffectiveAmount)
                ? ValidationResult.Ok()
                : ValidationResult.Fail($"'{turnResource.Label}' non disponibile in questo turno.");
        }

        if (profile.Pool(cost.Kind) is { } pool)
        {
            return character.Resources.CanSpend(pool, cost.EffectiveAmount)
                ? ValidationResult.Ok()
                : ValidationResult.Fail(PoolShortage(pool, cost.EffectiveAmount));
        }

        return ValidationResult.Fail(
            $"Costo '{cost.Kind}' non definito nel profilo di sistema '{profile.Name}'.");
    }

    /// <summary>
    /// Valida e, se l'intento è lecito, consuma le risorse. Restituisce l'esito:
    /// chi chiama fa broadcast solo su successo, dopo la persistenza.
    /// </summary>
    public ValidationResult Apply(Character character, Intent intent, TurnState turn, GameProfile profile)
    {
        var feature = character.Features.FirstOrDefault(f => f.Id == intent.FeatureId);
        if (feature is null)
            return ValidationResult.Fail($"Feature '{intent.FeatureId}' inesistente per '{character.Name}'.");

        var result = Validate(character, feature, turn, profile);
        if (!result.IsValid)
            return result;

        // L'upcast si controlla PRIMA di spendere qualunque cosa: con più costi,
        // pagare l'Azione e poi scoprire che lo slot richiesto non c'è
        // lascerebbe il turno mutilato per un intento respinto.
        foreach (var cost in feature.RealCosts)
        {
            if (profile.Pool(cost.Kind) is not { Kind: PoolKind.Leveled } leveled)
                continue;

            var requested = intent.SlotLevelOverride ?? cost.EffectiveAmount;
            if (requested < cost.EffectiveAmount || !character.Resources.CanSpend(leveled, requested))
                return ValidationResult.Fail(
                    $"Slot di livello {requested} non spendibile per '{feature.ShortName}'.");
        }

        foreach (var cost in feature.RealCosts)
        {
            if (profile.TurnResource(cost.Kind) is { } turnResource)
            {
                turn.Pay(turnResource.Id, cost.EffectiveAmount);
            }
            else if (profile.Pool(cost.Kind) is { } pool)
            {
                var amount = pool.Kind == PoolKind.Leveled
                    ? intent.SlotLevelOverride ?? cost.EffectiveAmount
                    : cost.EffectiveAmount;
                character.Resources.Spend(pool, amount);
            }
        }

        // I crediti dopo i costi, non prima: una feature che costa un'Azione e
        // ne concede una non deve poter pagare con quella che sta per dare.
        foreach (var grant in feature.RealGrants)
        {
            if (profile.TurnResource(grant.Kind) is { } granted)
                turn.Grant(granted.Id, grant.EffectiveAmount);
        }

        feature.Usage?.Consume();

        // Occupare uno slot esclusivo è l'ultima cosa: il costo è già pagato,
        // quindi la sostituzione è un fatto compiuto. Si riporta chi è caduto
        // — la 5e non concede tiri per restare concentrati sulla prima.
        if (profile.Exclusive(feature.Occupies) is { } slot)
        {
            var caduta = character.Occupy(slot.Id, feature);
            if (caduta is not null)
                return ValidationResult.Ok($"{slot.Label} passata a '{feature.ShortName}': '{caduta.ShortName}' è terminata.");
        }

        return ValidationResult.Ok();
    }

    private static string RechargeHint(string cycleId, GameProfile profile) =>
        profile.Cycle(cycleId) is { } cycle
            ? $"prima di: {cycle.Label.ToLowerInvariant()}"
            : "al momento";

    private static string PoolShortage(ResourcePoolDef pool, int amount) =>
        pool.Kind == PoolKind.Leveled
            ? $"Nessuno slot di livello {amount} o superiore in '{pool.Label}'."
            : $"Punti insufficienti in '{pool.Label}' (servono {amount}).";
}
