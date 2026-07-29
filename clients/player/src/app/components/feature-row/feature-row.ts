import { ChangeDetectionStrategy, Component, input, inject, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FeatureDto } from '../../models';

@Component({
  selector: 'app-feature-row',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="border-bottom py-2"
             [class.opacity-50]="(!canUse() && !feature().toggleable) || (feature().toggleable && !feature().active)">

      @if (editMode()) {
        <!-- Modalità Modifica -->
        <div class="p-2 bg-body-tertiary rounded mb-2">
          <input class="form-control form-control-sm mb-2" [(ngModel)]="draftName" placeholder="Nome">
          <div class="d-flex gap-2">
            <button class="btn btn-sm btn-outline-secondary" (click)="saveEdit()">Salva</button>
            <button class="btn btn-sm btn-outline-danger" (click)="deleteFeat()">Elimina</button>
          </div>
        </div>
      } @else {
        <!-- Modalità Normale -->
        <div class="d-flex align-items-center gap-2">
          <button class="btn btn-link text-start text-decoration-none text-body flex-grow-1 p-0" (click)="toggle.emit()">
            <div class="fw-medium">
              {{ feature().shortName }}
              <small class="text-secondary">{{ sourceLabel() }}</small>
            </div>
            <div class="small text-secondary d-flex flex-wrap gap-2">
              @if (feature().costs.length) {
                <span><i class="ra" [class]="iconForFeature()"></i> {{ costLabel() }}</span>
              }
              <!-- Cosa ti rende: l'azione di Attacco vale due colpi, Action
                   Surge ti ridà l'Azione. Senza, la riga mentirebbe per omissione. -->
              @if (grantLabel()) {
                <span class="text-success">{{ grantLabel() }}</span>
              }
              @if (feature().usage) {
                <span class="font-monospace">{{ feature().usage?.remaining }}/{{ feature().usage?.max }}</span>
              }
            </div>
          </button>

          @if (feature().toggleable) {
            <button class="btn btn-sm flex-shrink-0"
                    [class.btn-success]="feature().active" [class.btn-outline-secondary]="!feature().active"
                    (click)="toggleEffect.emit()">
              {{ feature().active ? 'Indossato' : 'Rimosso' }}
            </button>
          } @else if (feature().roll) {
            <!-- Sul bottone il conto già fatto ("1d20+6"); la formula nel title. -->
            <button class="btn btn-sm btn-outline-warning font-monospace flex-shrink-0"
                    [disabled]="!canUse()" (click)="roll.emit()" [title]="'Tira ' + feature().roll">
              <i class="ra ra-perspective-dice-six"></i> {{ feature().rollLabel ?? feature().roll }}
            </button>
          } @else if (feature().costs.length || feature().usage) {
            <button class="btn btn-sm btn-primary flex-shrink-0" [disabled]="!canUse()" (click)="use.emit()">Usa</button>
          }
        </div>

        @if (feature().effects.length) {
          <div class="d-flex flex-wrap gap-1 mt-1">
            @for (e of feature().effects; track $index) {
              <span class="badge text-bg-dark border fw-normal">{{ effectLabel()(e) }}</span>
            }
          </div>
        }

        @if (!feature().toggleable && feature().blockedReason) {
          <p class="small text-danger mb-0 mt-1">{{ feature().blockedReason }}</p>
        }

        <!-- Non è un blocco: usarla si può, ma qualcosa cade. Detto prima del tap. -->
        @if (feature().wouldReplace; as caduta) {
          <p class="small text-warning mb-0 mt-1">Chiuderebbe: {{ caduta }}</p>
        }

        @if (expanded()) {
          <p class="small text-secondary mt-1 mb-0">{{ description() || 'Caricamento…' }}</p>
        }
      }
    </article>
  `
})
export class FeatureRowComponent {
  feature = input.required<FeatureDto>();
  canUse = input.required<boolean>();
  sourceLabel = input.required<string>();
  costLabel = input.required<string>();
  grantLabel = input<string>('');
  iconForFeature = input.required<string>();
  effectLabel = input.required<(e: any) => string>();
  expanded = input.required<boolean>();
  description = input.required<string>();
  editMode = input.required<boolean>();
  
  toggle = output<void>();
  toggleEffect = output<void>();
  roll = output<void>();
  use = output<void>();
  
  // Modifiche
  save = output<{ name: string }>();
  delete = output<void>();
  
  draftName = '';

  ngOnInit() {
    this.draftName = this.feature().shortName;
  }
  
  saveEdit() {
    if (this.draftName.trim() && this.draftName !== this.feature().shortName) {
      this.save.emit({ name: this.draftName.trim() });
    }
  }
  
  deleteFeat() {
    if (confirm(`Vuoi davvero eliminare "${this.feature().shortName}"?`)) {
      this.delete.emit();
    }
  }
}
