# Screenshot del README — come rifarli

Gli screenshot in questa cartella sono già nel README (`01-scheda-telefono.png`,
`02-console-master.png`, `03-tiro-daggerheart.png`). Questa è la ricetta per
rigenerarli quando la console o il client cambiano faccia.

Sono stati catturati con un browser headless (Playwright/Chromium) puntato sul
server in locale — così le connessioni realtime non bloccano la cattura e l'output
è un file PNG. In breve:

```bash
# in una cartella qualsiasi
npm i playwright && npx playwright install chromium
```

Server avviato con un db di scena (`TABLELAN_DB=...`), viewport telefono per gli
scatti mobili (390×844) e desktop per la console (1280×860), `deviceScaleFactor: 2`
per il retina. Navigazione con `waitUntil: 'domcontentloaded'` (non `networkidle`,
che con lo stream dei tiri non arriva mai).

## 1 — `01-scheda-telefono.png` · la scheda sul telefono (HERO)

- **Db:** `giocatori-pronti.db` (i 10 pregen). **Personaggio:** *Il Tenebroso
  (Sorcerer 3)* — un incantatore riempie il frame e mostra tutto: PF, l'economia
  del turno (Azione / Bonus / Reazione), gli slot, e la lista di cosa puoi fare,
  ognuna già etichettata col costo e col tiro o il tasto «Usa».
- **Viewport:** 390×844, vista **In combattimento**, scheda **Azioni**.

## 2 — `02-console-master.png` · la console del Master

- **Db:** `giocatori-pronti.db`. Popola prima con qualche **tiro** (così il pannello
  TIRI non è vuoto) e un'**iniziativa**.
- **Viewport:** 1280×860, pagina `/admin/index.html`. Inquadra la griglia «Al
  tavolo», il log dei tiri a destra e l'URL di join in alto.

## 3 — `03-tiro-daggerheart.png` · non solo D&D

- **Db:** un db a parte col **profilo Daggerheart** (riserve Speranza/Paura,
  statistiche es. Agilità/Forza) e un paio di personaggi con una capacità dal tiro
  `duality: 2d12+@Agilità`. Tira una dozzina di volte per avere tutti e tre gli
  esiti nel log.
- **Cattura:** ritaglio del pannello `.rolls` (`locator('.rolls')`), tagliato netto
  alle prime 5 righe così si vedono Critico, Speranza e Paura coi dadi colorati.

## Blocco nel README

Già inserito, subito sotto il sottotitolo. Se rifai gli scatti mantenendo i nomi
file, il README non va toccato.
