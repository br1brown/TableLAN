namespace TableLAN.Core.Tests;

using TableLAN.Core.Characters;
using TableLAN.Core.Features;
using TableLAN.Core.Sources;

/// <summary>
/// Gli effetti: una mutazione/oggetto non solo aggiunge abilità, ma modifica
/// davvero i valori della scheda finché è attiva.
/// </summary>
public class EffectTests
{
    private static Feature EffectFeature(string id, bool toggleable, params Effect[] effects) => new()
    {
        Id = id,
        ShortName = id,
        SourceIds = ["src"],
        Costs = [],
        DescriptionId = "txt",
        Effects = effects,
        Toggleable = toggleable,
    };

    private static Character Pg(params Feature[] features) => new()
    {
        Id = "pg",
        Name = "PG",
        MaxHp = 30,
        CurrentHp = 30,
        Sources = [new Source { Id = "src", Name = "S", Type = SourceType.Homebrew }],
        Features = [.. features],
    };

    [Fact]
    public void Una_mutazione_permanente_modifica_una_statistica()
    {
        var pg = Pg(EffectFeature("mut-artigli", toggleable: false, new Effect("Forza", EffectOp.Add, 2)));
        pg.CustomStats["Forza"] = 14;

        Assert.Equal(16, pg.EffectiveStats()["Forza"]);
    }

    [Fact]
    public void Piu_effetti_sulla_stessa_feature_si_applicano_tutti()
    {
        var pg = Pg(EffectFeature("mut", false,
            new Effect("Forza", EffectOp.Add, 2),
            new Effect("Carisma", EffectOp.Add, -2)));
        pg.CustomStats["Forza"] = 10;
        pg.CustomStats["Carisma"] = 10;

        Assert.Equal(12, pg.EffectiveStats()["Forza"]);
        Assert.Equal(8, pg.EffectiveStats()["Carisma"]);
    }

    [Fact]
    public void Set_sovrascrive_la_base_e_puo_introdurre_una_nuova_statistica()
    {
        var pg = Pg(EffectFeature("mantello", false, new Effect("CA", EffectOp.Set, 15)));
        // "CA" non esisteva tra le statistiche base: l'effetto la introduce.
        Assert.Equal(15, pg.EffectiveStats()["CA"]);
    }

    [Fact]
    public void Effetto_sui_PF_massimi_e_il_clamp_del_danno()
    {
        var pg = Pg(EffectFeature("vigore", false, new Effect(Effect.MaxHpTarget, EffectOp.Add, 5)));
        Assert.Equal(35, pg.EffectiveMaxHp());

        pg.CurrentHp = 35;
        pg.AdjustHp(10); // non supera i PF massimi effettivi
        Assert.Equal(35, pg.CurrentHp);
    }

    [Fact]
    public void Oggetto_indossabile_applica_gli_effetti_solo_da_acceso()
    {
        var cloak = EffectFeature("mantello", toggleable: true, new Effect("CA", EffectOp.Add, 1));
        var pg = Pg(cloak);
        pg.CustomStats["CA"] = 13;

        // Toggleable: default acceso.
        Assert.Equal(14, pg.EffectiveStats()["CA"]);

        // Spento: l'effetto non si applica.
        pg.EffectStates["mantello"] = false;
        Assert.Equal(13, pg.EffectiveStats()["CA"]);
    }
}
