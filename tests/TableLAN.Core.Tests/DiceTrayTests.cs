namespace TableLAN.Core.Tests;

using TableLAN.Core.Dice;
using TableLAN.Core.Profile;

/// <summary>
/// Il vassoio dei dadi di un preset è fatto di formule, e una formula seminata
/// nel codice ma illeggibile dal motore sarebbe un bottone che al tavolo dà solo
/// un errore. Questo test è il guardiano: ogni dado di ogni preset dev'essere
/// una formula che <see cref="DiceFormula.TryParse"/> accetta.
/// </summary>
public class DiceTrayTests
{
    public static IEnumerable<object[]> OgniDadoDiOgniPreset() =>
        GameProfiles.Presets()
            .SelectMany(p => p.Dice.Select(d => new object[] { p.Name, d.Formula }));

    [Theory]
    [MemberData(nameof(OgniDadoDiOgniPreset))]
    public void Ogni_dado_del_vassoio_e_una_formula_valida(string preset, string formula)
    {
        var ok = DiceFormula.TryParse(formula, out var parsed, out var error);
        Assert.True(ok, $"«{formula}» nel vassoio di {preset} non è una formula valida: {error}");
        Assert.NotNull(parsed);
    }

    [Fact]
    public void Ogni_preset_arriva_con_un_vassoio_non_vuoto()
    {
        // Il vuoto è legittimo a runtime (il client ricade sui poliedrici), ma un
        // preset di fabbrica senza dadi è quasi sempre una dimenticanza.
        foreach (var p in GameProfiles.Presets())
            Assert.NotEmpty(p.Dice);
    }

    [Fact]
    public void Ogni_preset_porta_i_suoi_stati()
    {
        // Ogni gioco ha stati diversi (Avvelenato in D&D, Scosso in Savage): un
        // preset senza vocabolario di stati lascerebbe il Master col solo campo
        // libero, perdendo il senso di averli per sistema.
        foreach (var p in GameProfiles.Presets())
            Assert.NotEmpty(p.Conditions);
    }

    [Fact]
    public void Ogni_tiro_di_statistica_di_ogni_preset_e_una_formula_valida()
    {
        // Un @Tratto scritto male nel Roll di una statistica (es. «@Agilita»
        // senza accento) darebbe un bottone che al tavolo dà solo un errore. Qui
        // ogni formula di tiro dev'essere analizzabile.
        foreach (var p in GameProfiles.Presets())
            foreach (var s in p.Stats.Where(s => !string.IsNullOrWhiteSpace(s.Roll)))
            {
                var ok = DiceFormula.TryParse(s.Roll, out _, out var error);
                Assert.True(ok, $"«{s.Roll}» ({s.Label} di {p.Name}) non è valida: {error}");
            }
    }

    [Fact]
    public void Daggerheart_tira_i_tratti_sulla_coppia_Duality()
    {
        // Il preset che dà senso al motore Duality: i Tratti si tirano su 2d12
        // Speranza/Paura, non su un d20. Se questo cambia, il sistema ha perso
        // la sua identità.
        var dh = GameProfiles.Presets().Single(p => p.Id == "daggerheart");
        Assert.All(
            dh.Stats.Where(s => !string.IsNullOrWhiteSpace(s.Roll)),
            s => Assert.StartsWith("duality:", s.Roll));
    }
}
