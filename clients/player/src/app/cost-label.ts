import { ProfileDto } from './models';

/**
 * Traduce i costi dalla lingua del modello a quella del tavolo.
 *
 * Sta qui e non dentro una pagina perché serve a tutte, e perché quando questa
 * logica viveva solo nella scheda, la pagina di modifica mostrava `BonusAction`
 * — l'id interno, quello che il profilo promette di non mostrare mai. Non è
 * stata una svista isolata: è ciò che succede quando la traduzione è di
 * qualcuno invece che di tutti.
 */

interface Cost {
  kind: string;
  amount: number;
}

/** Un costo solo, secondo il profilo attivo. */
function oneCostLabel(profile: ProfileDto | null, cost: Cost): string {
  const turn = profile?.turnResources.find(t => t.id === cost.kind);
  // La quantità conta: senza, un'attività da 2 azioni si legge come una da una.
  if (turn) return (cost.amount || 0) > 1 ? `${turn.label} ×${cost.amount}` : turn.label;

  const pool = profile?.pools.find(x => x.id === cost.kind);
  if (pool) {
    return pool.kind === 'Leveled'
      ? `${pool.label} L${cost.amount || 1}`
      : `${pool.label} ×${cost.amount || 1}`;
  }

  // Il profilo non sa più cosa sia: si mostra com'è, ed è l'unico caso in cui
  // l'id affiora — succede cambiando sistema, e la scheda lo segnala a parte.
  return cost.kind;
}

/** Tutto quello che costa, insieme: "Azione + Slot incantesimo L1". */
export function costLabel(profile: ProfileDto | null, costs: Cost[]): string {
  return costs.length ? costs.map(c => oneCostLabel(profile, c)).join(' + ') : '';
}

/**
 * Cosa ti rende: "+2 Attacco", "+1 Azione".
 *
 * Col segno davanti, perché sta accanto al costo e la differenza fra le due
 * cose deve leggersi senza rifletterci: "Azione → +2 Attacco".
 */
export function grantLabel(profile: ProfileDto | null, grants: Cost[]): string {
  return grants.length
    ? grants.map(g => `+${g.amount || 1} ${turnLabel(profile, g.kind)}`).join(' ')
    : '';
}

function turnLabel(profile: ProfileDto | null, kind: string): string {
  return profile?.turnResources.find(t => t.id === kind)?.label ?? kind;
}
