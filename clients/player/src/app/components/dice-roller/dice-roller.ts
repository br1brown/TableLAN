import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DiePresetDto } from '../../models';

/**
 * Il vassoio dei dadi: i tiri rapidi «sul tavolo».
 *
 * Nasce da un buco trovato al primo tavolo vero — il Master non poteva tirare,
 * e non ogni tiro contestuale sta su una scheda. Il grosso sono bottoni pronti:
 * quali dadi ci siano lo decide il **sistema** (li configura il Master nella
 * Sistema), quindi sono uguali per tutti e cambiano col gioco, non con la
 * serata. Sotto resta un campo libero per la formula che nessun bottone copre.
 *
 * Il widget è lo stesso per Master e giocatore: cambia solo il `roller`, cioè
 * *chi* tira e sotto che nome. L'esito atterra nel log del tavolo come ogni
 * altro tiro; qui resta solo l'eventuale errore di formula.
 */
@Component({
  selector: 'app-dice-roller',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="d-flex flex-column gap-2">
      <!-- Vantaggio/svantaggio: si sceglie prima, e vale per qualunque dado si
           tocchi. Ri-cliccando si torna al tiro secco. -->
      <div class="btn-group btn-group-sm w-100" role="group" aria-label="Vantaggio o svantaggio">
        <button type="button" class="btn"
                [class.btn-secondary]="keep()==='Highest'" [class.btn-outline-secondary]="keep()!=='Highest'"
                (click)="toggleKeep('Highest')">Vantaggio</button>
        <button type="button" class="btn"
                [class.btn-warning]="keep()==='Sum'" [class.btn-outline-secondary]="keep()!=='Sum'"
                (click)="keep.set('Sum')">Secco</button>
        <button type="button" class="btn"
                [class.btn-secondary]="keep()==='Lowest'" [class.btn-outline-secondary]="keep()!=='Lowest'"
                (click)="toggleKeep('Lowest')">Svantaggio</button>
      </div>

      <!-- Il vassoio: un bottone per dado. -->
      <div class="d-flex flex-wrap gap-2">
        @for (d of tray(); track $index) {
          <button class="btn btn-outline-warning font-monospace" [disabled]="busy()"
                  (click)="rollPreset(d.formula)" [title]="'Tira ' + d.formula">
            <i class="ra ra-perspective-dice-six"></i> {{ d.label || d.formula }}
          </button>
        }
      </div>

      <!-- Formula libera: per il tiro che nessun bottone del vassoio prevede. -->
      <div class="input-group input-group-sm">
        <input class="form-control font-monospace" [(ngModel)]="formula"
               (keyup.enter)="fire(formula)" placeholder="altra formula: es. 2d6+3"
               aria-label="Formula libera del dado">
        <button type="button" class="btn btn-outline-secondary" (click)="showHelp.set(!showHelp())"
                [class.active]="showHelp()" [attr.aria-expanded]="showHelp()" title="Cosa posso scrivere?">?</button>
        <button class="btn btn-outline-secondary" [disabled]="busy() || !formula.trim()" (click)="fire(formula)">
          Tira
        </button>
      </div>

      <!-- Il motore capisce più di quanto un bottone lasci vedere: esplosivi,
           «tieni i migliori», pool a successi, Duality. Toccando un esempio lo si
           copia nel campo, pronto da tirare o ritoccare. -->
      @if (showHelp()) {
        <div class="small text-secondary border rounded p-2">
          <div class="mb-1">Tocca un esempio per provarlo:</div>
          <div class="d-flex flex-wrap gap-1">
            @for (ex of examples; track ex.f) {
              <button type="button" class="btn btn-sm btn-outline-secondary border-0 py-0 px-2 text-start"
                      (click)="formula = ex.f; showHelp.set(false)" [title]="ex.note">
                <span class="font-monospace text-body">{{ ex.f }}</span>
                <span class="ms-1 opacity-75">{{ ex.note }}</span>
              </button>
            }
          </div>
        </div>
      }

      @if (error()) { <div class="small text-danger">{{ error() }}</div> }
    </div>
  `
})
export class DiceRollerComponent {
  /** Chi esegue il tiro: torna null se è andata, il messaggio d'errore altrimenti. */
  roller = input.required<(formula: string, opts: { times?: number; keep?: 'Sum' | 'Highest' | 'Lowest' }) => Promise<string | null>>();

  /** Il vassoio dal profilo di sistema. Vuoto/assente ⇒ set poliedrico standard. */
  dice = input<DiePresetDto[] | null | undefined>(undefined);

  /** Il set di default quando il sistema non ne configura nessuno. */
  private static readonly DEFAULT: DiePresetDto[] =
    [4, 6, 8, 10, 12, 20, 100].map(n => ({ label: `d${n}`, formula: `1d${n}` }));

  readonly tray = computed<DiePresetDto[]>(() => {
    const configured = (this.dice() ?? []).filter(d => d?.formula?.trim());
    return configured.length ? configured : DiceRollerComponent.DEFAULT;
  });

  formula = '';
  readonly keep = signal<'Sum' | 'Highest' | 'Lowest'>('Sum');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly showHelp = signal(false);

  /**
   * La grammatica che il campo libero accetta, in esempi tangibili. È l'unico
   * posto dove esplosivi, «tieni i migliori», pool a successi e Duality si fanno
   * vedere: senza, resterebbero poteri che solo chi ha letto il codice conosce.
   */
  readonly examples: { f: string; note: string }[] = [
    { f: '2d6+3', note: 'somma con modificatore' },
    { f: '1d20+@Forza', note: 'con una tua statistica' },
    { f: '4d6kh3', note: 'tieni i 3 più alti' },
    { f: '1d8!', note: 'esplode sul massimo (Ace)' },
    { f: '@Destrezza d10 >= 6', note: 'pool a successi' },
    { f: 'duality: 2d12', note: 'Speranza / Paura' },
  ];

  toggleKeep(mode: 'Highest' | 'Lowest') {
    this.keep.update(k => (k === mode ? 'Sum' : mode));
  }

  /** Un dado del vassoio: tira senza toccare il campo libero. */
  rollPreset(formula: string) {
    this.fire(formula);
  }

  async fire(formula: string) {
    const f = formula.trim();
    if (!f || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    // Vantaggio/svantaggio = due tentativi; tiro secco = uno solo.
    const keep = this.keep();
    const times = keep === 'Sum' ? 1 : 2;

    this.error.set(await this.roller()(f, { times, keep }));
    this.busy.set(false);
  }
}
