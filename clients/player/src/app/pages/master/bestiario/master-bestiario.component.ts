import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CharacterDto } from '../../../models';
import { TableService } from '../../../table.service';

/** Un mostro è una scheda come un PG (stesso DTO), con in più le note libere. */
type MonsterDto = CharacterDto & { notes?: string | null };

/**
 * Bestiario del Master (port di wwwroot/admin/bestiario.html): i blocchi
 * statistici, solo suoi. Vive dietro `/api/admin/*`, quindi i giocatori non lo
 * vedono. Un mostro è un personaggio: aprirne la scheda porta all'editor
 * comune.
 */
@Component({
  selector: 'app-master-bestiario',
  standalone: true,
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './master-bestiario.component.html',
})
export class MasterBestiarioComponent implements OnInit, OnDestroy {
  private readonly table = inject(TableService);

  protected readonly monsters = signal<MonsterDto[]>([]);
  private poll?: ReturnType<typeof setInterval>;

  // Nome e note si azzerano dopo l'aggiunta (dietro un await): signal, così con
  // OnPush il reset si vede davvero. PF e CA li tocca solo chi digita, e li si
  // legge al volo in add(): restano campi semplici.
  protected readonly name = signal('');
  protected readonly notes = signal('');
  protected hp = 10;
  protected ac = 12;

  async ngOnInit(): Promise<void> {
    await this.refresh();
    this.poll = setInterval(() => this.refresh(), 3000);
  }

  ngOnDestroy(): void {
    clearInterval(this.poll);
  }

  private async refresh(): Promise<void> {
    this.monsters.set(await this.table.monsters());
  }

  ca(m: MonsterDto): unknown {
    return m.customStats?.['CA'] ?? '—';
  }

  async add(): Promise<void> {
    const name = this.name().trim();
    if (!name) return;
    await this.table.addMonster(
      name,
      Math.max(1, Math.trunc(this.hp) || 1),
      Math.max(1, Math.trunc(this.ac) || 10),
      this.notes().trim() || null,
    );
    this.name.set('');
    this.notes.set('');
    await this.refresh();
  }

  async changeHp(id: string, delta: number): Promise<void> {
    const updated = await this.table.adjustMonsterHp(id, delta);
    if (updated) this.monsters.set(updated);
  }

  async remove(m: MonsterDto): Promise<void> {
    if (!confirm(`Rimuovere «${m.name}» dal bestiario?`)) return;
    const updated = await this.table.deleteMonster(m.id);
    if (updated) this.monsters.set(updated);
  }

  /** Sdoppia il mostro: un'altra istanza con PF pieni (Goblin → Goblin (2)). */
  async duplicate(m: MonsterDto): Promise<void> {
    const updated = await this.table.duplicateMonster(m.id);
    if (updated) this.monsters.set(updated);
  }
}
