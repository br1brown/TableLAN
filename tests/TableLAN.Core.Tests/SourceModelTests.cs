namespace TableLAN.Core.Tests;

using TableLAN.Core.Characters;
using TableLAN.Core.Engine;
using TableLAN.Core.Features;
using TableLAN.Core.Sources;

/// <summary>
/// Verifica la tesi centrale del Capitolo 7: il multiclasse non è un caso
/// speciale, è solo un filtro sull'array di Fonti che restituisce più di
/// un elemento. La logica di raggruppamento è identica per mono e multi.
/// </summary>
public class SourceModelTests
{
    private static readonly Source Ladro = new() { Id = "src-ladro", Name = "Ladro", Type = SourceType.Class };
    private static readonly Source Monaco = new() { Id = "src-monaco", Name = "Monaco", Type = SourceType.Class };

    private static Character Multiclasse() => new()
    {
        Id = "pg-kael",
        Name = "Kael",
        Sources = [Ladro, Monaco],
        Features =
        [
            new Feature
            {
                Id = "ft-furtivo", ShortName = "Attacco Furtivo",
                SourceIds = ["src-ladro"], Costs = [], DescriptionId = "txt-furtivo",
            },
            new Feature
            {
                Id = "ft-schivata", ShortName = "Schivata Prodigiosa",
                SourceIds = ["src-ladro", "src-monaco"], Costs = [new ActivationCost("Reaction")], DescriptionId = "txt-schivata",
            },
            new Feature
            {
                Id = "ft-raffica", ShortName = "Raffica di Colpi",
                SourceIds = ["src-monaco"], Costs = [new ActivationCost("BonusAction")], DescriptionId = "txt-raffica",
            },
        ],
    };

    [Fact]
    public void Multiclasse_e_solo_un_filtro_sulle_fonti_di_tipo_classe()
    {
        var multi = Multiclasse();
        var mono = new Character { Id = "pg", Name = "Mono", Sources = [Ladro] };

        Assert.Equal(2, multi.SourcesOfType(SourceType.Class).Count());
        Assert.Single(mono.SourcesOfType(SourceType.Class));
    }

    [Fact]
    public void Le_feature_si_raggruppano_per_fonte_per_il_rendering_collassabile()
    {
        var groups = Multiclasse().FeaturesBySource();

        var colonnaLadro = groups[Ladro].Select(f => f.ShortName).ToList();
        var colonnaMonaco = groups[Monaco].Select(f => f.ShortName).ToList();

        Assert.Equal(["Attacco Furtivo", "Schivata Prodigiosa"], colonnaLadro);
        Assert.Equal(["Schivata Prodigiosa", "Raffica di Colpi"], colonnaMonaco);
    }

    [Fact]
    public void Feature_condivisa_tra_fonti_mantiene_un_solo_puntatore_di_descrizione()
    {
        var character = Multiclasse();
        var shared = character.Features.Single(f => f.Id == "ft-schivata");

        // Due Fonti, un solo testo: la deduplicazione è nel puntatore,
        // non in copie del testo per colonna.
        Assert.Equal(2, shared.SourceIds.Count);
        Assert.Equal("txt-schivata", shared.DescriptionId);

        var groups = character.FeaturesBySource();
        var occurrences = groups.SelectMany(g => g).Count(f => f.Id == "ft-schivata");
        Assert.Equal(2, occurrences);
        Assert.Single(character.Features.Where(f => f.Id == "ft-schivata"));
    }
}
