import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CharacterDto, RollEntryDto } from '../../models';
import { TableService } from '../../table.service';

@Component({
  selector: 'app-tavolo-tab',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="group tavolo">
      <h3><i class="ra ra-player"></i> Compagni</h3>
      <div class="tavolo-players">
        @for (other of otherCharacters(); track other.id) {
          <div class="tavolo-player">
            <span class="tp-name">{{ other.name }}</span>
            <div class="tp-bar">
              <span class="tp-fill" [style.width.%]="other.hp.max ? (100 * other.hp.current / other.hp.max) : 0"></span>
            </div>
          </div>
        } @empty {
          <p class="muted">Nessun altro giocatore al tavolo.</p>
        }
      </div>
      
      <h3 style="margin-top: 1.5rem;"><i class="ra ra-perspective-dice-six"></i> Log Tiri</h3>
      <div class="tavolo-rolls">
        @for (roll of rollLog(); track roll.id) {
          <div class="tavolo-roll">
            <div class="tr-head">
              <span class="tr-who">{{ roll.characterName }}</span>
              <span class="tr-what">{{ roll.label }}</span>
            </div>
            <div class="tr-body">
              <span class="tr-total">{{ roll.total }}</span>
              <span class="tr-detail">{{ roll.formula }} &rarr; {{ diceOf(roll) }}</span>
            </div>
          </div>
        } @empty {
          <p class="muted">Nessun tiro recente.</p>
        }
      </div>
    </section>
  `
})
export class TavoloTabComponent {
  otherCharacters = input.required<CharacterDto[]>();
  rollLog = input.required<RollEntryDto[]>();

  diceOf(roll: RollEntryDto): string {
    const kept = roll.attempts[roll.keptIndex];
    if (!kept) return '';
    const dice = kept.dice.map(d => `d${d[0]}:${d[1]}`).join(' ');
    const mod = kept.modifier ? ` ${kept.modifier > 0 ? '+' : '−'} ${Math.abs(kept.modifier)}` : '';
    const discarded = roll.attempts.length > 1
      ? ` (scartato ${roll.attempts.filter((_, i) => i !== roll.keptIndex).map(a => a.total).join(', ')})`
      : '';
    return `${dice}${mod}${discarded}`;
  }
}
