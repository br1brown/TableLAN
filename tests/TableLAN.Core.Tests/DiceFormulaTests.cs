namespace TableLAN.Core.Tests;

using TableLAN.Core.Dice;

/// <summary>Dado scriptato: i "tiri" li decide il test, non la sorte.</summary>
internal sealed class ScriptedRandom(params int[] values) : IRandomSource
{
    private int _cursor;

    public int Next(int minInclusive, int maxExclusive)
    {
        Assert.True(_cursor < values.Length, "Il motore ha tirato più dadi del previsto.");
        var value = values[_cursor++];
        Assert.InRange(value, minInclusive, maxExclusive - 1);
        return value;
    }

    public int Rolled => _cursor;
}

public class DiceFormulaParseTests
{
    [Theory]
    [InlineData("1d6")]
    [InlineData("d6")]
    [InlineData("2d6+3")]
    [InlineData("1d10-1")]
    [InlineData("3")]
    [InlineData("1d20+@CA")]
    [InlineData("@{Sanità Mentale}")]
    [InlineData("1d20+@maxhp")]
    [InlineData("1d8 + 2 - 1d4")]
    [InlineData("duality: 2d12+@Agilità")]
    [InlineData("duality: 1d12+1d12")]
    public void Formule_valide_si_analizzano(string input)
    {
        Assert.True(DiceFormula.TryParse(input, out var formula, out var error), $"{input} → {error}");
        Assert.NotNull(formula);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0d6")]
    [InlineData("1d0")]
    [InlineData("1d1")]
    [InlineData("1d6+")]
    [InlineData("abc")]
    [InlineData("@")]
    [InlineData("@{aperta")]
    [InlineData("100000d6")]
    [InlineData("1d6 * 2")]
    [InlineData("duality: 1d20")]        // un dado solo, non due
    [InlineData("duality: 1d12+1d8")]   // facce diverse
    [InlineData("duality: 2d12 >= 6")]  // la Duality non conta successi
    [InlineData("duality: @Forzad12")]  // dadi-da-statistica, non una coppia fissa
    [InlineData("")]
    [InlineData(null)]
    public void Formule_invalide_falliscono_spiegando_perche(string? input)
    {
        Assert.False(DiceFormula.TryParse(input, out var formula, out var error));
        Assert.Null(formula);
        Assert.False(string.IsNullOrWhiteSpace(error), "Un rifiuto senza motivo non serve a nessuno.");
    }

    [Fact]
    public void Un_dado_senza_quantita_ne_vale_uno()
    {
        Assert.True(DiceFormula.TryParse("d20", out var formula, out _));
        var term = Assert.Single(formula!.Terms);
        Assert.Equal(1, term.Count);
        Assert.Equal(20, term.Sides);
    }

    [Fact]
    public void Il_riferimento_ai_PF_massimi_e_lo_stesso_token_degli_effetti()
    {
        // Se questi divergono, "@maxhp" vuol dire due cose diverse nel dominio.
        Assert.Equal(Core.Features.Effect.MaxHpTarget, "@" + DiceFormula.MaxHpRef);
    }
}

public class DiceFormulaEvaluateTests
{
    private static readonly Dictionary<string, object?> NoStats = [];

    private static RollResult Roll(string input, IRandomSource rng, RollOptions? options = null,
        Dictionary<string, object?>? stats = null)
    {
        Assert.True(DiceFormula.TryParse(input, out var formula, out var parseError), parseError);
        var verdict = formula!.TryEvaluate(stats ?? NoStats, options ?? RollOptions.Single, rng, "Tizio", out var result);
        Assert.True(verdict.IsValid, verdict.Reason);
        return result!;
    }

    [Fact]
    public void Somma_dadi_e_modificatore()
    {
        var result = Roll("2d6+3", new ScriptedRandom(4, 5));

        Assert.Equal(12, result.Total);
        Assert.Equal(3, result.Kept.Modifier);
        Assert.Equal([new DieRoll(6, 4), new DieRoll(6, 5)], result.Kept.Dice);
    }

    [Fact]
    public void Un_termine_sottratto_toglie_dal_totale_ma_resta_un_dado_visibile()
    {
        var result = Roll("1d20-1d4", new ScriptedRandom(18, 3));

        Assert.Equal(15, result.Total);
        // Il d4 si vede col suo valore positivo: è il totale a scendere.
        Assert.Equal([new DieRoll(20, 18), new DieRoll(4, 3)], result.Kept.Dice);
    }

    [Fact]
    public void Con_vantaggio_vince_il_piu_alto_ma_lo_scartato_resta_visibile()
    {
        var result = Roll("1d20+2", new ScriptedRandom(7, 19), new RollOptions(2, Keep.Highest));

        Assert.Equal(21, result.Total);
        Assert.Equal(2, result.Attempts.Count);
        Assert.True(result.HasDiscarded);
        // Metà dell'informazione è il dado buttato: chi tira lo vuole vedere.
        Assert.Equal(9, result.Attempts[0].Total);
        Assert.Equal(1, result.KeptIndex);
    }

    [Fact]
    public void Con_svantaggio_vince_il_piu_basso()
    {
        var result = Roll("1d20", new ScriptedRandom(17, 4), new RollOptions(2, Keep.Lowest));

        Assert.Equal(4, result.Total);
        Assert.Equal(1, result.KeptIndex);
    }

    [Fact]
    public void Un_totale_negativo_non_si_clampa()
    {
        // Chi tira decide cosa farne: il motore non addolcisce i numeri.
        Assert.Equal(-4, Roll("1d4-5", new ScriptedRandom(1)).Total);
    }

    [Fact]
    public void Duality_speranza_quando_il_primo_dado_e_piu_alto()
    {
        var result = Roll("duality: 2d12+@Agilità", new ScriptedRandom(7, 3),
            stats: new() { ["Agilità"] = 2 });

        Assert.Equal(12, result.Total);              // 7 + 3 + 2
        Assert.Equal("hope", result.Outcome);
        // Il primo dado è la Speranza, il secondo la Paura, e i ruoli si vedono.
        Assert.Equal([new DieRoll(12, 7, "hope"), new DieRoll(12, 3, "fear")], result.Kept.Dice);
    }

    [Fact]
    public void Duality_paura_quando_il_secondo_dado_e_piu_alto()
    {
        var result = Roll("duality: 2d12", new ScriptedRandom(3, 7));

        Assert.Equal(10, result.Total);
        Assert.Equal("fear", result.Outcome);
    }

    [Fact]
    public void Duality_critico_quando_i_due_dadi_sono_uguali()
    {
        var result = Roll("duality: 2d12+1", new ScriptedRandom(9, 9));

        Assert.Equal(19, result.Total);              // 9 + 9 + 1
        Assert.Equal("crit", result.Outcome);
    }

    [Fact]
    public void Un_tiro_normale_non_ha_esito()
    {
        // La generalizzazione è additiva: senza modalità, nessun esito.
        Assert.Null(Roll("2d6+3", new ScriptedRandom(4, 5)).Outcome);
    }
}

public class StatRefTests
{
    /// <summary>
    /// La tesi del progetto: un oggetto equipaggiato cambia da solo ogni tiro
    /// che referenzia la statistica, senza che i dadi sappiano di oggetti.
    /// Senza questo test la tesi è una speranza.
    /// </summary>
    [Fact]
    public void Un_oggetto_equipaggiato_cambia_il_tiro_che_referenzia_la_statistica()
    {
        var mantello = new Features.Feature
        {
            Id = "mantello",
            ShortName = "Mantello della Protezione",
            SourceIds = ["src"],
            Costs = [],
            DescriptionId = "txt",
            Toggleable = true,
            Effects = [new Features.Effect("CA", Features.EffectOp.Add, 1)],
        };
        var kael = new Characters.Character
        {
            Id = "pg",
            Name = "Kael",
            Features = [mantello],
            CustomStats = { ["CA"] = 15 },
        };

        Assert.True(DiceFormula.TryParse("1d20+@CA", out var formula, out _));

        // Indossato: 10 + (15 + 1).
        var on = formula!.TryEvaluate(kael.EffectiveStats(), RollOptions.Single, new ScriptedRandom(10), "Kael", out var withCloak);
        Assert.True(on.IsValid);
        Assert.Equal(26, withCloak!.Total);

        // Tolto: l'effetto si spegne, e il tiro se ne accorge da solo.
        kael.EffectStates["mantello"] = false;
        var off = formula.TryEvaluate(kael.EffectiveStats(), RollOptions.Single, new ScriptedRandom(10), "Kael", out var without);
        Assert.True(off.IsValid);
        Assert.Equal(25, without!.Total);
    }

    /// <summary>
    /// Il bug che si vedrebbe solo dopo un riavvio: dopo il giro in SQLite le
    /// statistiche tornano come JsonElement, non come int.
    /// </summary>
    [Fact]
    public void Una_statistica_arrivata_da_SQLite_come_JsonElement_si_risolve()
    {
        var stats = new Dictionary<string, object?>
        {
            ["CA"] = System.Text.Json.JsonDocument.Parse("15").RootElement,
        };

        Assert.True(DiceFormula.TryParse("@CA", out var formula, out _));
        var verdict = formula!.TryEvaluate(stats, RollOptions.Single, new ScriptedRandom(), "Kael", out var result);

        Assert.True(verdict.IsValid, verdict.Reason);
        Assert.Equal(15, result!.Total);
    }

    [Fact]
    public void Una_statistica_col_nome_spaziato_si_referenzia_con_le_graffe()
    {
        var stats = new Dictionary<string, object?> { ["Sanità Mentale"] = 10 };

        Assert.True(DiceFormula.TryParse("@{Sanità Mentale}+2", out var formula, out _));
        var verdict = formula!.TryEvaluate(stats, RollOptions.Single, new ScriptedRandom(), "Lyra", out var result);

        Assert.True(verdict.IsValid, verdict.Reason);
        Assert.Equal(12, result!.Total);
    }

    [Fact]
    public void Una_statistica_inesistente_ferma_il_tiro_prima_di_tirare()
    {
        var rng = new ScriptedRandom(6);
        Assert.True(DiceFormula.TryParse("1d6+@Pippo", out var formula, out _));

        var verdict = formula!.TryEvaluate(NoStats, RollOptions.Single, rng, "Kael", out var result);

        Assert.False(verdict.IsValid);
        Assert.Contains("Pippo", verdict.Reason);
        Assert.Null(result);
        // Nessun dado consumato: un tiro mostrato e poi annullato è peggio di
        // un tiro mai fatto.
        Assert.Equal(0, rng.Rolled);
    }

    private static readonly Dictionary<string, object?> NoStats = [];
}

/// <summary>
/// I pool a successi: tanti dadi quanto dice un punteggio, e si contano le
/// facce invece di sommarle. È ciò che serve al Mondo di Tenebra — e finché non
/// c'era, il preset di Vampiri aveva l'economia del turno giusta e i dadi no.
/// </summary>
public sealed class DicePoolTests
{
    private static readonly Dictionary<string, object?> Scheda = new()
    {
        ["Destrezza"] = 4,
        ["Furtività"] = 3,
        ["Umanità"] = 7,
    };

    private static RollResult Tira(string formula, params int[] facce)
    {
        Assert.True(DiceFormula.TryParse(formula, out var f, out var err), err);
        Assert.True(f!.TryEvaluate(Scheda, RollOptions.Single, new ScriptedRandom(facce),
            "Prova", out var r).IsValid);
        return r!;
    }

    [Fact]
    public void Quanti_dadi_lo_dice_il_punteggio()
    {
        // Destrezza 4 -> quattro d10, non "4d10" scritto a mano.
        var r = Tira("@Destrezzad10", 7, 2, 9, 5);
        Assert.Equal(4, r.Kept.Dice.Count);
        Assert.All(r.Kept.Dice, d => Assert.Equal(10, d.Sides));
    }

    [Fact]
    public void Il_pool_segue_il_punteggio_quando_cambia()
    {
        Assert.True(DiceFormula.TryParse("@Destrezzad10", out var f, out _));
        var poca = new Dictionary<string, object?> { ["Destrezza"] = 2 };
        f!.TryEvaluate(poca, RollOptions.Single, new ScriptedRandom(3, 4), "X", out var r);
        Assert.Equal(2, r!.Kept.Dice.Count);
    }

    [Fact]
    public void Due_pool_si_sommano_nella_quantita()
    {
        // Vampiri: Destrezza 4 + Furtività 3 = sette d10.
        var r = Tira("@Destrezzad10+@{Furtività}d10", 1, 2, 3, 4, 5, 6, 7);
        Assert.Equal(7, r.Kept.Dice.Count);
    }

    [Fact]
    public void Con_la_soglia_si_contano_i_successi_non_si_somma()
    {
        // 7,2,9,5 con soglia 6 -> due successi (7 e 9). La somma farebbe 23.
        var r = Tira("@Destrezzad10>=6", 7, 2, 9, 5);
        Assert.Equal(2, r.Total);
    }

    [Fact]
    public void Il_totale_di_un_pool_e_il_numero_di_successi_non_i_pallini()
    {
        var r = Tira("@Destrezzad10 >= 6", 10, 10, 10, 10);
        Assert.Equal(4, r.Total);   // quattro successi, non 40
    }

    [Fact]
    public void Zero_successi_e_un_esito_legittimo()
    {
        var r = Tira("@Destrezzad10>=6", 1, 2, 3, 4);
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public void Una_prova_di_Vampiri_per_intero()
    {
        // Destrezza 4 + Furtività 3 = 7d10, successi a 6+.
        var r = Tira("@Destrezzad10+@{Furtività}d10>=6", 6, 1, 10, 3, 8, 5, 7);
        Assert.Equal(7, r.Kept.Dice.Count);
        Assert.Equal(4, r.Total);   // 6, 10, 8, 7
    }

    [Fact]
    public void Un_punteggio_a_zero_non_tira_dadi_e_non_esplode()
    {
        var vuota = new Dictionary<string, object?> { ["Destrezza"] = 0 };
        Assert.True(DiceFormula.TryParse("@Destrezzad10>=6", out var f, out _));
        var esito = f!.TryEvaluate(vuota, RollOptions.Single, new ScriptedRandom(5), "X", out var r);
        Assert.True(esito.IsValid);
        Assert.Empty(r!.Kept.Dice);
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public void Un_pool_su_una_statistica_inesistente_si_ferma_prima_di_tirare()
    {
        Assert.True(DiceFormula.TryParse("@Boh d10>=6", out var f, out _));
        var esito = f!.TryEvaluate(Scheda, RollOptions.Single, new ScriptedRandom(5), "Prova", out _);
        Assert.False(esito.IsValid);
        Assert.Contains("Boh", esito.Reason);
    }

    [Fact]
    public void La_soglia_senza_dadi_non_ha_senso()
    {
        Assert.False(DiceFormula.TryParse("@Forza>=6", out _, out var err));
        Assert.Contains("non tira dadi", err);
    }

    [Theory]
    [InlineData("@Destrezzad10>", "'>=', non '>'")]
    [InlineData("@Destrezzad10>=", "Manca il numero")]
    [InlineData("@Destrezzad10>=6+1", "non può esserci altro")]
    [InlineData("@Destrezzad10>=0", "almeno 1")]
    public void Una_soglia_scritta_male_lo_dice(string formula, string atteso)
    {
        Assert.False(DiceFormula.TryParse(formula, out _, out var err));
        Assert.Contains(atteso, err);
    }

    [Fact]
    public void Senza_soglia_un_pool_somma_come_sempre()
    {
        // @Destrezzad10 senza '>=' resta una somma: 7+2+9+5 = 23.
        var r = Tira("@Destrezzad10", 7, 2, 9, 5);
        Assert.Equal(23, r.Total);
    }

    [Fact]
    public void Il_riferimento_nudo_resta_quello_di_sempre()
    {
        // @Umanità senza 'dM' vale il punteggio, non tira niente.
        var r = Tira("1d10+@Umanità", 3);
        Assert.Single(r.Kept.Dice);
        Assert.Equal(10, r.Total);   // 3 + 7
    }
}

/// <summary>
/// La formula come la legge il giocatore sul bottone. Il bottone diceva
/// "1d20+@Destrezza+@Competenza": al tavolo il numero che serve è +6.
/// </summary>
public class DiceFormulaDescribeTests
{
    private static readonly Dictionary<string, object?> Scheda = new()
    {
        ["Destrezza"] = 4,
        ["Competenza"] = 2,
        ["Umanità"] = 7,
    };

    private static string? Descrivi(string formula)
    {
        Assert.True(DiceFormula.TryParse(formula, out var f, out var err), err);
        return f!.Describe(Scheda);
    }

    [Theory]
    [InlineData("1d20+@Destrezza+@Competenza", "1d20+6")]
    [InlineData("1d8+@Destrezza", "1d8+4")]
    [InlineData("2d6", "2d6")]
    [InlineData("1d4+1", "1d4+1")]
    [InlineData("1d20-@Destrezza", "1d20−4")]
    [InlineData("3", "3")]
    public void I_punteggi_entrano_nella_formula(string formula, string atteso) =>
        Assert.Equal(atteso, Descrivi(formula));

    [Fact]
    public void Le_costanti_si_sommano_ai_punteggi_in_un_numero_solo() =>
        // 1d10+@Destrezza+4 → +4 dalla scheda, +4 scritto: un solo "+8".
        Assert.Equal("1d10+8", Descrivi("1d10+@Destrezza+4"));

    [Fact]
    public void Un_modificatore_che_si_annulla_sparisce() =>
        Assert.Equal("1d20", Descrivi("1d20+@Destrezza-4"));

    [Fact]
    public void Un_pool_dice_quanti_dadi_cadono() =>
        // @Destrezzad10: quattro d10, non "@Destrezza d10".
        Assert.Equal("4d10", Descrivi("@Destrezzad10"));

    [Fact]
    public void La_soglia_dei_successi_resta_leggibile() =>
        Assert.Equal("4d10 ≥6", Descrivi("@Destrezzad10>=6"));

    [Fact]
    public void Una_statistica_che_non_esiste_non_inventa_un_numero()
    {
        // Meglio la formula grezza in faccia che un "+0" che mente.
        Assert.True(DiceFormula.TryParse("1d20+@Carisma", out var f, out _));
        Assert.Null(f!.Describe(Scheda));
    }
}

/// <summary>
/// Il vantaggio è due tentativi, non un'etichetta. Chi lo chiede non deve
/// doversi ricordare anche di chiedere il secondo dado.
/// </summary>
public class RollOptionsTests
{
    [Fact]
    public void Il_vantaggio_tira_due_volte_anche_se_nessuno_lo_chiede()
    {
        // Prima serviva new RollOptions(2, Keep.Highest): con Times implicito
        // il motore tirava una volta e il vantaggio spariva senza dirlo.
        Assert.Equal(2, new RollOptions(Keep: Keep.Highest).EffectiveTimes);
        Assert.Equal(2, new RollOptions(Keep: Keep.Lowest).EffectiveTimes);
    }

    [Fact]
    public void Senza_vantaggio_resta_un_tentativo_solo() =>
        Assert.Equal(1, new RollOptions().EffectiveTimes);

    [Fact]
    public void Chi_ne_chiede_di_piu_li_ottiene() =>
        // I tre dardi del Dardo Incantato restano tre, anche col vantaggio.
        Assert.Equal(3, new RollOptions(3, Keep.Highest).EffectiveTimes);

    [Fact]
    public void Il_tetto_dei_dieci_tentativi_regge() =>
        Assert.Equal(10, new RollOptions(99, Keep.Highest).EffectiveTimes);

    [Fact]
    public void Con_vantaggio_il_piu_alto_vince_davvero()
    {
        // Senza passare Times: è il caso che la scheda produce.
        var r = Roll("1d20", new ScriptedRandom(3, 18), new RollOptions(Keep: Keep.Highest));
        Assert.Equal(18, r.Total);
        Assert.True(r.HasDiscarded);
        Assert.Equal(2, r.Attempts.Count);
    }

    private static RollResult Roll(string formula, IRandomSource rng, RollOptions options)
    {
        Assert.True(DiceFormula.TryParse(formula, out var f, out _));
        Assert.True(f!.TryEvaluate(new Dictionary<string, object?>(), options, rng, "test", out var r).IsValid);
        return r!;
    }
}
