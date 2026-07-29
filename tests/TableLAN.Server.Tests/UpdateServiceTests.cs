namespace TableLAN.Server.Tests;

using TableLAN.Server.Services;

/// <summary>
/// Il controllo aggiornamenti confronta la versione in esecuzione con l'ultima
/// release. Due cose devono reggere: il confronto dei tag (numerico, non
/// alfabetico) e il non fare mai rumore quando la rete non c'è. Il fetch è
/// iniettato, così i test non toccano GitHub.
/// </summary>
public class UpdateServiceTests
{
    [Theory]
    // latest più recente ⇒ aggiornamento
    [InlineData("v0.2", "v0.3", true)]
    [InlineData("v0.2+95b6ac5", "v0.3", true)]   // il +commit non conta nel confronto
    [InlineData("v0.9", "v0.10", true)]          // numerico: 10 > 9, non alfabetico
    [InlineData("v1.0", "v1.0.1", true)]
    // uguale o più vecchia ⇒ niente
    [InlineData("v0.3", "v0.3", false)]
    [InlineData("v0.3+abc1234", "v0.3", false)]
    [InlineData("v0.10", "v0.9", false)]
    // build locale o tag illeggibile ⇒ mai un falso allarme
    [InlineData("dev", "v0.3", false)]
    [InlineData("v0.3", "boh", false)]
    public void IsNewer_confronta_i_tag_numero_per_numero(string current, string latest, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewer(current, latest));
    }

    [Fact]
    public async Task Quando_ce_ne_una_piu_nuova_lo_dice_con_tanto_di_link()
    {
        var svc = new UpdateService("v0.2+95b6ac5",
            _ => Task.FromResult<(string, string?)?>(("v0.3", "https://example/rel/v0.3")));

        var status = await svc.CheckAsync();

        Assert.True(status.UpdateAvailable);
        Assert.Equal("v0.2+95b6ac5", status.Current);
        Assert.Equal("v0.3", status.Latest);
        Assert.Equal("https://example/rel/v0.3", status.Url);
    }

    [Fact]
    public async Task Gia_all_ultima_non_propone_niente()
    {
        var svc = new UpdateService("v0.3",
            _ => Task.FromResult<(string, string?)?>(("v0.3", "https://example/rel/v0.3")));

        var status = await svc.CheckAsync();

        Assert.False(status.UpdateAvailable);
        Assert.Equal("v0.3", status.Latest);
    }

    [Fact]
    public async Task Offline_non_e_un_errore_ma_un_non_so()
    {
        // Il fetch esplode (nessuna rete): l'esito è "nessun aggiornamento noto",
        // non un'eccezione che risale al Master.
        var svc = new UpdateService("v0.2", _ => throw new HttpRequestException("no network"));

        var status = await svc.CheckAsync();

        Assert.False(status.UpdateAvailable);
        Assert.Null(status.Latest);
        Assert.Equal("v0.2", status.Current);
    }

    [Fact]
    public async Task Il_risultato_si_tiene_in_cache_e_non_richiede_ogni_volta()
    {
        var chiamate = 0;
        var svc = new UpdateService("v0.2", _ =>
        {
            chiamate++;
            return Task.FromResult<(string, string?)?>(("v0.3", "u"));
        });

        await svc.CheckAsync();
        await svc.CheckAsync();
        await svc.CheckAsync();

        Assert.Equal(1, chiamate);   // una sola vera richiesta, il resto è cache
    }
}
