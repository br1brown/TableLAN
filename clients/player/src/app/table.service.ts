import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { CampaignInfo, CharacterDto, InitiativeEntryDto, InventoryItemDto, IntentRejection, RollEntryDto, TableState, UpdateStatus } from './models';

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

  /**
   * Tiro libero del giocatore: una formula qualunque ("2d6+3", "1d20+@Forza"),
   * fuori da ogni feature, per i tiri contestuali del tavolo. Non spende
   * niente; l'esito torna nel log come ogni altro. In caso di formula rotta
   * torna il perché, così la si può correggere.
   */
  async rollFree(characterId: string, formula: string,
                 opts: { label?: string; times?: number; keep?: 'Sum' | 'Highest' | 'Lowest' } = {}): Promise<string | null> {
    const res = await this.post(`/api/characters/${characterId}/roll-free`, {
      formula,
      label: opts.label ?? null,
      times: opts.times ?? 1,
      keep: opts.keep ?? 'Sum',
    });
    const body = (await res.json().catch(() => ({}))) as { ok: boolean; reason?: string };
    return body.ok ? null : (body.reason ?? 'Tiro non riuscito.');
  }

  /**
   * Tiro libero del Master: come sopra, ma sotto il nome "Master" e senza
   * scheda. Rotta admin (solo loopback): la fa la finestra del Master, il
   * tavolo la vede. Torna il messaggio d'errore, o null se è andata.
   */
  async rollFreeMaster(formula: string,
                       opts: { label?: string; times?: number; keep?: 'Sum' | 'Highest' | 'Lowest' } = {}): Promise<string | null> {
    const res = await this.post('/api/admin/roll', {
      formula,
      label: opts.label ?? null,
      times: opts.times ?? 1,
      keep: opts.keep ?? 'Sum',
    });
    const body = (await res.json().catch(() => ({}))) as { ok: boolean; reason?: string };
    return body.ok ? null : (body.reason ?? 'Tiro non riuscito.');
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

  /** Imposta i PF temporanei (il cuscinetto che il danno consuma per primo). */
  setTempHp(characterId: string, value: number): Promise<Response> {
    return this.post(`/api/characters/${characterId}/temp-hp`, { value });
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

  // ---- Rotte del Master (solo loopback) ----
  //
  // Sono le stesse `/api/admin/*` che la console statica chiamava a mano: da
  // un telefono in LAN tornano 403, dalla finestra del Master rispondono. La
  // UI del master è ora la stessa app Angular — il confine è di rete, non di
  // interfaccia (Capitoli 5 e 11).

  /** Le bestie del bestiario. Su un telefono in LAN torna 403 → lista vuota. */
  async monsters(): Promise<CharacterDto[]> {
    try {
      const res = await fetch('/api/admin/monsters');
      return res.ok ? await res.json() : [];
    } catch {
      return [];
    }
  }

  /** L'URL di join (IP LAN corrente) per il QR e la testata. */
  async joinUrl(): Promise<string | null> {
    try {
      const res = await fetch('/api/admin/join-info');
      return res.ok ? (await res.json()).url : null;
    } catch {
      return null;
    }
  }

  /** La versione incisa nel build (`v0.3+a1b2c3d` o `dev`). */
  async version(): Promise<string | null> {
    try {
      const res = await fetch('/api/admin/version');
      return res.ok ? (await res.json()).version : null;
    } catch {
      return null;
    }
  }

  /**
   * Controllo aggiornamenti: confronta la versione in esecuzione con l'ultima
   * release su GitHub. Best-effort — offline torna `updateAvailable: false`,
   * senza far rumore. Null solo se la rotta stessa non risponde (non-loopback).
   */
  async updateCheck(): Promise<UpdateStatus | null> {
    try {
      const res = await fetch('/api/admin/update');
      return res.ok ? await res.json() : null;
    } catch {
      return null;
    }
  }

  /** Aggiunge una scheda al tavolo: è il Master che decide chi siede. */
  addCharacter(name: string, maxHp: number): Promise<Response> {
    return this.post('/api/admin/characters', { name, maxHp });
  }

  removeCharacter(id: string): Promise<Response> {
    return this.send('DELETE', `/api/admin/characters/${id}`, null);
  }

  /** L'intera iniziativa: il server la riordina, il client non decide chi va prima. */
  setInitiative(order: InitiativeEntryDto[], activeId: string | null): Promise<Response> {
    return this.post('/api/admin/initiative', { order, activeId });
  }

  advanceTurn(): Promise<Response> {
    return this.post('/api/admin/initiative/next', {});
  }

  // Stati sul combattimento (avvelenato, prono…): su qualunque creatura, PG o
  // mostro. Rotta admin — è tracciamento del Master. Lo stato nuovo arriva via
  // SignalR come ogni altra mutazione.
  addCondition(refId: string, label: string, rounds: number | null): Promise<Response> {
    return this.post(`/api/admin/conditions/${refId}`, { label, rounds });
  }

  removeCondition(refId: string, conditionId: string): Promise<Response> {
    return this.send('DELETE', `/api/admin/conditions/${refId}/${conditionId}`, null);
  }

  // Bestiario: PF in combattimento (delta) e CRUD. Le HP/delete tornano la
  // lista aggiornata, come faceva la console.
  addMonster(name: string, maxHp: number, armorClass: number, notes: string | null): Promise<Response> {
    return this.post('/api/admin/monsters', { name, maxHp, armorClass, notes });
  }

  async adjustMonsterHp(id: string, delta: number): Promise<CharacterDto[] | null> {
    const res = await this.post(`/api/admin/monsters/${id}/hp`, { delta });
    return res.ok ? res.json() : null;
  }

  async deleteMonster(id: string): Promise<CharacterDto[] | null> {
    const res = await this.send('DELETE', `/api/admin/monsters/${id}`, null);
    return res.ok ? res.json() : null;
  }

  /** Sdoppia un mostro: una nuova istanza con PF pieni e un nome numerato. */
  async duplicateMonster(id: string): Promise<CharacterDto[] | null> {
    const res = await this.post(`/api/admin/monsters/${id}/duplicate`, {});
    return res.ok ? res.json() : null;
  }

  /**
   * Importa una scheda da un PDF di D&D Beyond. Il file non esce di qui: la
   * rotta è dietro loopback e il PDF si legge in memoria e basta.
   */
  async importDdb(file: File): Promise<{ ok: boolean; data: any }> {
    const body = new FormData();
    body.append('file', file);
    const res = await fetch('/api/admin/import/ddb', { method: 'POST', body });
    const data = await res.json().catch(() => ({}));
    return { ok: res.ok, data };
  }

  // Profilo di sistema: le meccaniche (turno, riserve, cicli, statistiche).
  async profile(): Promise<any> {
    const res = await fetch('/api/admin/profile');
    return res.ok ? res.json() : null;
  }

  async profilePresets(): Promise<any[]> {
    const res = await fetch('/api/admin/profile/presets');
    return res.ok ? res.json() : [];
  }

  async saveProfile(profile: unknown): Promise<{ ok: boolean; data: any }> {
    const res = await this.put('/api/admin/profile', profile);
    const data = await res.json().catch(() => ({}));
    return { ok: res.ok, data };
  }

  // Campagne: ognuna è un file .db, cambiabile a caldo. Lo stato nuovo arriva
  // via SignalR come ogni altra mutazione; qui torna solo l'esito.
  async campaigns(): Promise<{ current: string; campaigns: CampaignInfo[] }> {
    try {
      const res = await fetch('/api/admin/campaigns');
      return res.ok ? await res.json() : { current: '', campaigns: [] };
    } catch {
      return { current: '', campaigns: [] };
    }
  }

  async switchCampaign(name: string): Promise<{ ok: boolean; data: any }> {
    const res = await this.post('/api/admin/campaigns/switch', { name });
    return { ok: res.ok, data: await res.json().catch(() => ({})) };
  }

  async newCampaign(name: string): Promise<{ ok: boolean; data: any }> {
    const res = await this.post('/api/admin/campaigns/new', { name });
    return { ok: res.ok, data: await res.json().catch(() => ({})) };
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
