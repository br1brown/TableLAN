import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal, ViewEncapsulation } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CharacterDto, EffectDto, FeatureDto, InventoryItemDto, PoolDefDto, ProfileDto, RollEntryDto, DieView } from '../../models';
import { TableService } from '../../table.service';
import { costLabel, grantLabel } from '../../cost-label';
import { HpPanelComponent } from '../../components/hp-panel/hp-panel';
import { TavoloTabComponent } from '../../components/tavolo-tab/tavolo-tab';
import { FeatureRowComponent } from '../../components/feature-row/feature-row';

/** Un gruppo di feature dentro una tab (es. "Azione Bonus" dentro "Azioni"). */
interface FeatureGroup {
  label: string;
  icon: string;
  features: FeatureDto[];
  /**
   * Vero per i gruppi che nascono chiusi: i livelli di slot, dove la lista è
   * lunga e la si consulta dopo aver deciso *con cosa* pagare. Le Azioni no —
   * quelle devono leggersi senza toccare niente, è il motivo per cui apri
   * l'app.
   */
  chiusoDiDefault?: boolean;
}

/** Una tab della scheda. Non è cablata: si deriva dal profilo (vedi `tabs`). */
interface TabDef {
  id: string;
  label: string;
  icon: string;
  groups: FeatureGroup[];
}

@Component({
  selector: 'app-session',
  standalone: true,
  imports: [CommonModule, FormsModule, HpPanelComponent, TavoloTabComponent, FeatureRowComponent],
  templateUrl: './session.component.html',
  styleUrl: './session.component.scss',
  encapsulation: ViewEncapsulation.None,
})
export class SessionComponent implements OnInit {
  protected readonly table = inject(TableService);
  protected readonly router = inject(Router);

  protected readonly selectedId = signal<string | null>(null);

  goToEdit() {
    const id = this.selectedId();
    if (id) {
      this.router.navigate(['/edit', id]);
    }
  }
  /**
   * Vero quando il Master ha detto che tocca a te.
   *
   * È tutto ciò che l'iniziativa fa al giocatore: lo avvisa. Non gli ricarica
   * niente e non gli chiude niente — il turno lo apre e lo chiude lui. Le due
   * cose restano indipendenti apposta: il Master può spostare il segnalino,
   * correggere l'ordine o saltare un giro senza che una scheda ne risenta.
   */
  protected readonly isMyTurn = computed(() => {
    const id = this.selectedId();
    const active = this.table.state()?.initiative?.activeId;
    return !!id && !!active && id === active;
  });

  /** Id della tab scelta. Stringa, non union: le tab le decide il profilo. */
  protected readonly view = signal<string>('azioni');

  /**
   * Modalità combattimento: la scheda si riduce a ciò che puoi fare adesso.
   *
   * Si chiamava `focus`, dal nome della legge che la giustifica (Hick-Hyman), e
   * il bottone diceva già "In combattimento" perché "Focus" al tavolo non lo
   * capiva nessuno. Il nome sbagliato però era rimasto nel codice, e da lì
   * tornava a galla in ogni discorso: il pezzo si chiama come la cosa che fa,
   * non come il libro da cui viene.
   */
  protected readonly combattimento = signal(false);
  /** Menù di cornice: chiuso, si apre su richiesta. */
  protected readonly menu = signal(false);
  protected readonly newItem = { name: '', quantity: 1 };
  protected readonly expanded = signal<Set<string>>(new Set());
  protected readonly descriptions = signal<Map<string, string>>(new Map());

  /** Il profilo di sistema attivo: da qui il client ricava tutte le meccaniche. */
  protected readonly profile = computed<ProfileDto | null>(() => this.table.state()?.profile ?? null);

  protected readonly character = computed<CharacterDto | null>(() => {
    const state = this.table.state();
    const id = this.selectedId();
    return state?.characters.find(c => c.id === id) ?? null;
  });

  protected readonly otherCharacters = computed(() => {
    const state = this.table.state();
    const id = this.selectedId();
    if (!state) return [];
    return state.characters.filter(c => c.id !== id);
  });

  protected readonly isActiveTurn = computed(() => {
    const state = this.table.state();
    const id = this.selectedId();
    return state?.initiative?.activeId === id;
  });

  protected readonly rollLog = this.table.rollLog;

  /**
   * Chip dell'economia del turno, generate dal profilo (non più i 4 fissi).
   *
   * Una risorsa che non hai e che il turno non ti dà non compare: gli
   * "Attacco" nascono a zero e li concede l'azione di Attacco, quindi la chip
   * appare quando ne hai in mano — cioè quando ti serve saperlo — e sparisce
   * quando li hai spesi. Mostrarla sempre a "0/0" sarebbe una riga di rumore
   * su ogni scheda, comprese quelle che non attaccano mai.
   */
  protected readonly turnChips = computed(() => {
    const c = this.character();
    const p = this.profile();
    if (!c || !p) return [];
    return p.turnResources
      .map(tr => ({
        id: tr.id,
        label: tr.label,
        remaining: c.turn[tr.id] ?? 0,
        perTurn: c.turnMax?.[tr.id] ?? tr.perTurn,
      }))
      .filter(chip => chip.perTurn > 0 || chip.remaining > 0);
  });

  /** Cicli di riposo del profilo (esclusa la ricarica "a turno"). */
  protected readonly restCycles = computed(() => (this.profile()?.cycles ?? []).filter(cy => !cy.perTurn));

  /**
   * Passiva ≠ "non costa nulla". È la distinzione che mancava, ed è la ragione
   * per cui la scheda era illeggibile: l'Attacco Furtivo costa "None" ma è
   * un'azione a tutti gli effetti, e finiva tra i tratti accanto a un mantello.
   *
   * Si guarda cosa la feature ti fa <em>fare</em>: qualcosa da spendere, da
   * consumare o da tirare. Se non c'è niente di tutto questo, non è roba da
   * turno — è la scheda che ti descrive.
   *
   * Prima la passività si deduceva dall'<em>avere effetti</em>, e su una scheda
   * vera si vedeva il buco: "Astuzia Gnomesca — vantaggio sui TS mentali contro
   * la magia" non ha effetti che il motore sappia esprimere (il vantaggio non è
   * un numero), quindi non risultava passiva e finiva in fila fra i tuoi
   * attacchi. Su un Druido di 3° erano tredici righe di prosa in mezzo alle
   * azioni. Non è un difetto dell'import: è che il modello non ha un modo di
   * dire "questo è solo testo" — e non gli serve, perché non fare nulla di
   * meccanico è già dirlo.
   */
  private isPassive(f: FeatureDto): boolean {
    // Si indossa e si toglie: è un interruttore, e vive fra i tratti anche se
    // gli effetti li ha eccome.
    if (f.toggleable) return true;
    // I crediti contano quanto i costi: una feature che *dà* un'Azione è
    // qualcosa che fai — sepolta fra i tratti non la useresti mai.
    return f.costs.length === 0 && f.grants.length === 0 && !f.usage && !f.roll;
  }

  /** La risorsa di turno che paga questa feature, se ce n'è una. */
  private turnCostOf(f: FeatureDto): { kind: string; amount: number } | undefined {
    const p = this.profile();
    return f.costs.find(c => p?.turnResources.some(t => t.id === c.kind));
  }

  /**
   * La riserva <em>a livelli</em> che paga questa feature, se ce n'è una: in 5e,
   * lo slot di un incantesimo.
   *
   * Solo le riserve a livelli, non tutte. Una riserva a punti — il ki — non ha
   * livelli da cui farsi raggruppare: le tre opzioni del Monaco restano dove
   * uno le cerca, sotto "Azione Bonus". È la gerarchia dei livelli a meritare
   * una tab, non il fatto di essere una riserva.
   */
  private leveledPoolCostOf(f: FeatureDto): { kind: string; amount: number } | undefined {
    const p = this.profile();
    return f.costs.find(c => p?.pools.some(x => x.id === c.kind && x.kind === 'Leveled'));
  }

  /** Vero se il profilo attivo sa ancora cosa sono tutti i costi di questa feature. */
  private knownCosts(f: FeatureDto): boolean {
    const p = this.profile();
    if (!p) return false;
    return f.costs.every(c =>
      p.turnResources.some(t => t.id === c.kind) || p.pools.some(x => x.id === c.kind));
  }

  /**
   * Le tab: Azioni, una per riserva a livelli che il personaggio usa davvero,
   * Tratti, Zaino, Tavolo.
   *
   * Le magie stavano nella tab Azioni, e la ragione era buona: lanciare un
   * incantesimo <em>è</em> un'azione, e "cosa posso fare in questo turno" si
   * risponde in un posto solo. Ha retto finché le feature di prova erano sette.
   * Su un Druido di 3° vero — 71 capacità — il gruppo "Azione" ne conteneva
   * 42: sette schermate e mezzo di telefono, e quasi quattro di scroll per
   * arrivare a Moonbeam. Raggruppare per costo non discrimina più niente quando
   * il costo è lo stesso per tutti: per un incantatore preparato "costa
   * un'Azione" lo fanno tutte e quaranta.
   *
   * Quindi si raggruppa per la risorsa che per lui è davvero scarsa — il
   * livello di slot — perché è la domanda che si fa al tavolo: non «cosa costa
   * un'Azione», ma «col mio slot di 2° cosa lancio?».
   *
   * Il motivo vero di prima non si perde: sulla riga il costo continua a dire
   * "Azione + Slot incantesimo L2", quindi che sia la tua azione del turno
   * resta scritto dov'era. E i trucchetti, che slot non ne costano, restano
   * fra le Azioni: sono gli attacchi a volontà, e lì uno li cerca.
   */
  protected readonly tabs = computed<TabDef[]>(() => {
    const c = this.character();
    const p = this.profile();
    if (!c || !p) return [];

    const tabs: TabDef[] = [];

    const actionable = c.features.filter(f => !this.isPassive(f));
    // Chi paga con uno slot esce dalle Azioni e va nella tab della sua riserva.
    const conSlot = actionable.filter(f => this.leveledPoolCostOf(f));
    const senzaSlot = actionable.filter(f => !this.leveledPoolCostOf(f));

    const groups = [
      ...p.turnResources.map(tr => ({
        label: tr.label,
        icon: this.iconForCost(tr.id),
        features: senzaSlot.filter(f => this.turnCostOf(f)?.kind === tr.id),
      })),
      // Si fa, ma non consuma il turno: l'Attacco Furtivo, gli attacchi con
      // l'arma. Costo nullo non vuol dire passiva.
      {
        label: 'Sempre disponibili',
        icon: 'ra-perspective-dice-six',
        features: senzaSlot.filter(f => !this.turnCostOf(f) && this.knownCosts(f)),
      },
      // Costo che il profilo attivo non conosce più (cambio di sistema).
      {
        label: 'Altro',
        icon: 'ra-uncertainty',
        features: senzaSlot.filter(f => !this.turnCostOf(f) && !this.knownCosts(f)),
      },
    ].filter(g => g.features.length > 0);

    if (groups.length)
      tabs.push({ id: 'azioni', label: 'Azioni', icon: 'ra-crossed-swords', groups });

    // Una tab per riserva a livelli, e solo se ci paghi qualcosa: senza una
    // magia che costi uno slot, la tab non esiste — come i punti ki, che il
    // guerriero non si vede a zero.
    for (const pool of p.pools.filter(x => x.kind === 'Leveled')) {
      const sue = conSlot.filter(f => this.leveledPoolCostOf(f)!.kind === pool.id);
      if (!sue.length) continue;

      // Un gruppo per livello, dal più basso: è l'ordine della scheda cartacea
      // e quello in cui si spende — prima il piccolo, il grosso se serve.
      const livelli = [...new Set(sue.map(f => this.leveledPoolCostOf(f)!.amount || 1))].sort((a, b) => a - b);
      tabs.push({
        id: `pool-${pool.id}`,
        label: pool.label,
        icon: this.iconForCost(pool.id),
        groups: livelli.map(n => ({
          label: `Livello ${n}`,
          icon: this.iconForCost(pool.id),
          features: sue.filter(f => (this.leveledPoolCostOf(f)!.amount || 1) === n),
          chiusoDiDefault: true,
        })),
      });
    }

    const traits = c.features.filter(f => this.isPassive(f));
    if (traits.length)
      tabs.push({
        id: 'tratti',
        label: 'Tratti',
        icon: 'ra-aura',
        groups: [{ label: 'Tratti', icon: 'ra-aura', features: traits }],
      });

    tabs.push({ id: 'zaino', label: 'Zaino', icon: 'ra-potion', groups: [] });
    tabs.push({ id: 'tavolo', label: 'Tavolo', icon: 'ra-heart-tower', groups: [] });
    return tabs;
  });

  /**
   * Tab attiva. Il set di tab cambia da personaggio a personaggio (l'incantatrice
   * pura non ha una tab Azioni: la sua scheda è la sua lista di magie), quindi
   * si ricade sempre sulla prima quando la selezionata non esiste più.
   */
  protected readonly activeTab = computed<TabDef | null>(() => {
    const tabs = this.tabs();
    return tabs.find(t => t.id === this.view()) ?? tabs[0] ?? null;
  });

  /**
   * In combattimento: solo ciò che puoi fare adesso, nella tab in cui sei.
   * Smonta dal DOM il resto.
   *
   * Toglie poco a inizio scontro, ed è nella natura della cosa: con gli slot
   * pieni puoi fare tutto, e non c'è niente da nascondere. Serve dopo, quando
   * hai speso — che è quando ti scordi di averlo fatto.
   */
  private readonly gruppiInCombattimento = computed(() =>
    (this.activeTab()?.groups ?? [])
      .map(g => ({ ...g, features: g.features.filter(f => this.canUse(f)) }))
      .filter(g => g.features.length > 0));

  /**
   * I gruppi che la scheda disegna davvero.
   *
   * Il filtro esisteva già, ma nessun template lo leggeva: il tasto si
   * accendeva e non toglieva niente dalla lista. Il filtro c'era, il
   * collegamento no.
   */
  protected readonly visibleGroups = computed(() =>
    this.combattimento() ? this.gruppiInCombattimento() : (this.activeTab()?.groups ?? []));

  /**
   * Vero nelle tab da cui si agisce: le Azioni e le riserve a livelli. Non fra
   * i Tratti, nello Zaino o al Tavolo, dove non c'è nessun turno da chiudere.
   */
  protected readonly tabDaCuiSiAgisce = computed(() => {
    const id = this.activeTab()?.id;
    return id === 'azioni' || !!id?.startsWith('pool-');
  });

  /**
   * I gruppi che il giocatore ha aperto o chiuso a mano, per chiave
   * `tab/gruppo`. Solo gli scostamenti: il default lo dice il gruppo, e questo
   * lo scavalca.
   */
  private readonly aperturaManuale = signal<Record<string, boolean>>({});

  /** Il personaggio è nella chiave: la sua scheda è un'altra scheda, e ciò che
   *  hai aperto sul Druido non riguarda il Chierico. */
  private chiave(g: FeatureGroup): string {
    return `${this.selectedId()}/${this.activeTab()?.id}/${g.label}`;
  }

  protected gruppoAperto(g: FeatureGroup): boolean {
    return this.aperturaManuale()[this.chiave(g)] ?? !g.chiusoDiDefault;
  }

  protected apriChiudi(g: FeatureGroup): void {
    const k = this.chiave(g);
    this.aperturaManuale.update(m => ({ ...m, [k]: !this.gruppoAperto(g) }));
  }

  async ngOnInit(): Promise<void> {
    await this.table.connect();
  }

  protected select(id: string): void {
    this.selectedId.set(id);
  }

  protected forget(): void {
    this.selectedId.set(null);
  }

  private static readonly TURN_ICONS: Record<string, string> = {
    Action: 'ra-sword',
    BonusAction: 'ra-lightning',
    Reaction: 'ra-shield',
    Interaction: 'ra-hand',
  };

  private isNone(kind: string): boolean {
    return !kind || kind === 'None';
  }

  /** Classe icona RPG per un tipo di costo, dedotta dal profilo. */
  protected iconForCost(kind: string): string {
    if (this.isNone(kind)) return 'ra-aura';
    const p = this.profile();
    if (p && p.turnResources.some(t => t.id === kind)) return SessionComponent.TURN_ICONS[kind] ?? 'ra-hourglass';
    const pool = p?.pools.find(x => x.id === kind);
    if (pool) return pool.kind === 'Leveled' ? 'ra-crystal-wand' : 'ra-vial';
    return 'ra-aura';
  }

  /** Tutto quello che costa, insieme: "Azione + Slot L1". */
  protected costLabel(f: FeatureDto): string {
    return costLabel(this.profile(), f.costs);
  }

  /** Cosa rende: "+2 Attacco". */
  protected grantLabel(f: FeatureDto): string {
    return grantLabel(this.profile(), f.grants ?? []);
  }

  /** Smetti di concentrarti: in 5e si può sempre, e non costa niente. */
  protected release(slotId: string): void {
    const id = this.selectedId();
    if (id) void this.table.releaseExclusive(id, slotId);
  }

  /** L'icona la dà il costo principale, cioè quello che consuma il turno. */
  protected iconForFeature(f: FeatureDto): string {
    const turn = this.turnCostOf(f);
    return this.iconForCost(turn?.kind ?? f.costs[0]?.kind ?? 'None');
  }

  protected poolEntries(c: CharacterDto): { label: string; kind: string; tiers: { level: string; max: number; remaining: number }[] }[] {
    return Object.values(c.pools).map(pool => ({
      label: pool.label,
      kind: pool.kind,
      tiers: Object.entries(pool.tiers).map(([level, s]) => ({ level, ...s })),
    }));
  }

  /**
   * Le statistiche con, dove esiste, il modificatore già formattato col segno.
   *
   * È il numero che sommi al dado: "Forza 18" da sola non ti dice cosa fare,
   * e il segno serve perché "+4" e "−1" si leggono senza pensarci. Le
   * statistiche senza modificatore (la CA, l'Umanità di Vampiri) non ne
   * mostrano nessuno: non ce l'hanno, e inventarne uno sarebbe peggio.
   */
  protected customStatEntries(c: CharacterDto): { name: string; value: unknown; mod: string | null }[] {
    const mods = c.statModifiers ?? {};
    return Object.entries(c.customStats).map(([name, value]) => {
      const m = mods[name];
      return {
        name,
        value,
        mod: m === undefined ? null : (m >= 0 ? `+${m}` : `−${Math.abs(m)}`),
      };
    });
  }

  protected async toggleFeature(feature: FeatureDto): Promise<void> {
    const next = new Set(this.expanded());
    if (next.has(feature.id)) {
      next.delete(feature.id);
    } else {
      next.add(feature.id);
      // Lazy load del testo esplicativo, solo alla prima espansione.
      if (!this.descriptions().has(feature.descriptionId)) {
        const text = await this.table.description(feature.descriptionId);
        const map = new Map(this.descriptions());
        map.set(feature.descriptionId, text);
        this.descriptions.set(map);
      }
    }
    this.expanded.set(next);
  }

  protected async use(feature: FeatureDto): Promise<void> {
    const id = this.selectedId();
    if (!id) return;
    await this.table.submitIntent(id, feature.id);
  }

  /**
   * Vantaggio/svantaggio per il prossimo tiro.
   *
   * Uno solo per la scheda, non uno per riga: il Master dice "tira con
   * vantaggio" e poi tocchi il tiro che vuoi — mettere due bottoni su ogni
   * riga avrebbe triplicato la lista che questa app esiste per accorciare.
   */
  protected readonly keep = signal<'Sum' | 'Highest' | 'Lowest'>('Sum');

  protected toggleKeep(mode: 'Highest' | 'Lowest'): void {
    this.keep.update(k => (k === mode ? 'Sum' : mode));
  }

  /**
   * Tira e paga. Il dado lo tira il motore: qui si chiede soltanto.
   *
   * Il vantaggio torna da sé a "normale" dopo il tiro: in 5e vale per *quel*
   * tiro, e lasciarlo acceso è il modo per tirarne tre col vantaggio senza
   * accorgersene.
   */
  protected async rollFeature(feature: FeatureDto): Promise<void> {
    const id = this.selectedId();
    if (!id) return;
    await this.table.roll(id, feature.id, { keep: this.keep() });
    this.keep.set('Sum');
  }

  /** Il tiro appena fatto, solo se è di questo personaggio e recente. */
  protected readonly myRoll = computed(() => {
    const roll = this.table.lastRoll();
    return roll && roll.characterId === this.selectedId() ? roll : null;
  });

  // ---- Edit Mode Handlers ----
  protected saveFeatureEdit(feature: FeatureDto, newName: string): void {
    const id = this.selectedId();
    if (!id) return;
    // La logica di base è inviare il DTO aggiornato tramite fetch
    // Siccome il player client non ha TableService.updateFeature, lo chiamiamo noi:
    fetch(`/api/characters/${id}/features/${feature.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ...feature,
        shortName: newName
      })
    });
  }

  protected deleteFeature(feature: FeatureDto): void {
    const id = this.selectedId();
    if (!id) return;
    fetch(`/api/characters/${id}/features/${feature.id}`, { method: 'DELETE' });
  }

  /**
   * I dadi del tentativo che conta, col loro ruolo — il template li colora.
   * Lo scartato (vantaggio/svantaggio) resta in coda, vedi {@link tailOf}.
   */
  keptDice(roll: RollEntryDto): DieView[] {
    return roll.attempts[roll.keptIndex]?.dice ?? [];
  }

  /**
   * Ciò che segue i dadi: il modificatore e i tentativi scartati.
   *
   * Lo scartato si mostra come faccia, non come totale: stampava il totale
   * accanto alla faccia di quello tenuto, cioè due unità diverse scritte
   * uguali. Col vantaggio il modificatore è lo stesso per entrambi, quindi è
   * la faccia il numero che i due tentativi si contendono.
   */
  tailOf(roll: RollEntryDto): string {
    const kept = roll.attempts[roll.keptIndex];
    if (!kept) return '';
    const facce = (a: RollEntryDto['attempts'][number]) =>
      a.dice.map(d => `d${d.sides}:${d.value}`).join(' ');
    const mod = kept.modifier ? ` ${kept.modifier > 0 ? '+' : '−'} ${Math.abs(kept.modifier)}` : '';
    const discarded = roll.attempts.length > 1
      ? ` (scartato ${roll.attempts.filter((_, i) => i !== roll.keptIndex).map(facce).join(', ')})`
      : '';
    return `${mod}${discarded}`;
  }

  /** La chiave grezza dell'esito (Duality) diventa parola italiana. */
  outcomeLabel(outcome: string): string {
    return ({ hope: 'Speranza', fear: 'Paura', crit: 'Critico' } as Record<string, string>)[outcome] ?? outcome;
  }

  protected setView(viewId: string): void {
    this.view.set(viewId);
  }

  protected resetTurn(): void {
    const id = this.selectedId();
    if (id) void this.table.resetTurn(id);
  }

  /** Etichetta leggibile di un effetto: "CA +1", "PF max +5", "Forza = 18". */
  protected effectLabel(e: EffectDto): string {
    if (e.op === 'Set') return `${e.value} ${e.target}`;
    if (e.value < 0) return `${e.value} ${e.target}`;
    return `+${e.value} ${e.target}`;
  }

  protected boundEffectLabel = this.effectLabel.bind(this);

  protected toggleEffect(feature: FeatureDto): void {
    const id = this.selectedId();
    if (id) void this.table.toggleEffect(id, feature.id, !feature.active);
  }

  protected rest(cycleId: string): void {
    const id = this.selectedId();
    if (id) void this.table.rest(id, cycleId);
  }

  // ---- Inventario ordinabile: ogni modifica rimanda l'intera lista. ----

  private saveInventory(items: InventoryItemDto[]): void {
    const id = this.selectedId();
    if (id) void this.table.setInventory(id, items);
  }

  protected addItem(): void {
    const c = this.character();
    const name = this.newItem.name.trim();
    if (!c || !name) return;
    const items: InventoryItemDto[] = [
      ...c.inventory,
      { id: '', name, quantity: Number(this.newItem.quantity) || 1, notes: null },
    ];
    this.saveInventory(items);
    this.newItem.name = '';
    this.newItem.quantity = 1;
  }

  protected changeQuantity(item: InventoryItemDto, delta: number): void {
    const c = this.character();
    if (!c) return;
    const items = c.inventory
      .map(i => i.id === item.id ? { ...i, quantity: Math.max(0, i.quantity + delta) } : i)
      .filter(i => i.quantity > 0);
    this.saveInventory(items);
  }

  protected removeItem(item: InventoryItemDto): void {
    const c = this.character();
    if (!c) return;
    this.saveInventory(c.inventory.filter(i => i.id !== item.id));
  }

  protected moveItem(index: number, delta: number): void {
    const c = this.character();
    if (!c) return;
    const target = index + delta;
    if (target < 0 || target >= c.inventory.length) return;
    const items = [...c.inventory];
    [items[index], items[target]] = [items[target], items[index]];
    this.saveInventory(items);
  }

  /**
   * Il verdetto arriva dal motore dentro lo snapshot. Qui non si riesegue
   * nessuna regola: c'era una copia client-side di RuleEngine.Validate che
   * rispondeva solo "sì/no" e buttava via il motivo — ed è per questo che
   * "perché non posso riusarla?" non aveva risposta.
   */
  protected canUse(feature: FeatureDto): boolean {
    return feature.canUse;
  }

  /**
   * Da dove arriva la feature. La provenienza è un'etichetta, non una
   * destinazione: era una tab ("Fonti"), ma nessuno apre la scheda per
   * navigare le proprie Fonti — è il modello dati che affiorava nell'UI.
   * Una feature concessa da più Fonti le nomina tutte (deduplicazione).
   */
  protected sourceLabel(feature: FeatureDto): string {
    const c = this.character();
    if (!c) return '';
    return feature.sourceIds
      .map(id => c.sources.find(s => s.id === id)?.name)
      .filter((name): name is string => !!name)
      .join(' · ');
  }
}
