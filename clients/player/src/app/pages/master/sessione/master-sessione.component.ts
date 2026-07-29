import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CharacterDto, InitiativeEntryDto, RollEntryDto, dieText } from '../../../models';
import { TableService } from '../../../table.service';
import { DiceRollerComponent } from '../../../components/dice-roller/dice-roller';

/**
 * Console di sessione del Master: il tavolo (schede con PF e riserve),
 * l'iniziativa a due punti d'ingresso (giocatori e bestie) e il log dei tiri.
 *
 * È il port di wwwroot/admin/index.html. Due cose spariscono, ed è il motivo
 * per cui portarla in Angular snellisce: i tiri non arrivano più via SSE ma
 * dal signal `rollLog` che TableService già alimenta con SignalR; lo stato non
 * si ripolla ogni 3s, cambia da solo a ogni broadcast. Resta un solo poll,
 * leggero, per il bestiario, che nello snapshot non c'è.
 */
@Component({
  selector: 'app-master-sessione',
  standalone: true,
  imports: [FormsModule, RouterLink, DiceRollerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './master-sessione.component.html',
})
export class MasterSessioneComponent implements OnInit, OnDestroy {
  protected readonly table = inject(TableService);

  /** Come si legge un dado (i dadi Fudge di Fate come segno, non «dN:valore»). */
  protected readonly dieText = dieText;

  protected readonly state = this.table.state;
  protected readonly rollLog = this.table.rollLog;
  protected readonly characters = computed(() => this.state()?.characters ?? []);
  protected readonly profile = computed(() => this.state()?.profile ?? null);

  /** Il bestiario non è nello snapshot: lo si chiede a parte, con un poll leggero. */
  protected readonly monsters = signal<CharacterDto[]>([]);
  private poll?: ReturnType<typeof setInterval>;

  protected readonly showQr = signal(false);

  // Questi campi si azzerano dopo un'azione (dietro un await): signal, così con
  // OnPush il reset si vede. newHp e hpAmt li tocca solo chi digita e si leggono
  // al volo: campi semplici.
  /** Nuova scheda al tavolo. */
  protected readonly newName = signal('');
  protected newHp = 10;

  /** Bozze per l'aggiunta all'iniziativa (una per tipo). */
  protected readonly initPcId = signal('');
  protected readonly initPcNum = signal<number | null>(null);
  protected readonly initNpcId = signal('');
  protected readonly initNpcNum = signal<number | null>(null);

  /** Quantità di danno/cura per scheda (default 1); la chiave è assente finché non si tocca. */
  protected hpAmt: Record<string, number | undefined> = {};

  // ---- Iniziativa (derivata dallo stato) ----

  protected readonly order = computed<InitiativeEntryDto[]>(
    () => this.state()?.initiative?.order ?? [],
  );
  protected readonly activeId = computed(() => this.state()?.initiative?.activeId ?? null);
  protected readonly round = computed(() => this.state()?.initiative?.round ?? 0);

  // ---- Stati (avvelenato, prono…) su chi è nell'iniziativa ----
  private readonly allConditions = computed(() => this.state()?.conditions ?? {});
  protected condFor(refId: string) { return this.allConditions()[refId] ?? []; }

  /** Gli stati che questo gioco conosce: la scelta rapida del «+ stato». */
  protected readonly conditionPresets = computed(() => this.state()?.profile?.conditions ?? []);

  /** Quale voce sta mostrando il campo «+ stato» (una alla volta), e la bozza. */
  protected readonly addingCond = signal<string | null>(null);
  protected condLabel = '';
  protected condRounds: number | null = null;

  /** Aggiunge uno stato dal vocabolario del sistema, con i round eventualmente impostati. */
  async addPreset(refId: string, label: string): Promise<void> {
    await this.table.addCondition(refId, label, this.condRounds);
    this.condRounds = null;
    this.addingCond.set(null);
  }

  protected openCond(refId: string): void {
    this.condLabel = '';
    this.condRounds = null;
    this.addingCond.set(this.addingCond() === refId ? null : refId);
  }

  async saveCond(refId: string): Promise<void> {
    const label = this.condLabel.trim();
    if (!label) return;
    await this.table.addCondition(refId, label, this.condRounds);
    this.condLabel = '';
    this.condRounds = null;
    this.addingCond.set(null);
  }

  async removeCond(refId: string, id: string): Promise<void> {
    await this.table.removeCondition(refId, id);
  }

  private readonly inInitiative = computed(() => new Set(this.order().map(e => e.refId)));

  /** Chi è già nel giro sparisce dalle tendine: aggiungerlo due volte non vuol dire niente. */
  protected readonly freePcs = computed(() =>
    this.characters().filter(c => !this.inInitiative().has(c.id)));
  protected readonly freeMonsters = computed(() =>
    this.monsters().filter(m => !this.inInitiative().has(m.id)));

  async ngOnInit(): Promise<void> {
    this.monsters.set(await this.table.monsters());
    this.poll = setInterval(async () => this.monsters.set(await this.table.monsters()), 4000);
  }

  ngOnDestroy(): void {
    clearInterval(this.poll);
  }

  // ---- Tavolo ----

  amount(id: string): number {
    return Math.max(1, Math.trunc(this.hpAmt[id] || 1));
  }

  async changeHp(id: string, sign: number): Promise<void> {
    await this.table.adjustHp(id, sign * this.amount(id));
  }

  async addPlayer(): Promise<void> {
    const name = this.newName().trim();
    if (!name) return;
    await this.table.addCharacter(name, Math.max(1, Math.trunc(this.newHp) || 1));
    this.newName.set('');
  }

  /** Percentuale di PF per la barra. */
  hpPercent(c: CharacterDto): number {
    return c.hp.max ? Math.round((100 * c.hp.current) / c.hp.max) : 0;
  }

  poolChips(c: CharacterDto): { label: string; text: string; empty: boolean }[] {
    return Object.values(c.pools).flatMap(pool =>
      Object.entries(pool.tiers).map(([lv, s]) => ({
        label: pool.label,
        text: pool.kind === 'Leveled'
          ? `${pool.label} L${lv} ${s.remaining}/${s.max}`
          : `${pool.label} ${s.remaining}/${s.max}`,
        empty: s.remaining === 0,
      })),
    );
  }

  // ---- Iniziativa ----

  /** Le voci correnti nella forma che la rotta si aspetta. */
  private entries(): InitiativeEntryDto[] {
    return this.order().map(e => ({
      refId: e.refId, kind: e.kind, name: e.name, initiative: e.initiative,
    }));
  }

  async addInitiative(kind: 'pc' | 'monster'): Promise<void> {
    const id = kind === 'pc' ? this.initPcId() : this.initNpcId();
    if (!id) return;
    const source = kind === 'pc' ? this.characters() : this.monsters();
    const found = source.find(x => x.id === id);
    if (!found) return;

    // Campo vuoto = 0: al tavolo capita di aggiungere prima e tirare dopo.
    const num = kind === 'pc' ? this.initPcNum() : this.initNpcNum();
    const order = [...this.entries(), { refId: found.id, kind, name: found.name, initiative: Number(num) || 0 }];
    await this.table.setInitiative(order, this.activeId());

    if (kind === 'pc') { this.initPcId.set(''); this.initPcNum.set(null); }
    else { this.initNpcId.set(''); this.initNpcNum.set(null); }
  }

  async removeInitiative(refId: string): Promise<void> {
    // L'activeId lo ripulisce il server se non è più in lista: qui non si duplica la regola.
    await this.table.setInitiative(this.entries().filter(e => e.refId !== refId), this.activeId());
  }

  async nextTurn(): Promise<void> {
    await this.table.advanceTurn();
  }

  async clearInitiative(): Promise<void> {
    if (confirm("Svuotare l'iniziativa?")) {
      await this.table.setInitiative([], null);
    }
  }

  // ---- Tiri ----

  /**
   * Il tiro libero del Master, sotto il nome "Master". Arrow property così
   * `this` regge quando il widget lo chiama. Torna null se è andata, il
   * messaggio d'errore altrimenti (formula rotta) — il widget lo mostra.
   */
  protected readonly rollMaster = (formula: string, opts: { times?: number; keep?: 'Sum' | 'Highest' | 'Lowest' }) =>
    this.table.rollFreeMaster(formula, opts);

  /**
   * Ri-tira una riga del log: stessa formula, sotto lo stesso nome. Un tiro del
   * Master torna dal Master; il tiro di un giocatore si rilancia come suo (così
   * un @Forza si risolve ancora sulla sua scheda).
   */
  async reroll(roll: RollEntryDto): Promise<void> {
    const label = '↻ ' + roll.label;
    if (roll.characterId === 'master')
      await this.table.rollFreeMaster(roll.formula, { label });
    else
      await this.table.rollFree(roll.characterId, roll.formula, { label });
  }

  private static readonly OUTCOME_LABEL: Record<string, string> = {
    hope: 'Speranza', fear: 'Paura', crit: 'Critico',
  };

  outcomeLabel(o: string): string {
    return MasterSessioneComponent.OUTCOME_LABEL[o] ?? o;
  }

  kept(roll: RollEntryDto) {
    return roll.attempts[roll.keptIndex];
  }

  discarded(roll: RollEntryDto): string {
    if (roll.attempts.length <= 1) return '';
    return roll.attempts
      .filter((_, i) => i !== roll.keptIndex)
      .map(a => a.total)
      .join(', ');
  }
}
