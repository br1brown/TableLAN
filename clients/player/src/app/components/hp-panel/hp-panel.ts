import { ChangeDetectionStrategy, Component, input, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CharacterDto } from '../../models';
import { TableService } from '../../table.service';

@Component({
  selector: 'app-hp-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="button" (click)="hpPanel.set(!hpPanel())"
         [attr.aria-expanded]="hpPanel()" aria-label="Gestisci i punti ferita">
      <div class="d-flex justify-content-between small mb-1">
        <span class="text-secondary">Punti Ferita</span>
        <span class="font-monospace fw-semibold">PF {{ c().hp.current }}/{{ c().hp.max }}@if (temp() > 0) {
          <span class="text-info ms-1">+{{ temp() }} temp</span>
        }</span>
      </div>
      <div class="progress" role="progressbar">
        <div class="progress-bar bg-danger"
             [style.width.%]="c().hp.max ? (100 * c().hp.current / c().hp.max) : 0"></div>
        @if (temp() > 0) {
          <div class="progress-bar bg-info" [style.width.%]="c().hp.max ? (100 * temp() / c().hp.max) : 0"></div>
        }
      </div>
    </div>

    @if (hpPanel()) {
      <div class="input-group my-2">
        <input class="form-control" type="number" min="1" inputmode="numeric" [(ngModel)]="hpAmount"
               placeholder="quanti" aria-label="Quantità di PF">
        <button class="btn btn-outline-danger" [disabled]="!hpDelta()" (click)="applyHp(-1)">
          <i class="ra ra-health-decrease"></i> Danno
        </button>
        <button class="btn btn-outline-success" [disabled]="!hpDelta()" (click)="applyHp(1)">
          <i class="ra ra-health-increase"></i> Cura
        </button>
      </div>
      <!-- PF temporanei: un cuscinetto che il danno mangia per primo. -->
      <div class="input-group input-group-sm mb-2">
        <span class="input-group-text">PF temp.</span>
        <input class="form-control" type="number" min="0" inputmode="numeric" [(ngModel)]="tempAmount"
               placeholder="es. 5" aria-label="PF temporanei">
        <button class="btn btn-outline-info" (click)="applyTemp()">Imposta</button>
      </div>
    }
  `
})
export class HpPanelComponent {
  c = input.required<CharacterDto>();
  
  private table = inject(TableService);
  
  hpPanel = signal(false);
  hpAmount = signal('');
  tempAmount = signal('');

  protected readonly temp = computed(() => this.c().hp.temp ?? 0);

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

  applyTemp(): void {
    const value = Math.max(0, Math.trunc(Number(this.tempAmount()) || 0));
    void this.table.setTempHp(this.c().id, value);
    this.tempAmount.set('');
    this.hpPanel.set(false);
  }
}
