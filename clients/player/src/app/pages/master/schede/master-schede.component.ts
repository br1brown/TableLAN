import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CharacterDto } from '../../../models';
import { TableService } from '../../../table.service';

/**
 * Elenco delle schede (port di wwwroot/admin/schede.html): un PG e un mostro
 * sono la stessa scheda, si aprono nello stesso editor. La pagina non è più un
 * secondo editor scritto a mano — quella duplicazione è il motivo per cui la
 * modifica mostrava "BonusAction" mentre la scheda diceva "Azione Bonus".
 *
 * Include l'import da un PDF di D&D Beyond: il file si legge in memoria e non
 * resta da nessuna parte.
 */
@Component({
  selector: 'app-master-schede',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './master-schede.component.html',
})
export class MasterSchedeComponent implements OnInit, OnDestroy {
  private readonly table = inject(TableService);

  protected readonly characters = computed(() => this.table.state()?.characters ?? []);
  protected readonly monsters = signal<CharacterDto[]>([]);
  private poll?: ReturnType<typeof setInterval>;

  /** Esito dell'import: tono (ok/errore/lavoro) e testo. */
  protected readonly importState = signal<{ tone: 'ok' | 'ko' | 'busy'; html: string } | null>(null);
  protected readonly importing = signal(false);

  async ngOnInit(): Promise<void> {
    this.monsters.set(await this.table.monsters());
    this.poll = setInterval(async () => this.monsters.set(await this.table.monsters()), 3000);
  }

  ngOnDestroy(): void {
    clearInterval(this.poll);
  }

  meta(c: CharacterDto): string {
    const ca = c.customStats?.['CA'] ?? '—';
    return `PF ${c.hp.current}/${c.hp.max} · CA ${ca} · ${c.features.length} capacità`;
  }

  async importPdf(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    if (!file) {
      this.importState.set({ tone: 'ko', html: 'Scegli prima il PDF della scheda.' });
      return;
    }

    // Due click uguali farebbero due schede identiche: il bottone si spegne.
    this.importing.set(true);
    this.importState.set({ tone: 'busy', html: `Leggo ${file.name}…` });

    try {
      const { ok, data } = await this.table.importDdb(file);
      if (!ok) {
        this.importState.set({ tone: 'ko', html: data.error ?? 'Import non riuscito.' });
        return;
      }
      const avvisi: string[] = data.avvisi ?? [];
      this.importState.set({
        tone: 'ok',
        html:
          `<strong>${data.nome}</strong> importato: ${data.feature} capacità ` +
          `(${data.incantesimi} incantesimi) da ${data.fonti} fonti.` +
          (avvisi.length ? `<ul class="mb-0 mt-1">${avvisi.map(a => `<li>${a}</li>`).join('')}</ul>` : ''),
      });
      input.value = '';
    } catch (e: any) {
      this.importState.set({ tone: 'ko', html: e?.message ?? 'Errore imprevisto.' });
    } finally {
      this.importing.set(false);
    }
  }
}
