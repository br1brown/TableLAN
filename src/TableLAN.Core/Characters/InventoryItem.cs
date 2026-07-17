namespace TableLAN.Core.Characters;

/// <summary>
/// Voce d'inventario: oggetto trasportato con quantità e note libere.
/// L'ordine è quello della lista sul personaggio, così "ordinabile"
/// (Capitolo 9) è semplicemente riordinare l'array. Estensione a basso
/// impatto: nessuna regola, solo dati che il client mostra e riordina.
/// </summary>
public sealed class InventoryItem
{
    public required string Id { get; init; }

    public required string Name { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Notes { get; set; }
}
