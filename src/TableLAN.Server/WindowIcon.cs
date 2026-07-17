namespace TableLAN.Server;

using System.Runtime.InteropServices;

/// <summary>
/// L'icona della finestra nativa, tirata fuori dall'assembly su disco.
///
/// Photino la vuole come percorso a un file, e nel pubblicato a file singolo
/// un file non c'è: l'icona è una risorsa incorporata. Qui la si scrive nel
/// temporaneo e si restituisce il percorso.
///
/// Windows la vuole .ico, GTK la vuole .png: non è una preferenza, sono due
/// API diverse che leggono due formati diversi.
///
/// Se qualcosa va storto si torna null e il chiamante tira dritto: un'icona
/// mancante è una finestra un po' brutta, non una serata persa.
/// </summary>
internal static class WindowIcon
{
    public static string? EstraiPercorso()
    {
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var risorsa = windows ? "TableLAN.icona.ico" : "TableLAN.icona.png";
        var nome = windows ? "tablelan.ico" : "tablelan.png";

        try
        {
            using var dentro = typeof(WindowIcon).Assembly.GetManifestResourceStream(risorsa);
            if (dentro is null)
                return null;

            // Una cartella nostra dentro il temporaneo: scrivere "tablelan.ico"
            // nella radice del temp è un buon modo per litigare con qualcun altro.
            var cartella = Path.Combine(Path.GetTempPath(), "tablelan-icona");
            Directory.CreateDirectory(cartella);
            var percorso = Path.Combine(cartella, nome);

            // Si riscrive a ogni avvio: costa niente ed è l'unico modo perché
            // un aggiornamento che cambia l'icona non resti indietro.
            using (var fuori = File.Create(percorso))
                dentro.CopyTo(fuori);

            return percorso;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
