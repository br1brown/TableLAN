import { ChangeDetectionStrategy, Component, OnInit, effect, inject, signal, computed } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableService } from '../../table.service';
import { costLabel } from '../../cost-label';
import { CharacterDto, FeatureDraft } from '../../models';
import { FeatureEditorComponent } from '../../components/feature-editor/feature-editor';

@Component({
  selector: 'app-edit-sheet',
  standalone: true,
  imports: [CommonModule, FormsModule, FeatureEditorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './edit-sheet.component.html',
})
export class EditSheetComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private location = inject(Location);
  protected table = inject(TableService);

  characterId = signal<string | null>(null);

  /**
   * La scheda di un mostro, quando l'id non è fra i giocatori.
   *
   * Arriva da `/api/admin/*`, che risponde solo su loopback: la console del
   * Master la ottiene, un telefono in LAN prende 403 e qui resta null. Stessa
   * pagina, stesso codice — è il server a decidere chi vede cosa.
   */
  private readonly monster = signal<CharacterDto | null>(null);

  /** Vero se stiamo modificando un mostro: cambia solo la rotta di salvataggio. */
  protected readonly isMonster = computed(() => this.monster()?.id === this.characterId());

  protected character = computed(() => {
    const id = this.characterId();
    const daiGiocatori = this.table.state()?.characters.find(c => c.id === id);
    if (daiGiocatori) return daiGiocatori;

    const m = this.monster();
    return m?.id === id ? m : null;
  });

  protected profile = computed(() => this.table.state()?.profile ?? null);

  /** Gli stessi costi che legge il giocatore sulla scheda, con le stesse parole. */
  protected costLabel(f: { costs: { kind: string; amount: number }[] }): string {
    return costLabel(this.profile(), f.costs);
  }

  // Draft states
  protected draftName = signal('');
  
  // Feature Add Form Draft
  protected readonly showAddForm = signal(false);
  protected readonly featureDraft = {
    shortName: '',
    costKind: 'None',
    maxUses: '' as string,
    recharge: '',
    description: '',
  };

  /** L'ultima scheda per cui il campo è stato riempito. */
  private syncedFor: string | null = null;

  constructor() {
    // Il nome si allinea quando la scheda arriva, non quando arriva l'id:
    // aprendo /edit/:id di persona lo stato non c'è ancora, e leggerlo dentro
    // paramMap lo trovava null lasciando il campo vuoto per sempre.
    //
    // Si allinea una volta per scheda, non a ogni ridiffusione: il tavolo
    // ribatte lo stato di continuo, e riscrivere il campo mentre lo stai
    // battendo te lo cancellerebbe sotto le dita. Ma cambiando scheda deve
    // ripartire — l'id è la chiave, non il fatto che il campo sia vuoto.
    effect(() => {
      const c = this.character();
      if (!c || this.syncedFor === c.id) return;
      this.syncedFor = c.id;
      this.draftName.set(c.name);
      this.draftHp.set({ current: c.hp.current, max: c.hp.max });
    });
  }

  ngOnInit() {
    this.route.paramMap.subscribe(async params => {
      const id = params.get('id');
      this.characterId.set(id);
      this.monster.set(null);

      // Se non è fra i giocatori, può essere un mostro: si chiede, e se non ne
      // abbiamo il diritto la risposta è "no" e la pagina dirà che non c'è.
      if (id && !this.table.state()?.characters.some(c => c.id === id)) {
        this.monster.set(await this.table.monster(id));
      }
    });
  }

  async saveName() {
    const id = this.characterId();
    const name = this.draftName().trim();
    if (id && name) {
      await this.table.updateName(id, name);
    }
  }

  async addStat(inputEl: HTMLInputElement) {
    const id = this.characterId();
    const name = inputEl.value.trim();
    if (id && name) {
      await this.table.updateStat(id, name, 0);
      inputEl.value = '';
    }
  }

  async removeStat(name: string) {
    const id = this.characterId();
    if (id && confirm(`Rimuovere la statistica ${name}?`)) {
      await this.table.deleteStat(id, name);
    }
  }

  async updateStat(name: string, value: any) {
    const id = this.characterId();
    if (id) {
      const parsed = isNaN(Number(value)) ? value : Number(value);
      await this.table.updateStat(id, name, parsed);
    }
  }

  /**
   * Le riserve del profilo con quel che la scheda ne ha: una riga per livello
   * di slot, una sola (livello 0) per i pool a punti.
   *
   * Il profilo comanda l'elenco, non la scheda: una riserva a zero non compare
   * fra i `pools` del personaggio, e senza il profilo non ci sarebbe modo di
   * dargliela la prima volta.
   */
  protected readonly reserves = computed(() => {
    const c = this.character();
    const p = this.profile();
    if (!c || !p) return [];

    return p.pools.map(pool => {
      const stato = c.pools[pool.id];
      const livelli = pool.kind === 'Leveled'
        ? [1, 2, 3, 4, 5, 6, 7, 8, 9]
        : [0];
      return {
        id: pool.id,
        label: pool.label,
        kind: pool.kind,
        tiers: livelli.map(level => ({
          level,
          max: stato?.tiers[level]?.max ?? 0,
          remaining: stato?.tiers[level]?.remaining ?? 0,
        })),
      };
    });
  });

  /** Quanti slot/punti di questo livello ha il personaggio. 0 = non ce l'ha. */
  async setPoolMax(poolId: string, level: number, value: string | number) {
    const id = this.characterId();
    if (!id) return;
    const max = Math.max(0, Number(value) || 0);
    await this.table.setPool(id, poolId, level, max);
  }

  /** I PF in bozza: si toccano qui, si salvano quando lo dici tu. */
  protected readonly draftHp = signal<{ current: number; max: number }>({ current: 0, max: 1 });

  protected setDraftHp(campo: 'current' | 'max', valore: string | number): void {
    this.draftHp.update(h => ({ ...h, [campo]: Number(valore) || 0 }));
  }

  /** Il massimo che conta davvero: base più gli effetti attivi. */
  protected effectiveMaxHp(c: CharacterDto): number {
    return c.hp.max;
  }

  /** Il modificatore già formattato col segno, o null se la statistica non ne ha. */
  protected modOf(c: CharacterDto, stat: string): string | null {
    const m = c.statModifiers?.[stat];
    return m === undefined ? null : (m >= 0 ? `+${m}` : `−${Math.abs(m)}`);
  }

  protected async saveHp(): Promise<void> {
    const id = this.characterId();
    if (!id) return;
    const { current, max } = this.draftHp();

    const res = this.isMonster()
      ? await this.table.setMonsterHp(id, max, current)
      : await this.table.setHp(id, max, current);

    if (!res.ok) {
      this.saveError.set('Non si sono potuti salvare i PF.');
      return;
    }
    this.saveError.set(null);
    if (this.isMonster()) this.monster.set(await this.table.monster(id));
  }

  /** I testi già scaricati: arrivano lazy, solo per la capacità che apri. */
  protected readonly descriptions = signal(new Map<string, string>());

  /**
   * I bersagli possibili di un effetto: le statistiche della scheda più i PF
   * massimi. Vengono dalla scheda, non da una lista cablata — le statistiche
   * le dichiara il profilo, o le inventa il Master.
   */
  protected effectTargets(c: CharacterDto): string[] {
    return [...Object.keys(c.customStats), '@maxhp'];
  }

  protected async loadDescription(descriptionId: string): Promise<void> {
    if (!descriptionId || this.descriptions().has(descriptionId)) return;
    const testo = await this.table.description(descriptionId);
    this.descriptions.update(m => new Map(m).set(descriptionId, testo));
  }

  /**
   * Salva tutto ciò che è della capacità in una volta.
   *
   * Il server valida la formula e rifiuta con un motivo: una rotta salvata
   * male è una capacità che non tirerà mai, e lo scopriresti al tavolo.
   */
  protected async saveFeature(featureId: string, draft: FeatureDraft): Promise<void> {
    const id = this.characterId();
    if (!id) return;

    // Un mostro non è raggiungibile dalle rotte dei PG — è ciò che impedisce a
    // un telefono di curare l'orso — quindi passa dalle sue, su loopback.
    const res = this.isMonster()
      ? await this.table.updateMonsterFeature(id, featureId, draft)
      : await this.table.updateFeature(id, featureId, draft);

    if (!res.ok) {
      const errore = await res.json().catch(() => null);
      this.saveError.set(errore?.error ?? 'Non si è potuto salvare.');
      return;
    }
    this.saveError.set(null);
    if (this.isMonster()) this.monster.set(await this.table.monster(id));
  }

  /** L'ultimo rifiuto del server, in chiaro. Null = tutto a posto. */
  protected readonly saveError = signal<string | null>(null);

  async deleteFeature(featureId: string) {
    const id = this.characterId();
    if (id && confirm('Rimuovere questa feature?')) {
      await this.table.deleteFeature(id, featureId);
    }
  }

  async submitFeatureDraft() {
    const id = this.characterId();
    if (!id || !this.featureDraft.shortName.trim()) return;

    const body = {
      ...this.featureDraft,
      maxUses: this.featureDraft.maxUses ? parseInt(this.featureDraft.maxUses) : null,
      recharge: this.featureDraft.maxUses ? this.featureDraft.recharge : null,
    };

    await this.table.addFeature(id, body);
    this.showAddForm.set(false);
    this.featureDraft.shortName = '';
    this.featureDraft.description = '';
  }

  goBack() {
    this.location.back();
  }
}
