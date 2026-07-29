namespace TableLAN.Server.Tests;

using TableLAN.Server.Services;

/// <summary>
/// Cambiare campagna a caldo non basta: chiudendo e riaprendo il programma il
/// Master si aspetta di ritrovare la campagna che stava usando, non di tornare
/// ogni volta a tablelan.db. Lo garantisce un puntatore accanto ai .db, scritto
/// allo switch e riletto all'avvio. Questi test coprono quel giro — senza
/// avviare il server, perché è pura logica di file.
/// </summary>
public sealed class CampaignPersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"tablelan-camp-{Guid.NewGuid():N}");

    public CampaignPersistenceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string Db(string name) => Path.Combine(_dir, name);
    private void TouchDb(string name) => File.WriteAllText(Db(name), string.Empty);

    [Fact]
    public void SwitchWritesPointer_AndRestartRemembersIt()
    {
        TouchDb("tablelan.db");
        TouchDb("drakkenheim.db");

        var svc = new CampaignService(Db("tablelan.db"));
        svc.SetCurrent(Db("drakkenheim.db"));

        // Il puntatore contiene il solo nome file (niente percorso).
        var pointer = Path.Combine(_dir, CampaignService.PointerFileName);
        Assert.True(File.Exists(pointer));
        Assert.Equal("drakkenheim.db", File.ReadAllText(pointer).Trim());

        // Al riavvio si ripartirebbe da qui, non da tablelan.db.
        Assert.Equal(Db("drakkenheim.db"), CampaignService.RememberedCampaign(_dir));
    }

    [Fact]
    public void NoPointer_MeansNoMemory()
    {
        TouchDb("tablelan.db");
        Assert.Null(CampaignService.RememberedCampaign(_dir));
    }

    [Fact]
    public void PointerToArchivedCampaign_FallsBackToDefault()
    {
        // Il puntatore c'è ma la campagna è stata rinominata/archiviata: non deve
        // aprire il vuoto, deve ricadere sul default (null qui, tablelan.db a monte).
        File.WriteAllText(Path.Combine(_dir, CampaignService.PointerFileName), "sparita.db");
        Assert.Null(CampaignService.RememberedCampaign(_dir));
    }

    [Fact]
    public void PointerIsNotListedAsACampaign()
    {
        TouchDb("tablelan.db");
        var svc = new CampaignService(Db("tablelan.db"));
        svc.SetCurrent(Db("tablelan.db"));   // scrive il puntatore

        var names = svc.List().Select(c => c.Name).ToList();
        Assert.Contains("tablelan.db", names);
        Assert.DoesNotContain(CampaignService.PointerFileName, names);
    }

    [Fact]
    public void PathFor_StaysInsideTheFolder_AndAddsDbExtension()
    {
        var svc = new CampaignService(Db("tablelan.db"));

        // Nome semplice: diventa <cartella>/nome.db
        Assert.Equal(Db("nuova.db"), svc.PathFor("nuova"));
        Assert.Equal(Db("nuova.db"), svc.PathFor("nuova.db"));

        // Tentativo di traversal: GetFileName lo riduce al solo nome, dentro la cartella.
        Assert.Equal(Db("passwd.db"), svc.PathFor("../../etc/passwd"));

        // Vuoto: niente percorso.
        Assert.Null(svc.PathFor(""));
        Assert.Null(svc.PathFor("   "));
    }
}
