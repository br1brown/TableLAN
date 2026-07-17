namespace TableLAN.Core.Sources;

/// <summary>
/// Tipo di una Fonte. Il motore non conosce D&D: questi tipi servono solo
/// al raggruppamento semantico e al rendering collassabile per Fonte.
/// </summary>
public enum SourceType
{
    Class,
    Subclass,
    Race,
    Background,
    Item,
    Homebrew,
    Other,
}
