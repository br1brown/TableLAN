# Screenshot del README — come rifarli

Il README ha due gruppi di immagini, entrambe catturate guidando il **vero
client Angular** con un browser headless (Playwright/Chromium):

- **`docs/screenshot/`** (qui) — i tre scatti "poster" in cima al README:
  `01-scheda-telefono.png`, `02-console-master.png`, `03-tiro-daggerheart.png`.
- **`docs/guida/`** — il tour illustrato completo della sezione «Uno sguardo a
  cosa fa» (`01`…`26`), una schermata per passo.

Non serve rifarli a mano: lo script [`genera-screenshot.mjs`](genera-screenshot.mjs)
li rigenera **tutti**, con la scena già pronta (mostri, iniziativa, tiri
Duality), mantenendo i nomi dei file — così il README non va toccato.

## Gli attori non sono cablati

Lo script **non sa** che i pregen si chiamano Kael e Lyra: li ricava dallo stato
vivo (`/api/state`), scegliendo un *eroe* e un *incantatore* — dove
"incantatore" è semplicemente chi ha degli slot a livelli. Se domani cambi i
personaggi di test, le schermate si adattano da sole: basta che nel seed restino
almeno due personaggi e uno abbia degli slot. Le uniche cose **fisse** sono di
scena, non del seed — i mostri, i numeri d'iniziativa, il tiro Duality, gli
**stati** messi in iniziativa, i **PF temporanei** dell'eroe e il **giro di round**
che fa segnare «Round 1» — e stanno tutte nel blocco `SCENA` in cima allo script,
un posto solo da toccare.

## La ricetta

1. **Il server**, con un db di scena e la porta da vetrina (l'IP LAN mostrato in
   testata lo decide il server; `5116` è quello che compare negli scatti):

   ```bash
   TABLELAN_PORT=5116 TABLELAN_DB=/tmp/scena.db \
     dotnet run --project src/TableLAN.Server -c Debug -- --headless
   ```

   Meglio un **db fresco**: lo script aggiunge mostri e una capacità, e su un db
   già popolato li duplicherebbe.

2. **Il client** con `ng serve`, che fa da proxy REST/WebSocket al server. Punta
   il proxy alla stessa porta del server (`proxy.conf.json` → `127.0.0.1:5116`):

   ```bash
   cd clients/player && ng serve --port 4200 --proxy-config proxy.conf.json
   ```

3. **Lo script**, che è un pacchetto a sé con le sue due dipendenze
   (`playwright-core` e `sharp`). Il Chromium è già in `/opt/pw-browsers` in molti
   ambienti; altrimenti `npx playwright install chromium`.

   ```bash
   cd docs/screenshot
   npm install
   node genera-screenshot.mjs
   ```

   `BASE`, `API` e `CHROME` sono configurabili via ambiente (default:
   `http://localhost:4200`, `http://127.0.0.1:5116`, il Chromium di
   `/opt/pw-browsers`). Lo script stampa quali attori ha scelto e un ✅/❌ per
   ogni scatto.

## Dettagli utili

Lo script cattura a `deviceScaleFactor: 2` e ridimensiona/ricomprime coi PNG a
palette (`sharp`), per non appesantire il repo. Telefono **390×844** → 480px di
larghezza, console **1280×860** → 1200px. Navigazione con
`waitUntil: 'domcontentloaded'` (non `networkidle`: lo stream realtime non
"quieta" mai). La console è su `/master`, la scheda su `/<guid-personaggio>`,
l'editor su `/edit/<guid>`.

**I tiri arrivano dal vivo** (SignalR non riproduce lo storico): una pagina
appena aperta ha il log vuoto. Lo script li lancia *mentre la pagina è aperta*
(`fireHistory`), con delle POST a `…/roll` e `…/admin/roll`, e solo dopo scatta.
Per questo alcune schermate col log dei tiri cambiano a ogni giro (i dadi sono
casuali): è normale, non è una regressione.
