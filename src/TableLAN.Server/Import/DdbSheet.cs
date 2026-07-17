namespace TableLAN.Server.Import;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

/// <summary>
/// I campi di una scheda esportata in PDF da D&D Beyond, nell'ordine in cui
/// stanno sulla pagina.
///
/// L'ordine non è un dettaglio: è l'unica cosa che dice a quale livello
/// appartiene un incantesimo. Il PDF elenca «=== 2nd LEVEL ===» e poi le magie
/// di quel livello, e i nomi dei campi (<c>spellName0</c>, <c>spellName1</c>…)
/// sono numerati di seguito senza mai nominare il livello. Una mappa
/// nome→valore perderebbe proprio l'informazione che serve.
///
/// I valori NON stanno nell'AcroForm: quei PDF non lo dichiarano affatto, e
/// ogni libreria che passa di lì trova zero campi. Stanno nei widget delle
/// annotazioni, uno per casella, ed è da lì che si leggono.
/// </summary>
public static class DdbSheet
{
    /// <summary>Coppie (nome del campo, valore) in ordine di pagina. Vuota se il PDF non ne ha.</summary>
    public static IReadOnlyList<(string Nome, string Valore)> Leggi(Stream pdf)
    {
        var campi = new List<(string, string)>();

        using var document = PdfDocument.Open(pdf);
        foreach (var pagina in document.GetPages())
        {
            foreach (var annotazione in pagina.GetAnnotations())
            {
                var dizionario = annotazione.AnnotationDictionary;
                if (!dizionario.TryGet(NameToken.Create("T"), out var nome) ||
                    !dizionario.TryGet(NameToken.Create("V"), out var valore))
                    continue;

                var n = Testo(nome);
                var v = Testo(valore);
                if (!string.IsNullOrEmpty(n) && !string.IsNullOrWhiteSpace(v))
                    campi.Add((n, Pulisci(v)));
            }
        }

        return campi;
    }

    private static string Testo(IToken token) => token switch
    {
        StringToken s => s.Data,
        HexToken h => h.Data,
        NameToken n => n.Data,
        NumericToken num => num.Data.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    /// <summary>
    /// I PDF di D&D Beyond scrivono i bullet come <c>\x95</c> e i separatori
    /// come <c>\xad</c>: fuori dalla loro codepage sono due caratteri che sullo
    /// schermo di un telefono non vogliono dire niente.
    /// </summary>
    private static string Pulisci(string s) =>
        s.Replace('\x95', '*').Replace('\xad', '-').Replace("\r\n", "\n").Replace('\r', '\n').Trim();
}
