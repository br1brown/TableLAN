import { Component, input, inject, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FeatureDto } from '../../models';

@Component({
  selector: 'app-feature-row',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <article class="feature" [class.disabled]="!canUse() && !feature().toggleable" [class.off]="feature().toggleable && !feature().active">
      
      @if (editMode()) {
        <!-- Modalità Modifica -->
        <div style="grid-column: 1 / -1; display: flex; flex-direction: column; gap: .5rem; padding: .5rem; background: var(--surface-2); border-radius: var(--radius-sm); margin-bottom: .5rem;">
          <input [(ngModel)]="draftName" placeholder="Nome" style="background: var(--bg); color: var(--text); padding: .4rem; border: 1px solid var(--border); border-radius: var(--radius-sm);">
          <div style="display: flex; gap: .5rem;">
            <button class="secondary" (click)="saveEdit()">Salva</button>
            <button class="ghost" (click)="deleteFeat()" style="color: #dc3545;">Elimina</button>
          </div>
        </div>
      } @else {
        <!-- Modalità Normale -->
        <button class="feature-main" (click)="toggle.emit()">
          <span class="feature-name">
            {{ feature().shortName }}
            <span class="feature-from">{{ sourceLabel() }}</span>
          </span>
          <span class="feature-meta">
            @if (feature().costs.length) {
              <span class="cost"><i class="ra" [class]="iconForFeature()"></i> {{ costLabel() }}</span>
            }
            <!-- Cosa ti rende: l'azione di Attacco vale due colpi, Action
                 Surge ti ridà l'Azione. Senza, costa e basta — e la riga
                 mentirebbe per omissione. -->
            @if (grantLabel()) {
              <span class="grant">{{ grantLabel() }}</span>
            }
            @if (feature().usage) {
              <span class="uses">{{ feature().usage?.remaining }}/{{ feature().usage?.max }}</span>
            }
          </span>
        </button>
        
        @if (feature().toggleable) {
          <button class="toggle" [class.on]="feature().active" (click)="toggleEffect.emit()">
            {{ feature().active ? 'Indossato' : 'Rimosso' }}
          </button>
        } @else if (feature().roll) {
          <!-- Sul bottone il conto già fatto ("1d20+6"); la formula com'è
               scritta resta nel title, per chi vuole controllarla. -->
          <button class="roll" [disabled]="!canUse()" (click)="roll.emit()" [title]="'Tira ' + feature().roll">
            <i class="ra ra-perspective-dice-six"></i> {{ feature().rollLabel ?? feature().roll }}
          </button>
        } @else if (feature().costs.length || feature().usage) {
          <button class="use" [disabled]="!canUse()" (click)="use.emit()">Usa</button>
        }
        
        @if (feature().effects.length) {
          <div class="effects">
            @for (e of feature().effects; track $index) {
              <span class="eff">{{ effectLabel()(e) }}</span>
            }
          </div>
        }
        
        @if (!feature().toggleable && feature().blockedReason) {
          <p class="blocked">{{ feature().blockedReason }}</p>
        }

        <!-- Non è un blocco: usarla si può, ma qualcosa cade. Detto prima di
             toccare il bottone, non dopo aver speso lo slot. -->
        @if (feature().wouldReplace; as caduta) {
          <p class="replaces">Chiuderebbe: {{ caduta }}</p>
        }
        
        @if (expanded()) {
          <p class="description">
            {{ description() || 'Caricamento…' }}
          </p>
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
