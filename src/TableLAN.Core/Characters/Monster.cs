namespace TableLAN.Core.Characters;

/// <summary>
/// Un Mostro è a tutti gli effetti un Personaggio, ma con alcune estensioni
/// specifiche per il Master (come gli appunti segreti o, in futuro, regole di
/// Grado di Sfida). Eredita tutta l'infrastruttura del motore delle regole:
/// TurnState, Features, Pools, Effetti e Statistiche.
/// </summary>
public sealed class Monster : Character
{
    public string? Notes { get; init; }
}
