import { Component, computed, input, output, signal, effect } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CostDto, EffectDto, FeatureDraft, FeatureDto, ProfileDto } from '../../models';
import { costLabel } from '../../cost-label';

/**
 * L'editor di una feature: tutto ciò che è suo, in un posto solo.
 *
 * Componente muto — riceve con `input()`, chiede con `output()`, non conosce né
 * il servizio né la rotta. Chi lo usa decide cosa farne.
 *
 * **Le opzioni le dichiara il profilo, non il client.** I costi possibili sono
 * le risorse di turno e le riserve del sistema attivo; le ricariche sono i suoi
 * cicli. Cambiando profilo cambiano le tendine, senza toccare questo file: è lo
 * stesso principio per cui la scheda raggruppa per costo senza sapere cos'è
 * un'Azione.
 *
 * Il costo mostra l'**etichetta**, mai il codice interno (`BonusAction`): il
 * codice è la chiave con cui le feature si agganciano al profilo, ed è
 * dichiarato "mai mostrato". Il valore del `<select>` è l'id, ciò che si legge
 * è il nome.
 */
@Component({
  selector: 'app-feature-editor',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './feature-editor.html',
  styleUrl: './feature-editor.scss',
})
export class FeatureEditorComponent {
  readonly feature = input.required<FeatureDto>();
  readonly profile = input.required<ProfileDto>();
  /** Il testo, già scaricato da chi ci sta sopra: qui non si va in rete. */
  readonly description = input<string>('');
  /** Le statistiche che un effetto può bersagliare, dalla scheda. */
  readonly effectTargets = input<string[]>([]);

  readonly save = output<FeatureDraft>();
  readonly remove = output<void>();
  /** Chiede il testo: arriva lazy, e serve solo se apri l'editor. */
  readonly needsDescription = output<string>();

  /** Bozza locale: si tocca qui, si salva quando lo dici tu. */
  protected readonly draft = signal<FeatureDraft>(EMPTY);

  /** Aperto = stai modificando. Chiuso, la riga è solo un nome. */
  protected readonly open = signal(false);

  /** L'ultima feature per cui la bozza è stata riempita. */
  private syncedFor: string | null = null;

  constructor() {
    // La bozza si riempie una volta per feature, non a ogni ridiffusione dello
    // stato: il tavolo ribatte di continuo, e riscrivere i campi mentre li
    // stai battendo te li cancellerebbe sotto le dita.
    effect(() => {
      const f = this.feature();
      if (this.syncedFor === f.id) return;
      this.syncedFor = f.id;
      this.draft.set(fromFeature(f));
    });

    // Il testo arriva dopo, ed è lazy: quando c'è, entra nella bozza solo se
    // non l'hai già toccato tu.
    effect(() => {
      const testo = this.description();
      if (testo && this.draft().description === null) {
        this.draft.update(d => ({ ...d, description: testo }));
      }
    });
  }

  /** Le tendine dei costi: risorse di turno e riserve, dal profilo. */
  protected readonly costOptions = computed(() => {
    const p = this.profile();
    return [
      ...p.turnResources.map(t => ({ id: t.id, label: t.label })),
      ...p.pools.map(x => ({ id: x.id, label: x.label })),
    ];
  });

  /** Le ricariche possibili: i cicli del sistema attivo. */
  protected readonly cycles = computed(() => this.profile().cycles);

  /**
   * Quello che costa, in chiaro, per l'intestazione chiusa. Stessa traduzione
   * che legge il giocatore sulla scheda: una sola, o le due pagine divergono —
   * è già successo, ed è così che è nato "Costo: BonusAction".
   */
  protected readonly costsLabel = computed(() => costLabel(this.profile(), this.draft().costs));

  protected toggleOpen(): void {
    const apertoOra = !this.open();
    this.open.set(apertoOra);
    if (apertoOra && !this.description()) this.needsDescription.emit(this.feature().descriptionId);
  }

  protected set<K extends keyof FeatureDraft>(key: K, value: FeatureDraft[K]): void {
    this.draft.update(d => ({ ...d, [key]: value }));
  }

  protected setUses(raw: string): void {
    const n = raw.trim() === '' ? null : Number(raw);
    this.draft.update(d => ({
      ...d,
      maxUses: n,
      // Usi senza ricarica sarebbero cariche che non tornano mai: se ne metti,
      // il ciclo deve esistere. Se li togli, la ricarica non vuol più dire niente.
      recharge: n === null ? null : (d.recharge || this.cycles()[0]?.id || null),
    }));
  }

  protected addCost(): void {
    const primo = this.costOptions()[0];
    if (!primo) return;
    this.draft.update(d => ({ ...d, costs: [...d.costs, { kind: primo.id, amount: 1 }] }));
  }

  protected setCost(i: number, patch: Partial<CostDto>): void {
    this.draft.update(d => ({
      ...d,
      costs: d.costs.map((c, j) => (j === i ? { ...c, ...patch } : c)),
    }));
  }

  protected removeCost(i: number): void {
    this.draft.update(d => ({ ...d, costs: d.costs.filter((_, j) => j !== i) }));
  }

  protected addEffect(): void {
    const primo = this.effectTargets()[0] ?? '';
    this.draft.update(d => ({ ...d, effects: [...d.effects, { target: primo, op: 'Add', value: 1 }] }));
  }

  protected setEffect(i: number, patch: Partial<EffectDto>): void {
    this.draft.update(d => ({
      ...d,
      effects: d.effects.map((e, j) => (j === i ? { ...e, ...patch } : e)),
    }));
  }

  protected removeEffect(i: number): void {
    this.draft.update(d => ({ ...d, effects: d.effects.filter((_, j) => j !== i) }));
  }

  protected submit(): void {
    const d = this.draft();
    if (!d.shortName.trim()) return;
    this.save.emit({ ...d, shortName: d.shortName.trim(), roll: d.roll?.trim() || null });
  }
}

const EMPTY: FeatureDraft = {
  shortName: '',
  costs: [],
  maxUses: null,
  recharge: null,
  roll: null,
  description: null,
  toggleable: false,
  effects: [],
};

/** La feature com'è, nella forma modificabile. */
function fromFeature(f: FeatureDto): FeatureDraft {
  return {
    shortName: f.shortName,
    costs: f.costs.map(c => ({ ...c })),
    maxUses: f.usage?.max ?? null,
    recharge: f.usage?.recharge ?? null,
    roll: f.roll,
    // Null = non ancora arrivata. Diverso da "": quello è un testo cancellato.
    description: null,
    toggleable: f.toggleable,
    effects: f.effects.map(e => ({ ...e })),
  };
}
