import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CampaignInfo } from '../../../models';
import { TableService } from '../../../table.service';

interface Modifier { base: number; div: number; }
interface StatDef { id: string; label: string; default: number; roll: string; modifier: Modifier | null; }
interface PoolDef { id: string; label: string; kind: 'Leveled' | 'Points'; rechargeCycle: string; }
interface TurnRes { id: string; label: string; perTurn: number; }
interface CycleDef { id: string; label: string; rank: number; perTurn: boolean; }
interface DieDef { label: string; formula: string; }

interface SystemProfile {
  id: string;
  name: string;
  turnResources: TurnRes[];
  pools: PoolDef[];
  cycles: CycleDef[];
  stats: StatDef[];
  dice: DieDef[];
  conditions: string[];
}

/**
 * Editor del profilo di sistema (port di wwwroot/admin/sistema.html): turno,
 * riserve, cicli e statistiche. Il default è D&D 5e; da qui si adatta ad altri
 * sistemi senza programmare. Cambia le regole per tutti i giocatori insieme:
 * prima di salvare, il conteggio delle feature che perderebbero la meccanica
 * è concreto, non un paragrafo di avviso.
 *
 * Il JSON avanzato è un dettaglio interno, dietro un toggle: il Master lavora
 * coi nomi, i codici interni non li vede mai.
 */
@Component({
  selector: 'app-master-sistema',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './master-sistema.component.html',
})
export class MasterSistemaComponent implements OnInit {
  private readonly table = inject(TableService);
  private readonly cdr = inject(ChangeDetectorRef);

  // `form` è il modello di una form template-driven: ngModel lo modifica in
  // loco, campo per campo, per tutta la scheda. Tenerlo un signal non darebbe
  // vantaggi (si muterebbe comunque in loco) e complicherebbe ogni binding —
  // resta un oggetto normale. Quando invece lo si SOSTITUISCE dietro un await
  // (caricamento, preset, salvataggio) serve un markForCheck, perché con OnPush
  // una mutazione fuori da un evento non ridisegna da sola.
  protected form: SystemProfile = this.empty();

  // Il resto è stato reattivo vero e proprio (messaggi, liste, campi che si
  // azzerano dopo un'azione): signal, così OnPush li segue senza trucchi.
  protected readonly presets = signal<SystemProfile[]>([]);
  protected presetIndex = 0;
  protected readonly msg = signal('');
  protected showJson = false;

  // ---- Campagne (file .db) ----
  // Stanno qui, fra le impostazioni, e non in cima alla Sessione: cambiare
  // campagna non è un gesto di gioco, e ogni campagna ha il suo profilo di
  // sistema (quello editato qui sotto), che infatti si ricarica allo switch.
  protected readonly campaigns = signal<CampaignInfo[]>([]);
  protected readonly currentCampaign = signal('');
  protected readonly switchTo = signal('');
  protected readonly newCampaignName = signal('');
  protected readonly campaignMsg = signal('');
  protected readonly otherCampaigns = computed(() => this.campaigns().filter(c => !c.current));

  /** Lo stato è vivo: il conteggio orfani si fa sulle schede attuali. */
  private readonly state = this.table.state;

  /** Il JSON del profilo, ricalcolato a ogni CD (dietro il toggle "avanzato"). */
  protected profileJson(): string { return JSON.stringify(this.form, null, 2); }

  private empty(): SystemProfile {
    return { id: '', name: '', turnResources: [], pools: [], cycles: [], stats: [], dice: [], conditions: [] };
  }

  async ngOnInit(): Promise<void> {
    this.presets.set(await this.table.profilePresets());
    await this.loadProfile();
    await this.loadCampaigns();
  }

  private async loadProfile(): Promise<void> {
    const active = await this.table.profile();
    if (active) { this.form = active; this.ensureArrays(); this.cdr.markForCheck(); }
  }

  // ---- Campagne ----

  private async loadCampaigns(): Promise<void> {
    const { current, campaigns } = await this.table.campaigns();
    this.currentCampaign.set(current);
    this.campaigns.set(campaigns);
  }

  /** Dopo un cambio/creazione: la lista e il profilo della campagna nuova. */
  private async afterCampaignChange(): Promise<void> {
    await this.loadCampaigns();
    await this.loadProfile();
  }

  async switchCampaign(): Promise<void> {
    const name = this.switchTo();
    if (!name) return;
    if (!confirm(`Cambiare campagna a «${name}»? La sessione corrente si chiude e si carica quella scelta.`)) return;
    const { ok, data } = await this.table.switchCampaign(name);
    if (!ok) { this.campaignMsg.set(data.error ?? 'Cambio non riuscito.'); return; }
    this.campaignMsg.set('');
    this.switchTo.set('');
    await this.afterCampaignChange();
  }

  async createCampaign(): Promise<void> {
    const name = this.newCampaignName().trim();
    if (!name) return;
    const { ok, data } = await this.table.newCampaign(name);
    if (!ok) { this.campaignMsg.set(data.error ?? 'Creazione non riuscita.'); return; }
    this.campaignMsg.set('');
    this.newCampaignName.set('');
    await this.afterCampaignChange();
  }

  private ensureArrays(): void {
    this.form.turnResources ??= [];
    this.form.pools ??= [];
    this.form.cycles ??= [];
    this.form.stats ??= [];
    this.form.dice ??= [];
    this.form.conditions ??= [];
  }

  // Codice interno stabile, assegnato alla creazione: la chiave con cui le
  // feature agganciano la meccanica. Resta fisso anche rinominando l'etichetta.
  private newId(prefix: string): string {
    return prefix + '_' + Math.random().toString(36).slice(2, 7);
  }

  private slug(s: string): string {
    return String(s || '').trim().replace(/\s+/g, '').replace(/[^A-Za-z0-9]/g, '')
      || ('x' + Math.random().toString(36).slice(2, 6));
  }

  // ---- Aggiunta / rimozione righe ----

  addTurn(): void { this.form.turnResources.push({ id: this.newId('t'), label: '', perTurn: 1 }); }
  addPool(): void { this.form.pools.push({ id: this.newId('p'), label: '', kind: 'Points', rechargeCycle: '' }); }
  addStat(): void { this.form.stats.push({ id: this.newId('s'), label: '', default: 10, roll: '', modifier: null }); }
  addDie(): void { this.form.dice.push({ label: '', formula: '' }); }
  addConditionRow(): void { this.form.conditions.push(''); }
  delCondition(i: number): void { this.form.conditions.splice(i, 1); }
  addCycle(): void {
    this.form.cycles.push({ id: this.newId('c'), label: '', rank: this.form.cycles.length + 1, perTurn: false });
  }

  del<K extends keyof SystemProfile>(section: K, i: number): void {
    (this.form[section] as unknown[]).splice(i, 1);
  }

  /**
   * Il modificatore di una statistica: acceso, `@Nome` nelle formule vale il
   * modificatore; spento, vale il punteggio intero (la CA, l'Umanità in
   * Vampiri). Il default è spento — è D&D a essere strano.
   */
  toggleModifier(s: StatDef, on: boolean): void {
    s.modifier = on ? { base: 10, div: 2 } : null;
  }

  /** La regola in atto su un valore vero: "18 → +4" si legge meglio di una formula. */
  esempioMod(s: StatDef): string {
    const base = Number(s.modifier?.base ?? 10);
    const div = Number(s.modifier?.div) || 1;
    const v = Number(s.default) || 0;
    const m = Math.floor((v - base) / div);
    return `${v} → ${m >= 0 ? '+' : '−'}${Math.abs(m)}`;
  }

  // ---- Preset e import ----

  loadPreset(): void {
    const p = this.presets()[this.presetIndex];
    if (!p) return;
    this.form = JSON.parse(JSON.stringify(p));
    this.ensureArrays();
    this.msg.set('Preset caricato — ricorda di salvare.');
  }

  async importFile(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    try {
      const parsed = JSON.parse(await file.text());
      if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed))
        throw new Error('il file non contiene un profilo.');
      for (const k of ['turnResources', 'pools', 'cycles', 'stats', 'dice', 'conditions'])
        if (k in parsed && !Array.isArray(parsed[k]))
          throw new Error(`"${k}" dev'essere una lista.`);
      this.form = parsed;
      this.ensureArrays();
      this.msg.set(`Importato «${file.name}» — controlla e poi salva.`);
    } catch (e: any) {
      this.msg.set('File non valido: ' + (e?.message || 'JSON illeggibile.'));
    } finally {
      this.cdr.markForCheck();
    }
  }

  // ---- Salvataggio ----

  private normalize(): void {
    this.form.name = (this.form.name || '').trim() || 'Profilo';
    this.form.id = this.form.id || this.slug(this.form.name);

    for (const t of this.form.turnResources) {
      t.id = t.id || this.slug(t.label);
      t.label = t.label || t.id;
      // Lo zero è un valore, non un campo vuoto: "|| 1" lo trasformerebbe in 1.
      const n = Number(t.perTurn);
      t.perTurn = Number.isFinite(n) && n >= 0 ? n : 1;
    }
    for (const p of this.form.pools) {
      p.id = p.id || this.slug(p.label);
      p.label = p.label || p.id;
      p.kind = p.kind || 'Points';
    }
    for (const c of this.form.cycles) {
      c.id = c.id || this.slug(c.label);
      c.label = c.label || c.id;
      c.rank = Number(c.rank) || 1;
      c.perTurn = !!c.perTurn;
    }
    for (const s of this.form.stats) {
      s.id = s.id || this.slug(s.label);
      s.label = s.label || s.id;
      s.default = Number(s.default) || 0;
      // Vuoto = non si tira: solo null lo dice al motore.
      s.roll = ((s.roll || '').trim() || null) as any;
      if (s.modifier) {
        s.modifier.base = Number(s.modifier.base) || 0;
        s.modifier.div = Number(s.modifier.div) || 1;
      } else {
        s.modifier = null;
      }
    }
    this.form.stats = this.form.stats.filter(s => s.label.trim() !== '');

    // Un dado del vassoio è la sua formula: senza, la riga non tira niente e si
    // butta. L'etichetta è cosmetica — vuota, sul bottone compare la formula.
    for (const d of this.form.dice) {
      d.formula = (d.formula || '').trim();
      d.label = (d.label || '').trim();
    }
    this.form.dice = this.form.dice.filter(d => d.formula !== '');

    // Gli stati sono etichette: si ripuliscono e si buttano i vuoti.
    this.form.conditions = this.form.conditions.map(c => (c || '').trim()).filter(c => c !== '');
  }

  /** Un costo è orfano se il profilo del form non sa più cosa sia. */
  private isOrphanCost(cost: { kind: string }): boolean {
    if (!cost.kind || cost.kind === 'None') return false;
    return !this.form.turnResources.some(t => t.id === cost.kind)
      && !this.form.pools.some(p => p.id === cost.kind);
  }

  /** Quante feature (e su quanti personaggi) perderebbero la meccanica col form corrente. */
  private orphanCount(): { features: number; characters: number } {
    const s = this.state();
    if (!s) return { features: 0, characters: 0 };
    let features = 0;
    let characters = 0;
    for (const c of s.characters) {
      const n = c.features.filter(f => (f.costs ?? []).some(cost => this.isOrphanCost(cost))).length;
      if (n > 0) { features += n; characters++; }
    }
    return { features, characters };
  }

  save(): void {
    this.normalize();
    const { features, characters } = this.orphanCount();
    if (features === 0) { this.doSave(); return; }
    const pg = characters === 1 ? 'personaggio' : 'personaggi';
    const question =
      `Con questo profilo, ${features} feature su ${characters} ${pg} ` +
      `${features === 1 ? 'perderà' : 'perderanno'} la loro meccanica: ` +
      `${features === 1 ? 'resterà' : 'resteranno'} sulla scheda ma non ` +
      `${features === 1 ? 'sarà attivabile' : 'saranno attivabili'} finché non ` +
      `${features === 1 ? 'la rimappi' : 'le rimappi'} da Schede. Salvare?`;
    if (confirm(question)) this.doSave();
  }

  private async doSave(): Promise<void> {
    const { ok, data } = await this.table.saveProfile(this.form);
    if (ok) {
      this.form = data;
      this.ensureArrays();
      this.msg.set('Profilo salvato: le schede si aggiornano dal vivo.');
    } else {
      this.msg.set('Errore: ' + (data.error || 'salvataggio non riuscito.'));
    }
    this.cdr.markForCheck();
  }
}
