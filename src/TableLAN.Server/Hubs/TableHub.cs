namespace TableLAN.Server.Hubs;

using Microsoft.AspNetCore.SignalR;
using TableLAN.Server.Services;

/// <summary>
/// Canale realtime di sola ricezione. Dopo il riorientamento le mutazioni
/// (intenti, riposi, HP, authoring del Master) passano da rotte REST che
/// rispondono subito al chiamante e poi ridiffondono lo stato qui: così
/// quando il Master aggiunge una mutazione, la scheda del giocatore si
/// aggiorna dal vivo senza che il client debba interrogare il server.
/// </summary>
public sealed class TableHub : Hub
{
    public const string StateChanged = "stateChanged";

    /// <summary>
    /// Un tiro è avvenuto. Evento separato da <see cref="StateChanged"/> perché
    /// un tiro è un fatto, non uno stato: infilarlo nello snapshot lo farebbe
    /// riconsegnare a ogni modifica dei PF, e rigiocare a ogni riconnessione.
    /// </summary>
    public const string RollMade = "rollMade";

    private readonly GameStateService _state;

    public TableHub(GameStateService state) => _state = state;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync(StateChanged, _state.Snapshot());
    }
}
