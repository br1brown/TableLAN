namespace TableLAN.Server.Services;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// Scopre l'indirizzo IP LAN corrente della macchina del Master.
/// Con DHCP l'IP cambia tra un avvio e l'altro (Capitolo 6): questo
/// servizio viene interrogato a ogni avvio per rigenerare il QR.
/// </summary>
public sealed class LanDiscoveryService
{
    /// <summary>
    /// IP locale in uscita: apre un socket UDP verso un indirizzo esterno
    /// (nessun pacchetto viene inviato) e legge quale interfaccia il
    /// sistema operativo sceglierebbe. Più affidabile dell'enumerazione
    /// delle interfacce quando ce n'è più di una.
    /// </summary>
    public IPAddress GetLanAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endpoint)
                return endpoint.Address;
        }
        catch (SocketException)
        {
            // Nessuna rete raggiungibile: si ripiega sull'enumerazione.
        }

        var fallback = Dns.GetHostAddresses(Dns.GetHostName())
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                 && !IPAddress.IsLoopback(a));
        return fallback ?? IPAddress.Loopback;
    }

    public string GetPlayerUrl(int port) => $"http://{GetLanAddress()}:{port}/";
}
