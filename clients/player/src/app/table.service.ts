import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { CharacterDto, InventoryItemDto, IntentRejection, RollEntryDto, TableState } from './models';

/**
 * Canale col motore. Dopo il riorientamento lo strumento è la scheda
 * personale del giocatore, non un VTT condiviso: le azioni sono chiamate
 * REST sulla propria scheda (l'esito di un intento respinto torna nella
 * risposta, solo a chi ha agito), mentre SignalR serve solo a ricevere gli
 * aggiornamenti dal vivo — per esempio quando il Master aggiunge una
 * mutazione alla tua scheda.
 */
@Injectable({ providedIn: 'root' })
export class TableService {
  readonly state = signal<TableState | null>(null);
  readonly rejection = signal<IntentRejection | null>(null);
  readonly connected = signal(false);
  /** L'ultimo tiro del tavolo, chiunque l'abbia fatto. */
  readonly lastRoll = signal<RollEntryDto | null>(null);
  /** Log degli ultimi tiri per la vista Tavolo. */
  readonly rollLog = signal<RollEntryDto[]>([]);

  private connection?: signalR.HubConnection;
  private readonly descriptions = new Map<string, string>();

  async connect(): Promise<void> {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hub')
      .withAutomaticReconnect()
      .build();

    this.connection.on('stateChanged', (state: TableState) => this.state.set(state));
    // Evento separato dallo stato: un tiro è un fatto, non una proprietà della
    // scheda. Arriva anche dai tiri altrui — il tavolo li vede.
    this.connection.on('rollMade', (roll: RollEntryDto) => {
      this.lastRoll.set(roll);
      this.rollLog.update(log => [roll, ...log].slice(0, 30));
    });
    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onclose(() => this.connected.set(false));

    await this.connection.start();
    this.connected.set(true);
  }

  async submitIntent(characterId: string, featureId: string, slotLevelOverride: number | null = null): Promise<void> {
    const res = await fetch(`/api/characters/${characterId}/intent`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ featureId, slotLevelOverride }),
    });
    const body = (await res.json()) as { ok: boolean; reason?: string; notice?: string };
    if (!body.ok) {
      this.rejection.set({ characterId, featureId, reason: body.reason ?? 'Azione non consentita.' });
      setTimeout(() => this.rejection.set(null), 4000);
    } else if (body.notice) {
      // Riuscito, ma è caduto qualcosa: la Benedizione su cui concentravi. Non
      // è un rifiuto, e non deve leggersi come tale.
      this.notice.set(body.notice);
      setTimeout(() => this.notice.set(null), 5000);
    }
  }

  /** L'ultima conseguenza da leggere: intento riuscito, ma è caduto qualcosa. */
  readonly notice = signal<string | null>(null);

  /**
   * Tira il dado di una feature. `spend` false tira senza pagare il costo: il
   * Dardo Incantato spende uno slot e tira tre volte, l'Attacco Furtivo si
   * somma a un colpo già tirato.
   */
  async roll(characterId: string, featureId: string,
             opts: { spend?: boolean; times?: number; keep?: 'Sum' | 'Highest' | 'Lowest' } = {}): Promise<void> {
    const res = await fetch(`/api/characters/${characterId}/roll`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        featureId,
        spend: opts.spend ?? true,
        times: opts.times ?? 1,
        keep: opts.keep ?? 'Sum',
      }),
    });
    const body = (await res.json()) as { ok: boolean; reason?: string; notice?: string };
    if (!body.ok) {
      this.rejection.set({ characterId, featureId, reason: body.reason ?? 'Tiro non consentito.' });
      setTimeout(() => this.rejection.set(null), 4000);
    } else if (body.notice) {
      this.notice.set(body.notice);
      setTimeout(() => this.notice.set(null), 5000);
    }
    // L'esito arriva via SignalR come tutti gli altri: una sola strada.
  }

  resetTurn(characterId: string): Promise<Response> {
    return fetch(`/api/characters/${characterId}/reset-turn`, { method: 'POST' });
  }

  rest(characterId: string, cycle: string): Promise<Response> {
    return this.post(`/api/characters/${characterId}/rest`, { cycle });
  }

  adjustHp(characterId: string, delta: number): Promise<Response> {
    return this.post(`/api/characters/${characterId}/hp`, { delta });
  }

  /** Equipaggia/rimuovi: accende o spegne gli effetti di una feature indossabile. */
  toggleEffect(characterId: string, featureId: string, active: boolean): Promise<Response> {
    return this.post(`/api/characters/${characterId}/features/${featureId}/toggle`, { active });
  }

  /** Auto-aggiunta lato giocatore (Capitolo 10): "una sezione di aggiunta". */
  addFeature(characterId: string, draft: {
    shortName: string;
    costKind: string;
    slotLevel?: number;
    maxUses?: number | null;
    recharge?: string | null;
    description?: string | null;
  }): Promise<Response> {
    return this.post(`/api/characters/${characterId}/features`, draft);
  }

  updateFeature(characterId: string, featureId: string, data: any): Promise<Response> {
    return this.put(`/api/characters/${characterId}/features/${featureId}`, data);
  }

  /**
   * La scheda di un mostro, se chi chiede ha il diritto di vederla.
   *
   * Sta dietro `/api/admin/*`, che risponde **solo su 127.0.0.1**: dalla
   * console del Master arriva, da un telefono in LAN torna 403 e il mostro
   * resta invisibile. Il confine è di rete, non di interfaccia — è la stessa
   * app, ed è il server a decidere chi vede cosa (Capitoli 5 e 11).
   */
  async monster(monsterId: string): Promise<CharacterDto | null> {
    try {
      const res = await fetch(`/api/admin/monsters/${monsterId}`);
      return res.ok ? await res.json() : null;
    } catch {
      return null;
    }
  }

  /** Come updateFeature, ma per un mostro: rotta admin, stesso lavoro. */
  updateMonsterFeature(monsterId: string, featureId: string, data: any): Promise<Response> {
    return this.put(`/api/admin/monsters/${monsterId}/features/${featureId}`, data);
  }

  /**
   * Imposta i PF: è authoring, non gioco — "salgo di livello" o "avevo
   * digitato male". Il danno e la cura al tavolo restano `adjustHp`.
   */
  setHp(characterId: string, maxHp: number | null, currentHp: number | null): Promise<Response> {
    return this.put(`/api/characters/${characterId}/hp`, { maxHp, currentHp });
  }

  setMonsterHp(monsterId: string, maxHp: number | null, currentHp: number | null): Promise<Response> {
    return this.put(`/api/admin/monsters/${monsterId}/hp`, { maxHp, currentHp });
  }

  deleteFeature(characterId: string, featureId: string): Promise<Response> {
    return this.send('DELETE', `/api/characters/${characterId}/features/${featureId}`, null);
  }

  updateName(characterId: string, name: string): Promise<Response> {
    return this.put(`/api/characters/${characterId}/name`, { name });
  }

  updateStat(characterId: string, name: string, value: any): Promise<Response> {
    return this.put(`/api/characters/${characterId}/stats`, { name, value });
  }

  deleteStat(characterId: string, name: string): Promise<Response> {
    return this.send('DELETE', `/api/characters/${characterId}/stats/${encodeURIComponent(name)}`, null);
  }

  /** Smetti di occupare uno slot esclusivo: in 5e, molli la concentrazione. */
  releaseExclusive(characterId: string, slotId: string): Promise<Response> {
    return this.send('DELETE', `/api/characters/${characterId}/occupied/${encodeURIComponent(slotId)}`, null);
  }

  /** Un livello di una riserva: `max` a 0 lo toglie. Livello 0 = pool a punti. */
  setPool(characterId: string, poolId: string, level: number, max: number | null,
          remaining: number | null = null): Promise<Response> {
    return this.put(`/api/characters/${characterId}/pools`, { poolId, level, max, remaining });
  }

  /**
   * Rimpiazza l'intero inventario ordinato (Capitolo 9): aggiunta, modifica
   * quantità e riordino passano tutti da qui.
   */
  setInventory(characterId: string, items: InventoryItemDto[]): Promise<Response> {
    return this.put(`/api/characters/${characterId}/inventory`, { items });
  }

  private post(url: string, body: unknown): Promise<Response> {
    return this.send('POST', url, body);
  }

  private put(url: string, body: unknown): Promise<Response> {
    return this.send('PUT', url, body);
  }

  private send(method: string, url: string, body: unknown): Promise<Response> {
    return fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  /**
   * Testo esplicativo lazy-loaded (Capitolo 7): scaricato alla prima
   * espansione, poi riusato dalla cache — deduplicato per costruzione,
   * perché la chiave è il puntatore condiviso, non la feature.
   */
  async description(descriptionId: string): Promise<string> {
    if (!descriptionId) return '(nessuna descrizione)';
    const cached = this.descriptions.get(descriptionId);
    if (cached !== undefined) return cached;

    const res = await fetch(`/api/descriptions/${descriptionId}`);
    if (!res.ok) return '(descrizione non disponibile)';
    const body = (await res.json()) as { text: string };
    this.descriptions.set(descriptionId, body.text);
    return body.text;
  }
}
