/*
  Impalcatura condivisa delle pagine della console.

  Modulo ES, nessun build step: la console deve poter disegnare senza che un
  bundle compili — è lei che avvia il server, non il contrario.
*/

// ---- Testata e navigazione ----

// L'ordine racconta la giornata: prima si prepara, poi si gioca. Ma la
// Sessione è prima perché è quella che apri quando i giocatori sono già lì.
const PAGES = [
  { href: 'index.html', label: 'Sessione', icon: 'ra-hourglass' },
  { href: 'schede.html', label: 'Schede', icon: 'ra-pawn' },
  { href: 'bestiario.html', label: 'Bestiario', icon: 'ra-monster-skull' },
  { href: 'sistema.html', label: 'Sistema', icon: 'ra-book' },
];

export function mountHeader(current) {
  const header = document.querySelector('header');
  header.innerHTML = `
    <h1><i class="ra ra-dragon"></i>TableLAN</h1>
    <nav class="pages">
      ${PAGES.map(p => `<a href="${p.href}" class="${p.href === current ? 'current' : ''}">
        <i class="ra ${p.icon}"></i>${p.label}</a>`).join('')}
    </nav>
    <span class="url" id="join-url"></span>
    <span class="build" id="build"></span>
    <span class="conn" id="conn" hidden>Server non raggiungibile</span>`;

  refreshJoinUrl();
  showBuild();
}

// La versione non cambia mentre l'app gira: si chiede una volta e resta lì.
function showBuild() {
  fetch('/api/admin/version')
    .then(r => r.json())
    .then(v => { document.getElementById('build').textContent = v.version; })
    .catch(() => { /* la testata funziona anche senza: non è un errore da mostrare */ });
}

function refreshJoinUrl() {
  fetch('/api/admin/join-info')
    .then(r => r.ok ? r.json() : Promise.reject(new Error(r.status)))
    .then(i => { document.getElementById('join-url').textContent = i.url; markOnline(); })
    .catch(() => markOffline());
}

// ---- Stato della connessione ----
//
// Ogni chiamata qui sotto inghiottiva l'errore e restituiva null; chi disegna
// riceveva null e disegnava il vuoto. Il risultato era una console muta:
// tavolo vuoto, niente QR, i bottoni che non fanno niente — e nessun indizio
// sul perché. Il client dei giocatori la spia "Connesso" ce l'ha da sempre;
// il Master, che possiede la macchina, sapeva meno dei suoi giocatori.
//
// L'errore continua a non far esplodere la pagina (un tavolo non si ferma per
// un pacchetto perso), ma adesso si vede, e quando torna si ricarica da sola.

let online = true;
let recovering = null;

// Uscendo dalla pagina il browser annulla le fetch in volo, e l'annullamento
// arriva qui identico a un server morto. Senza questo, cliccare "Schede"
// marcava offline la pagina che stavi lasciando.
let leaving = false;
addEventListener('pagehide', () => { leaving = true; });

function setConn(bad) {
  const el = document.getElementById('conn');
  if (el) el.hidden = !bad;
}

function markOnline() {
  if (online) return;
  online = true;
  setConn(false);
  clearInterval(recovering);
  recovering = null;
  // Niente reload: entrambe le pagine ripollano ogni 3s, quindi tornando su si
  // ripopolano da sole. Il reload che c'era qui annullava la navigazione in
  // corso (cliccavi "Schede" e ti riportava a Sessione) e cancellava i campi
  // che il Master stava compilando. La cura era peggiore del male.
}

function markOffline() {
  if (!online || leaving) return;
  online = false;
  setConn(true);
  // Riprova da sola: se il server torna, il Master non deve fare F5.
  recovering ??= setInterval(refreshJoinUrl, 2000);
}

// ---- Conferma delle azioni irreversibili ----

let pendingConfirm = null;

export function confirmAction(message, onConfirm) {
  pendingConfirm = onConfirm;
  document.getElementById('confirm-msg').textContent = message;
  document.getElementById('confirm-overlay').style.display = 'flex';
}

function hideConfirm() { document.getElementById('confirm-overlay').style.display = 'none'; }

export function mountConfirm() {
  const overlay = document.createElement('div');
  overlay.id = 'confirm-overlay';
  overlay.className = 'overlay';
  overlay.innerHTML = `
    <div class="dialog">
      <p id="confirm-msg"></p>
      <div class="dialog-actions">
        <button class="secondary" id="confirm-no">Annulla</button>
        <button id="confirm-yes">Conferma</button>
      </div>
    </div>`;
  document.body.appendChild(overlay);

  document.getElementById('confirm-yes').onclick = () => {
    const f = pendingConfirm;
    pendingConfirm = null;
    hideConfirm();
    if (f) f();
  };
  document.getElementById('confirm-no').onclick = () => { pendingConfirm = null; hideConfirm(); };
  document.addEventListener('keydown', e => { if (e.key === 'Escape') { pendingConfirm = null; hideConfirm(); } });
}

// ---- Rete ----

export async function api(method, url, body) {
  try {
    const res = await fetch(url, {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    });
    if (!res.ok) {
      // 4xx/5xx: il server c'è e ha detto di no. Non è una disconnessione,
      // ma non deve nemmeno sparire nel nulla come prima.
      console.error(`${method} ${url} → ${res.status}`);
      return null;
    }
    markOnline();
    return res.json().catch(() => null);
  } catch {
    markOffline();
    return null;
  }
}

export async function getState() {
  try {
    const res = await fetch('/api/state');
    if (!res.ok) { console.error(`GET /api/state → ${res.status}`); return null; }
    markOnline();
    return await res.json();
  } catch {
    markOffline();
    return null;
  }
}

// ---- Utilità ----

export const esc = s => String(s ?? '')
  .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/**
 * Etichetta di un costo secondo il profilo attivo. Include la quantità quando
 * è più di uno: senza, un'attività da 2 azioni di Pathfinder si legge "Azione"
 * come una da una sola, e il Master non se ne accorge.
 */
export function costLabel(profile, cost) {
  if (!cost.kind || cost.kind === 'None') return 'Passiva';
  const turn = profile?.turnResources.find(t => t.id === cost.kind);
  if (turn) return (cost.amount || 0) > 1 ? `${turn.label} ×${cost.amount}` : turn.label;
  const pool = profile?.pools.find(x => x.id === cost.kind);
  if (pool) return pool.kind === 'Leveled' ? `${pool.label} L${cost.amount || 1}` : `${pool.label} ×${cost.amount || 1}`;
  return cost.kind;
}

/** Tutti i costi insieme: "Azione + Slot L1". Vuoto = passiva. */
export function costsLabel(profile, costs) {
  return (costs ?? []).length === 0
    ? 'Passiva'
    : costs.map(c => costLabel(profile, c)).join(' + ');
}

/** Un costo è "orfano" se il profilo attivo non sa più cosa sia. */
export function isOrphanCost(profile, cost) {
  if (!cost.kind || cost.kind === 'None') return false;
  return !profile.turnResources.some(t => t.id === cost.kind)
      && !profile.pools.some(x => x.id === cost.kind);
}

/** Vero se almeno un costo della feature è orfano. */
export const hasOrphanCost = (profile, costs) =>
  (costs ?? []).some(c => isOrphanCost(profile, c));

/**
 * Ridisegna solo se qualcosa è cambiato davvero.
 * Serve perché il poll periodico, altrimenti, azzererebbe i form che il Master
 * sta compilando proprio mentre li compila.
 */
export function makeRenderGuard() {
  let last = '';
  return data => {
    const json = JSON.stringify(data);
    if (json === last) return false;
    last = json;
    return true;
  };
}
