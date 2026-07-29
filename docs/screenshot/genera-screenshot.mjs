// Genera TUTTI gli screenshot del README — i 26 del tour (docs/guida) e i 3
// "poster" in cima (docs/screenshot) — guidando il vero client Angular con un
// browser headless. Mantiene i nomi dei file, così il README non va toccato.
//
// Prerequisiti (una volta):
//   - il server avviato con un db di scena e un IP LAN da vetrina, es.:
//       TABLELAN_PORT=5116 TABLELAN_DB=/tmp/scena.db dotnet run \
//         --project src/TableLAN.Server -c Debug -- --headless
//   - il client servito con ng serve, che fa da proxy al server:
//       (proxy.conf.json → 127.0.0.1:5116)  ng serve --port 4200
//   - Chromium + le librerie del browser:
//       npm i playwright-core sharp     # il Chromium è già in /opt/pw-browsers
//
// Poi, dalla cartella con playwright-core/sharp installati:
//   BASE=http://localhost:4200 API=http://127.0.0.1:5116 \
//   CHROME=/opt/pw-browsers/chromium-1194/chrome-linux/chrome \
//   node docs/screenshot/genera-screenshot.mjs
//
// GLI ATTORI NON SONO CABLATI. Lo script non sa che i pregen si chiamano Kael e
// Lyra: li ricava da /api/state (vedi resolveActors), così se domani cambi i
// personaggi di test le schermate si adattano da sole — basta che nel seed
// restino "un eroe e un incantatore". Le uniche cose fisse sono di *scena* (i
// mostri, i numeri d'iniziativa, il tiro Duality): stanno nel blocco SCENA qui
// sotto, un posto solo da toccare.
//
// I tiri arrivano dal vivo (SignalR non riproduce lo storico): il log si riempie
// lanciando i tiri MENTRE la pagina è aperta — vedi fireHistory().

import { chromium } from 'playwright-core';
import sharp from 'sharp';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const GUIDA = resolve(HERE, '../guida');
const HERO = HERE;

const BASE = process.env.BASE ?? 'http://localhost:4200';
const API = process.env.API ?? 'http://127.0.0.1:5116';
const EXE = process.env.CHROME ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';

const j = (m, p, b) => fetch(API + p, {
  method: m, headers: { 'Content-Type': 'application/json' },
  body: b === undefined ? undefined : JSON.stringify(b),
}).then(async r => ({ ok: r.ok, data: await r.json().catch(() => ({})) }));

// ============================================================================
// SCENA — le uniche cose fisse, di scena e non del seed. Cambiale qui.
// ============================================================================
const MONSTERS = [
  { name: 'Goblin', maxHp: 7, armorClass: 15, notes: null },
  { name: 'Orso Gufo', maxHp: 59, armorClass: 13, notes: 'Multiattacco: artigli e becco.' },
];
// Il mostro che entra in iniziativa (l'ultimo della lista, il più "grosso").
const INITIATIVE_MONSTER = MONSTERS[MONSTERS.length - 1].name;
// La capacità Duality, aggiunta all'incantatore per lo scatto Speranza/Paura.
const DUALITY = { shortName: 'Colpo del Destino', roll: 'duality: 2d12' };
// I numeri d'iniziativa: chi va prima. L'eroe è di turno (segnalino attivo).
const INITIATIVE = { hero: 18, monster: 14, caster: 11 };
// Stati da mettere in scena sull'iniziativa (etichetta + round che restano), così
// gli scatti mostrano gli stati per-sistema e non un'iniziativa nuda.
const CONDITIONS = { hero: { label: 'Avvelenato', rounds: 3 }, monster: { label: 'Prono', rounds: null } };
// PF temporanei sull'eroe: il cuscinetto anti-danno compare come +N sulla scheda.
const HERO_TEMP_HP = 5;

// ---- Attori, ricavati dallo stato vivo (niente nomi/id cablati) ----
//   caster = un personaggio con slot a livelli (un incantatore) → 08-incantatrice
//   hero   = il primo diverso dal caster → le schermate della scheda (04-17)
//   heroRollFeatureId = una sua capacità che tira → l'overlay del tiro (06)
function resolveActors(state) {
  const chars = state.characters ?? [];
  if (chars.length < 2)
    throw new Error(`Servono almeno 2 personaggi nel seed, trovati ${chars.length}.`);

  const hasLeveledPool = c => Object.values(c.pools ?? {}).some(p => p.kind === 'Leveled');
  const caster = chars.find(hasLeveledPool)
              ?? chars.find(c => Object.keys(c.pools ?? {}).length > 0)
              ?? chars[1];
  const hero = chars.find(c => c.id !== caster.id) ?? chars[0];
  const rollFeat = (hero.features ?? []).find(f => f.roll)
                ?? chars.flatMap(c => c.features ?? []).find(f => f.roll);

  return {
    hero: { id: hero.id, name: hero.name },
    caster: { id: caster.id, name: caster.name },
    heroRollFeatureId: rollFeat?.id ?? null,
  };
}

// ---- Scena: mostri, capacità Duality dell'incantatore, iniziativa ----
async function setupScene(actors) {
  for (const m of MONSTERS) await j('POST', '/api/admin/monsters', m);
  const monsters = (await j('GET', '/api/admin/monsters')).data;
  const monster = monsters.find(m => m.name === INITIATIVE_MONSTER);

  const add = await j('POST', `/api/characters/${actors.caster.id}/features`, { shortName: DUALITY.shortName, costKind: 'None' });
  const fid = add.data.id;
  await j('PUT', `/api/characters/${actors.caster.id}/features/${fid}`, { shortName: DUALITY.shortName, roll: DUALITY.roll });

  await j('POST', '/api/admin/initiative', {
    order: [
      { refId: actors.hero.id, kind: 'pc', name: actors.hero.name, initiative: INITIATIVE.hero },
      { refId: monster.id, kind: 'monster', name: monster.name, initiative: INITIATIVE.monster },
      { refId: actors.caster.id, kind: 'pc', name: actors.caster.name, initiative: INITIATIVE.caster },
    ],
    activeId: actors.hero.id,
  });

  // Stati sull'iniziativa + PF temporanei sull'eroe: le novità di combattimento
  // devono vedersi negli scatti, non solo esistere.
  await j('POST', `/api/admin/conditions/${actors.hero.id}`, CONDITIONS.hero);
  await j('POST', `/api/admin/conditions/${monster.id}`, CONDITIONS.monster);
  await j('POST', `/api/characters/${actors.hero.id}/temp-hp`, { value: HERO_TEMP_HP });

  // Un giro completo riporta il segnalino sull'eroe e fa segnare «Round 1»: il
  // contatore di round parte da 0 e sale solo quando il giro torna in cima.
  for (let i = 0; i < 3; i++) await j('POST', '/api/admin/initiative/next', {});

  return { fid };
}

// I tiri vanno lanciati mentre la pagina del log è aperta (SignalR è live-only).
async function fireHistory(actors, fid) {
  await j('POST', '/api/admin/roll', { formula: '1d20', label: 'Prova di Percezione' });
  for (let i = 0; i < 8; i++)
    await j('POST', `/api/characters/${actors.caster.id}/roll`, { featureId: fid, spend: false });
}

async function save(buf, dir, name, width) {
  await sharp(buf).resize({ width }).png({ palette: true, quality: 90, compressionLevel: 9 })
    .toFile(`${dir}/${name}.png`);
  console.log(`  💾 ${name}.png`);
}
const results = [];
async function shot(label, fn) {
  try { await fn(); results.push(`✅ ${label}`); }
  catch (e) { results.push(`❌ ${label} — ${e.message.split('\n')[0]}`); console.log(`  ❌ ${label}: ${e.message.split('\n')[0]}`); }
}

const browser = await chromium.launch({ executablePath: EXE, headless: true });
async function ctx(w, h) {
  const c = await browser.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: 2 });
  return { c, p: await c.newPage() };
}
const settle = (p, ms = 700) => p.waitForTimeout(ms);

try {
  const actors = resolveActors((await j('GET', '/api/state')).data);
  console.log(`attori: ${actors.hero.name} (eroe) · ${actors.caster.name} (incantatore)`);
  const { fid } = await setupScene(actors);
  console.log('scena pronta');

  // =================== CONSOLE (1280×860 → 1200w) ===================
  {
    const { c, p } = await ctx(1280, 860);

    await shot('01-console-master', async () => {
      await p.goto(`${BASE}/master`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('app-dice-roller .btn-outline-warning');
      await p.waitForSelector('text=Connesso');
      await settle(p, 800);
      await fireHistory(actors, fid);
      await p.waitForSelector('aside .border-top', { timeout: 8000 });
      await settle(p, 600);
      await save(await p.screenshot(), GUIDA, '01-console-master', 1200);
      await save(await p.screenshot(), HERO, '02-console-master', 1200);
    });

    await shot('18-iniziativa', async () => {
      const card = p.locator('.card', { has: p.getByText('Iniziativa') }).first();
      await save(await card.screenshot(), GUIDA, '18-iniziativa', 1200);
    });

    await shot('19-tiri', async () => {
      const card = p.locator('aside .card').first();
      await save(await card.screenshot(), GUIDA, '19-tiri', 480);
    });

    await shot('02-qr-invito', async () => {
      await p.locator('button', { hasText: 'Mostra QR Invito' }).click();
      await p.waitForSelector('.modal img');
      await settle(p);
      await save(await p.screenshot(), GUIDA, '02-qr-invito', 1200);
      await p.keyboard.press('Escape').catch(() => {});
      await p.locator('.modal-backdrop').click({ force: true }).catch(() => {});
    });

    await shot('20-schede', async () => {
      await p.goto(`${BASE}/master/schede`, { waitUntil: 'domcontentloaded' });
      await settle(p, 1000);
      await save(await p.screenshot(), GUIDA, '20-schede', 1200);
    });

    await shot('21-bestiario', async () => {
      await p.goto(`${BASE}/master/bestiario`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector(`text=${INITIATIVE_MONSTER}`);
      await settle(p);
      await save(await p.screenshot(), GUIDA, '21-bestiario', 1200);
    });

    await shot('22-bestiario-aggiungi', async () => {
      await p.locator('text=+ Aggiungi un mostro').click();
      await settle(p);
      await save(await p.screenshot(), GUIDA, '22-bestiario-aggiungi', 1200);
    });

    await shot('23-sistema', async () => {
      await p.goto(`${BASE}/master/sistema`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=Dadi sul tavolo');
      await settle(p, 900);
      await save(await p.screenshot(), GUIDA, '23-sistema', 1200);
    });

    await shot('24-campagna', async () => {
      const card = p.locator('.card', { has: p.getByText('Campagna:') }).first();
      await save(await card.screenshot(), GUIDA, '24-campagna', 1200);
    });

    await shot('26-sistema-json', async () => {
      await p.locator('button', { hasText: 'JSON avanzato' }).click();
      await settle(p);
      await save(await p.screenshot({ fullPage: true }), GUIDA, '26-sistema-json', 1200);
    });

    await shot('25-sistema-preset', async () => {
      await p.goto(`${BASE}/master/sistema`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=Parti da un preset');
      const sel = p.locator('select', { has: p.locator('option', { hasText: 'Call of Cthulhu' }) }).first();
      await sel.selectOption({ label: 'Call of Cthulhu' });
      await p.locator('button', { hasText: /^Carica$/ }).click();
      await settle(p, 900);
      await save(await p.screenshot(), GUIDA, '25-sistema-preset', 1200);
    });

    await c.close();
  }

  // =================== SCHEDA GIOCATORE (390×844 → 480w) ===================
  {
    const { c, p } = await ctx(390, 844);
    const goHero = async () => {
      await p.goto(`${BASE}/${actors.hero.id}`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=Punti Ferita');
      await settle(p, 500);
    };

    await shot('03-chi-sei', async () => {
      await p.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=Chi sei?');
      await settle(p);
      await save(await p.screenshot(), GUIDA, '03-chi-sei', 480);
    });

    await shot('04-scheda-intera', async () => {
      await goHero();
      await save(await p.screenshot({ fullPage: true }), GUIDA, '04-scheda-intera', 480);
      await save(await p.screenshot(), HERO, '01-scheda-telefono', 480);
    });

    await shot('05-punti-ferita', async () => {
      await goHero();
      await p.locator('app-hp-panel [role="button"]').first().click();
      await p.waitForSelector('app-hp-panel input');
      await settle(p);
      await save(await p.screenshot(), GUIDA, '05-punti-ferita', 480);
    });

    await shot('06-tiro-risultato', async () => {
      if (!actors.heroRollFeatureId) throw new Error("l'eroe non ha una capacità che tira");
      await goHero();
      await p.waitForSelector('.badge.text-bg-success');
      await j('POST', `/api/characters/${actors.hero.id}/roll`, { featureId: actors.heroRollFeatureId, spend: false });
      await p.waitForSelector('.display-3', { timeout: 8000 });
      await settle(p);
      await save(await p.screenshot(), GUIDA, '06-tiro-risultato', 480);
    });

    await shot('07-vantaggio', async () => {
      await goHero();
      await p.locator('button', { hasText: /^\s*Vantaggio\s*$/ }).first().click();
      await settle(p);
      await save(await p.screenshot(), GUIDA, '07-vantaggio', 480);
    });

    await shot('09-tab-tratti', async () => {
      await goHero();
      await p.locator('.nav-link', { hasText: 'Tratti' }).click();
      await settle(p);
      await save(await p.screenshot(), GUIDA, '09-tab-tratti', 480);
    });

    await shot('10-tab-zaino', async () => {
      await goHero();
      await p.locator('.nav-link', { hasText: 'Zaino' }).click();
      await settle(p);
      await save(await p.screenshot(), GUIDA, '10-tab-zaino', 480);
    });

    await shot('11-tab-tavolo', async () => {
      await goHero();
      await p.locator('.nav-link', { hasText: 'Tavolo' }).click();
      await p.waitForSelector('app-dice-roller .btn-outline-warning');
      await p.waitForSelector('.badge.text-bg-success');
      await fireHistory(actors, fid);
      await p.waitForSelector('app-tavolo-tab .border-top', { timeout: 8000 });
      await settle(p, 600);
      await save(await p.screenshot(), GUIDA, '11-tab-tavolo', 480);
    });

    await shot('12-menu', async () => {
      await goHero();
      await p.locator('button[aria-label="Altro"]').click();
      await settle(p);
      await save(await p.screenshot(), GUIDA, '12-menu', 480);
    });

    await shot('13-in-combattimento', async () => {
      await goHero();
      await p.locator('button', { hasText: 'In combattimento' }).click();
      await settle(p);
      await save(await p.screenshot(), GUIDA, '13-in-combattimento', 480);
    });

    await shot('14-termina-turno', async () => {
      await goHero();
      await p.locator('button', { hasText: 'In combattimento' }).click();
      await settle(p);
      await save(await p.screenshot({ fullPage: true }), GUIDA, '14-termina-turno', 480);
    });

    await shot('08-incantatrice', async () => {
      await p.goto(`${BASE}/${actors.caster.id}`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=Punti Ferita');
      await settle(p);
      await save(await p.screenshot(), GUIDA, '08-incantatrice', 480);
    });

    await shot('hero-03-tiro-daggerheart', async () => {
      await p.goto(`${BASE}/${actors.caster.id}`, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('.badge.text-bg-success');
      await j('POST', `/api/characters/${actors.caster.id}/roll`, { featureId: fid, spend: false });
      await p.waitForSelector('.display-3', { timeout: 8000 });
      await settle(p);
      const overlay = p.locator('.container > div.text-center').first();
      await save(await overlay.screenshot(), HERO, '03-tiro-daggerheart', 480);
    });

    await c.close();
  }

  // =================== EDITOR SCHEDA (390×844 → 480w) ===================
  {
    const { c, p } = await ctx(390, 844);
    const editHero = `${BASE}/edit/${actors.hero.id}`;

    await shot('15-modifica-scheda', async () => {
      await p.goto(editHero, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=Tratti e Capacità');
      await settle(p, 600);
      await save(await p.screenshot(), GUIDA, '15-modifica-scheda', 480);
    });

    await shot('16-modifica-capacita', async () => {
      await p.goto(editHero, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('app-feature-editor');
      const first = p.locator('app-feature-editor').first();
      await first.locator('button, [role="button"], .card-header, summary').first().click().catch(() => {});
      await settle(p, 600);
      await save(await p.screenshot({ fullPage: true }), GUIDA, '16-modifica-capacita', 480);
    });

    await shot('17-modifica-aggiungi', async () => {
      await p.goto(editHero, { waitUntil: 'domcontentloaded' });
      await p.waitForSelector('text=+ Aggiungi Capacità');
      await p.locator('button', { hasText: '+ Aggiungi Capacità' }).click();
      await p.waitForSelector('text=Salva Capacità');
      await settle(p, 500);
      await save(await p.screenshot({ fullPage: true }), GUIDA, '17-modifica-aggiungi', 480);
    });

    await c.close();
  }
} catch (e) {
  console.log('💥', e.message);
} finally {
  await browser.close();
}

console.log('\n==== ESITO ====');
results.forEach(r => console.log(r));
const failed = results.filter(r => r.startsWith('❌')).length;
console.log(`\n${results.length - failed}/${results.length} ok`);
process.exit(failed === 0 ? 0 : 1);
