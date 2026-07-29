import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CharacterDto, RollEntryDto, DieView, DiePresetDto, dieText } from '../../models';
import { TableService } from '../../table.service';
import { DiceRollerComponent } from '../dice-roller/dice-roller';

@Component({
  selector: 'app-tavolo-tab',
  standalone: true,
  imports: [CommonModule, DiceRollerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <h3 class="h5"><i class="ra ra-perspective-dice-six"></i> Tira un dado</h3>
      <!-- Il vassoio del sistema, più la formula libera per i tiri contestuali:
           il giocatore tira sotto il proprio nome, e va al log del tavolo. -->
      <div class="mb-4">
        <app-dice-roller [roller]="rollFree" [dice]="dice()"></app-dice-roller>
      </div>

      <h3 class="h5"><i class="ra ra-player"></i> Compagni</h3>
      <div class="d-flex flex-column gap-2 mb-4">
        @for (other of otherCharacters(); track other.id) {
          <div>
            <div class="small mb-1">{{ other.name }}</div>
            <div class="progress" role="progressbar">
              <div class="progress-bar bg-danger"
                   [style.width.%]="other.hp.max ? (100 * other.hp.current / other.hp.max) : 0"></div>
            </div>
          </div>
        } @empty {
          <p class="text-secondary">Nessun altro giocatore al tavolo.</p>
        }
      </div>

      <h3 class="h5"><i class="ra ra-perspective-dice-six"></i> Log Tiri</h3>
      <div class="d-flex flex-column">
        @for (roll of rollLog(); track roll.id) {
          <div class="border-top py-2">
            <div class="d-flex justify-content-between align-items-start gap-2">
              <div>
                <div class="fw-semibold">{{ roll.characterName }}</div>
                <div class="small text-secondary">
                  @if (roll.outcome) {
                    <span class="badge rounded-pill me-1"
                          [class.text-bg-warning]="roll.outcome === 'hope' || roll.outcome === 'crit'"
                          [class.text-bg-danger]="roll.outcome === 'fear'">{{ outcomeLabel(roll.outcome) }}</span>
                  }
                  {{ roll.label }}
                </div>
              </div>
              <span class="fs-4 fw-bold text-warning font-monospace">{{ roll.total }}</span>
            </div>
            <div class="small text-secondary font-monospace mt-1">{{ roll.formula }} &rarr;
              @for (d of keptDice(roll); track $index) {
                <span [class.text-warning]="d.role === 'hope'" [class.text-danger]="d.role === 'fear'"
                      [class.text-info]="d.role === 'fudge'"
                      [class.text-decoration-line-through]="d.role === 'dropped'" [class.opacity-50]="d.role === 'dropped'">{{ dieText(d) }} </span>
              }{{ tailOf(roll) }}</div>
          </div>
        } @empty {
          <p class="text-secondary">Nessun tiro recente.</p>
        }
      </div>
    </section>
  `
})
export class TavoloTabComponent {
  private readonly table = inject(TableService);

  otherCharacters = input.required<CharacterDto[]>();
  rollLog = input.required<RollEntryDto[]>();
  /** Chi sta tirando: la sua scheda. Il tiro libero va sotto questo nome. */
  characterId = input.required<string>();
  /** Il vassoio dei dadi del sistema; vuoto ⇒ il widget mostra il set standard. */
  dice = input<DiePresetDto[] | null | undefined>(undefined);

  /**
   * Il tiro libero del giocatore. Arrow property così `this` regge quando il
   * widget lo chiama; torna null se è andata, il messaggio d'errore altrimenti.
   */
  readonly rollFree = (formula: string, opts: { times?: number; keep?: 'Sum' | 'Highest' | 'Lowest' }) =>
    this.table.rollFree(this.characterId(), formula, opts);

  /** I dadi del tentativo che conta, col loro ruolo — il template li colora. */
  keptDice(roll: RollEntryDto): DieView[] {
    return roll.attempts[roll.keptIndex]?.dice ?? [];
  }

  /** Come si legge un dado (i Fudge di Fate come segno, non «dN:valore»). */
  protected readonly dieText = dieText;

  /** Ciò che segue i dadi: modificatore e tentativi scartati. */
  tailOf(roll: RollEntryDto): string {
    const kept = roll.attempts[roll.keptIndex];
    if (!kept) return '';
    const mod = kept.modifier ? ` ${kept.modifier > 0 ? '+' : '−'} ${Math.abs(kept.modifier)}` : '';
    const discarded = roll.attempts.length > 1
      ? ` (scartato ${roll.attempts.filter((_, i) => i !== roll.keptIndex).map(a => a.total).join(', ')})`
      : '';
    return `${mod}${discarded}`;
  }

  /** La chiave grezza dell'esito diventa parola italiana. */
  outcomeLabel(outcome: string): string {
    return ({ hope: 'Speranza', fear: 'Paura', crit: 'Critico' } as Record<string, string>)[outcome] ?? outcome;
  }
}
