namespace TableLAN.Server.Services;

using QRCoder;

/// <summary>
/// Genera il QR di join (Capitolo 6). Il QR viene rigenerato a ogni avvio
/// perché ingloba l'IP LAN corrente, soggetto a variazioni DHCP.
/// </summary>
public sealed class QrCodeService
{
    /// <summary>PNG del QR che punta all'URL dei giocatori.</summary>
    public byte[] GeneratePng(string url, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>Versione ASCII per log/console del Master.</summary>
    public string GenerateAscii(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var ascii = new AsciiQRCode(data);
        return ascii.GetGraphic(1, "██", "  ");
    }
}
