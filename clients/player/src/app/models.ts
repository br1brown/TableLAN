// DTO dello snapshot di stato inviato dal motore via SignalR.
// Il client non calcola meccaniche: si ridisegna su ciò che riceve, e
// interpreta costi/turno/riserve tramite il profilo di sistema.

export interface TableState {
  profile: ProfileDto;
  characters: CharacterDto[];
  initiative?: InitiativeDto;
}

/**
 * L'iniziativa del Master. Al giocatore serve per una cosa sola: sapere che
 * tocca a lui.
 *
 * Non tocca le sue risorse — il turno lo chiude lui dalla scheda. Le due cose
 * sono indipendenti: il Master sposta il segnalino quando vuole, anche
 * correggendo un errore, e nessuna scheda ne risente.
 */
export interface InitiativeDto {
  order: InitiativeEntryDto[];
  activeId: string | null;
}

export interface InitiativeEntryDto {
  refId: string;
  kind: 'pc' | 'monster';
  name: string;
  initiative: number;
}

export interface ProfileDto {
  id: string;
  name: string;
  turnResources: TurnResourceDto[];
  pools: PoolDefDto[];
  cycles: CycleDto[];
}

export interface TurnResourceDto {
  id: string;
  label: string;
  perTurn: number;
}

export interface PoolDefDto {
  id: string;
  label: string;
  kind: 'Leveled' | 'Points';
  rechargeCycle: string;
}

export interface CycleDto {
  id: string;
  label: string;
  rank: number;
  perTurn: boolean;
}

export interface CharacterDto {
  id: string;
  name: string;
  hp: { current: number; max: number };
  customStats: Record<string, unknown>;
  /**
   * Il modificatore, per le sole statistiche che ne hanno uno secondo il
   * profilo. È il numero che si somma davvero al dado: "Forza 18" da solo non
   * dice cosa sommare, e prima la scheda non lo diceva affatto.
   */
  statModifiers: Record<string, number>;
  /** Riserve del personaggio, per pool id → livelli. */
  pools: Record<string, PoolStateDto>;
  inventory: InventoryItemDto[];
  /** Economia del turno: id risorsa → residui in questo turno. */
  turn: Record<string, number>;
  turnMax?: Record<string, number>;
  /** Slot esclusivi occupati (in 5e: su cosa stai concentrando). Dura fra i turni. */
  occupied?: OccupiedSlotDto[];
  sources: SourceDto[];
  features: FeatureDto[];
}

/** Uno slot esclusivo occupato, coi nomi già pronti da leggere. */
export interface OccupiedSlotDto {
  slot: string;
  label: string;
  featureId: string;
  featureName: string;
}

export interface PoolStateDto {
  label: string;
  kind: 'Leveled' | 'Points';
  tiers: Record<string, { max: number; remaining: number }>;
}

export interface InventoryItemDto {
  id: string;
  name: string;
  quantity: number;
  notes: string | null;
}

export interface SourceDto {
  id: string;
  name: string;
  type: string;
  parentSourceId: string | null;
}

export interface EffectDto {
  target: string; // nome statistica, o "@maxhp" per i PF massimi
  op: 'Add' | 'Set';
  value: number;
}

/**
 * Tutto ciò che è di una feature, nella forma che la rotta PUT accetta.
 *
 * È una cosa sola perché "cambiare l'Attacco Furtivo" è un gesto solo: prima
 * il costo, il tiro e il resto erano rotte separate, e nome, usi, ricarica e
 * testo non si potevano cambiare affatto — si cancellava e si rifaceva.
 */
export interface FeatureDraft {
  shortName: string;
  costs: CostDto[];
  maxUses: number | null;
  recharge: string | null;
  roll: string | null;
  description: string | null;
  toggleable: boolean;
  effects: EffectDto[];
}

export interface CostDto {
  kind: string;
  amount: number;
}

export interface FeatureDto {
  id: string;
  shortName: string;
  sourceIds: string[];
  /**
   * Cosa costa attivarla. Lista, non uno solo: lanciare un incantesimo consuma
   * l'Azione E lo slot. Vuota = passiva.
   */
  costs: { kind: string; amount: number }[];
  usage: { max: number; remaining: number; recharge: string } | null;
  /** Effetti che modificano la scheda quando la feature è attiva. */
  effects: EffectDto[];
  /** Oggetto indossabile: gli effetti si accendono/spengono. */
  toggleable: boolean;
  /** Se la feature (con effetti) è attualmente attiva. */
  active: boolean;
  // Puntatore di deduplicazione: il testo si scarica solo all'espansione.
  descriptionId: string;
  customData: Record<string, unknown>;
  /**
   * Verdetto del motore, già calcolato lato server: il client non rivaluta le
   * regole (Capitolo 8), le mostra. `blockedReason` è il perché, in chiaro,
   * disponibile PRIMA del tap — non più solo come toast dopo il rifiuto.
   */
  canUse: boolean;
  blockedReason: string | null;
  /** Risorse di turno che usarla accredita (Action Surge, Attacco Extra). */
  grants: { kind: string; amount: number }[];
  /** Slot esclusivo che occuperebbe (in 5e: "Concentration"), null quasi sempre. */
  occupies: string | null;
  /** Nome di ciò che cadrebbe usandola. Null se non butteresti giù niente. */
  wouldReplace: string | null;
  /** Formula del tiro (es. "1d6", "1d20+@CA"), null se la feature non tira. */
  roll: string | null;
  /**
   * La stessa formula coi punteggi già risolti ("1d20+6"), dal motore. Null
   * se la feature non tira o se una statistica non si risolve: allora si
   * mostra `roll` grezza.
   */
  rollLabel: string | null;
}

/** Un dado dentro un tentativo: facce, valore, ed eventuale ruolo. */
export interface DieView {
  sides: number;
  value: number;
  /** "hope"/"fear" nella Duality di Daggerheart; null per i dadi anonimi. */
  role: string | null;
}

/** Esito di un tiro, dal server. Conserva anche i tentativi scartati. */
export interface RollEntryDto {
  id: number;
  characterId: string;
  characterName: string;
  label: string;
  formula: string;
  total: number;
  spent: boolean;
  /** Per tentativo: i dadi (con ruolo), il modificatore, il totale. */
  attempts: { dice: DieView[]; modifier: number; total: number }[];
  keptIndex: number;
  /** Esito categorico del tiro tenuto ("hope"/"fear"/"crit"), o null. */
  outcome: string | null;
}

export interface IntentRejection {
  characterId: string;
  featureId: string;
  reason: string;
}
