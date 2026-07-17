namespace TableLAN.Core.Characters;

using TableLAN.Core.Profile;

/// <summary>
/// Le riserve di risorse del personaggio (i valori correnti; la struttura la
/// definisce il profilo). Generalizza il vecchio ResourcePool: gestisce sia
/// pool "a livelli" con upcasting (slot incantesimo 5e) sia pool "a punti"
/// (mana, ki). Internamente entrambi sono livello → (max, residui): un pool a
/// punti usa il solo livello 0.
/// </summary>
public sealed class ResourceBook
{
    // poolId → (livello → (max, residui))
    private readonly Dictionary<string, Dictionary<int, (int Max, int Remaining)>> _pools = new();

    public void SetTier(string poolId, int level, int max, int? remaining = null)
    {
        if (!_pools.TryGetValue(poolId, out var tiers))
            _pools[poolId] = tiers = new();
        tiers[level] = (max, remaining ?? max);
    }

    public IReadOnlyDictionary<string, Dictionary<int, (int Max, int Remaining)>> Snapshot() => _pools;

    public bool Has(string poolId) => _pools.ContainsKey(poolId);

    /// <summary>Residui di un dato livello di un pool (livello 0 per i pool a punti).</summary>
    public int Remaining(string poolId, int level = 0) =>
        _pools.TryGetValue(poolId, out var tiers) && tiers.TryGetValue(level, out var s) ? s.Remaining : 0;

    /// <summary>
    /// Vero se il costo è spendibile dal pool: per i pool a livelli serve uno
    /// slot di livello ≥ <paramref name="amount"/>; per quelli a punti serve
    /// che i residui coprano <paramref name="amount"/>.
    /// </summary>
    public bool CanSpend(ResourcePoolDef pool, int amount)
    {
        if (!_pools.TryGetValue(pool.Id, out var tiers))
            return false;

        return pool.Kind == PoolKind.Leveled
            ? tiers.Any(kv => kv.Key >= amount && kv.Value.Remaining > 0)
            : tiers.TryGetValue(0, out var s) && s.Remaining >= amount;
    }

    /// <summary>
    /// Spende dal pool. Per i pool a livelli consuma uno slot del livello più
    /// basso che soddisfa la richiesta (upcasting) e restituisce quel livello;
    /// per quelli a punti sottrae la quantità e restituisce 0.
    /// </summary>
    public int Spend(ResourcePoolDef pool, int amount)
    {
        if (!_pools.TryGetValue(pool.Id, out var tiers))
            throw new InvalidOperationException($"Pool '{pool.Id}' assente sul personaggio.");

        if (pool.Kind == PoolKind.Points)
        {
            var s = tiers.GetValueOrDefault(0);
            if (s.Remaining < amount)
                throw new InvalidOperationException($"Punti insufficienti nel pool '{pool.Id}'.");
            tiers[0] = (s.Max, s.Remaining - amount);
            return 0;
        }

        var level = tiers
            .Where(kv => kv.Key >= amount && kv.Value.Remaining > 0)
            .OrderBy(kv => kv.Key)
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Nessuno slot di livello {amount}+ nel pool '{pool.Id}'.");

        var slot = tiers[level];
        tiers[level] = (slot.Max, slot.Remaining - 1);
        return level;
    }

    /// <summary>Ricarica interamente un pool (tutti i livelli al massimo).</summary>
    public void Restore(ResourcePoolDef pool)
    {
        if (!_pools.TryGetValue(pool.Id, out var tiers))
            return;
        foreach (var level in tiers.Keys.ToList())
            tiers[level] = (tiers[level].Max, tiers[level].Max);
    }
}
