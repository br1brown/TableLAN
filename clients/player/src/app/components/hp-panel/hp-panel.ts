import { Component, input, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CharacterDto } from '../../models';
import { TableService } from '../../table.service';

@Component({
  selector: 'app-hp-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <button class="hp-bar" (click)="hpPanel.set(!hpPanel())"
            [attr.aria-expanded]="hpPanel()" aria-label="Gestisci i punti ferita">
      <span class="hp-fill" [style.width.%]="c().hp.max ? (100 * c().hp.current / c().hp.max) : 0"></span>
      <span class="hp-text">PF {{ c().hp.current }}/{{ c().hp.max }}</span>
    </button>

    @if (hpPanel()) {
      <div class="hp-panel">
        <input type="number" min="1" inputmode="numeric" [(ngModel)]="hpAmount"
               placeholder="quanti" aria-label="Quantità di PF">
        <button class="hp-damage" [disabled]="!hpDelta()" (click)="applyHp(-1)">
          <i class="ra ra-health-decrease"></i> Danno
        </button>
        <button class="hp-heal" [disabled]="!hpDelta()" (click)="applyHp(1)">
          <i class="ra ra-health-increase"></i> Cura
        </button>
      </div>
    }
  `
})
export class HpPanelComponent {
  c = input.required<CharacterDto>();
  
  private table = inject(TableService);
  
  hpPanel = signal(false);
  hpAmount = signal('');
  
  hpDelta = computed(() => {
    return Math.max(0, Math.trunc(Number(this.hpAmount()) || 0));
  });

  applyHp(sign: 1 | -1): void {
    const amount = this.hpDelta();
    if (!amount) return;
    
    void this.table.adjustHp(this.c().id, sign * amount);
    this.hpAmount.set('');
    this.hpPanel.set(false);
  }
}
