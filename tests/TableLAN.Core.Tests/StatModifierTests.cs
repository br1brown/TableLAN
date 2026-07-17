namespace TableLAN.Core.Tests;

using TableLAN.Core.Profile;

/// <summary>
/// Il modificatore è nato da un bug trovato giocando: <c>1d20+@Forza</c> con
/// Forza 18 tirava <c>1d20+18</c>. Legale, silenzioso, e assurdo — un attacco a
/// +18 al primo livello. Il punteggio non è mai ciò che sommi al d20.
/// </summary>
public sealed class StatModifierTests
{
    private static readonly StatModifier Dnd = new();   // Base 10, Div 2

    [Theory]
    // La tabella di D&D 5e, per intero: è il contratto.
    [InlineData(1, -5)]
    [InlineData(2, -4)]
    [InlineData(3, -4)]
    [InlineData(7, -2)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(14, 2)]
    [InlineData(18, 4)]
    [InlineData(19, 4)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void Il_modificatore_segue_la_tabella_di_D_and_D(int punteggio, int atteso) =>
        Assert.Equal(atteso, Dnd.Of(punteggio));

    /// <summary>
    /// La trappola: la divisione fra interi di C# tronca verso lo zero, quindi
    /// (7-10)/2 darebbe −1 invece di −2. Un personaggio scarso meno scarso del
    /// dovuto, e nessuno se ne accorge mai perché il numero è plausibile.
    /// </summary>
    [Fact]
    public void Sotto_la_media_si_arrotonda_verso_il_basso_non_verso_lo_zero()
    {
        Assert.Equal(-2, Dnd.Of(7));
        Assert.NotEqual((7 - 10) / 2, Dnd.Of(7));   // −1, che sarebbe sbagliato
    }

    [Fact]
    public void Una_statistica_senza_modificatore_vale_per_intero()
    {
        // La CA si somma tutta; l'Umanità di Vampiri non si somma a niente.
        var ca = new StatDef { Id = "ca", Label = "CA", Default = 10 };
        Assert.Equal(16, ca.Effective(16));
        Assert.Null(ca.Modifier);
    }

    [Fact]
    public void Una_statistica_con_modificatore_vale_il_modificatore()
    {
        var forza = new StatDef { Id = "for", Label = "Forza", Default = 10, Modifier = new() };
        Assert.Equal(4, forza.Effective(18));
    }

    [Fact]
    public void Un_altro_sistema_puo_avere_un_altro_passo()
    {
        // La regola è dichiarata dal profilo, non cablata: qui ogni 3 punti
        // sopra 12 vale +1. Il motore non sa che D&D esiste.
        var strano = new StatModifier { Base = 12, Div = 3 };
        Assert.Equal(0, strano.Of(12));
        Assert.Equal(1, strano.Of(15));
        Assert.Equal(-1, strano.Of(11));
    }

    [Fact]
    public void Un_passo_di_zero_non_divide_per_zero()
    {
        // Un profilo scritto a mano può contenere qualsiasi cosa: non deve far
        // esplodere un tiro al tavolo.
        var rotto = new StatModifier { Base = 10, Div = 0 };
        Assert.Equal(8, rotto.Of(18));
    }

    [Fact]
    public void Il_preset_di_D_and_D_da_il_modificatore_alle_caratteristiche_ma_non_alla_CA()
    {
        var p = GameProfiles.Dnd5e();

        Assert.NotNull(p.Stats.First(s => s.Label == "Forza").Modifier);
        Assert.NotNull(p.Stats.First(s => s.Label == "Costituzione").Modifier);
        // La CA è un bersaglio, non una caratteristica: 16 è 16.
        Assert.Null(p.Stats.First(s => s.Label == "CA").Modifier);
    }
}
