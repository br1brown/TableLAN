# TableLAN

**La scheda del personaggio sul telefono, mentre giocate al tavolo.**

Il Master apre un programma sul suo computer. I giocatori inquadrano un quadratino
bianco e nero con la fotocamera del telefono, e si ritrovano la scheda del loro
personaggio sullo schermo: quanti punti ferita ha, cosa può fare in questo turno,
cosa ha già usato. Si tira anche i dadi, se vuole.

Non serve internet. Non serve iscriversi da nessuna parte. Non serve installare niente
sul telefono. Basta che il computer del Master e i telefoni siano attaccati **alla
stessa rete Wi-Fi** — quella di casa va benissimo.

---

## Cosa serve

- Un computer col **Windows** o con **Linux** (Ubuntu, Mint), che è quello del Master.
- Il Wi-Fi di casa. Anche senza internet: basta che sia lo stesso per tutti.
- I telefoni dei giocatori, con un browser qualunque. Vanno bene tutti.

Non c'è la versione per **Mac**: non è una dimenticanza, semplicemente non viene
costruita. Su un Mac oggi TableLAN non parte.

## Scaricarlo

Si scarica sempre da qui, che è l'indirizzo dell'**ultima versione**:

**https://github.com/br1brown/TableLAN/releases/latest**

In quella pagina, in fondo, c'è una sezione **Assets** con dei file da scaricare.
Ne servono uno solo, e quale dipende dal computer:

| Se hai… | scarica il file |
|---|---|
| Windows | `TableLAN-win-x64.zip` |
| Ubuntu, Mint o altro Linux | `TableLAN-linux-x64.zip` |

Sono circa 50 MB. Dentro c'è già tutto: non bisogna installare nient'altro, né
prima né dopo.

### Su Windows

1. Scarica `TableLAN-win-x64.zip`.
2. **Estrailo.** Tasto destro sul file scaricato → *Estrai tutto…* → *Estrai*.
   Questo passaggio non si salta: se apri il programma restando "dentro" lo zip,
   Windows lo esegue in una cartella temporanea e la tua campagna sparisce alla
   chiusura.
3. Nella cartella estratta, doppio clic su **`TableLAN.exe`**.

Si apre una finestra con un drago dorato nell'angolo: è la console del Master.

### Su Linux (Ubuntu, Mint)

Su Linux il doppio clic su un programma scaricato di solito non funziona — è una
misura di sicurezza del sistema, non un difetto di TableLAN. Ci vuole il **Terminale**
(si apre con `Ctrl+Alt+T`). Copia e incolla queste righe, una alla volta, premendo
Invio dopo ognuna:

```bash
cd ~/Scaricati                       # o ~/Downloads, dipende dalla lingua del sistema
unzip TableLAN-linux-x64.zip -d TableLAN
cd TableLAN
chmod +x TableLAN run.sh installa-desktop.sh
./run.sh
```

Il `chmod +x` dice a Linux "questi file si possono eseguire": senza, risponde
*Permission denied*. Va fatto una volta sola.

Da qui in poi, per giocare basta `./run.sh`.

Se lo vuoi anche nel menù delle applicazioni, con la sua icona, lancia **una volta**
`./installa-desktop.sh`: da lì in avanti lo trovi fra i programmi come tutti gli altri
e il terminale non serve più. Non installa niente nel sistema — mette due file nella
tua cartella personale, e ti dice in fondo come toglierli.

## La prima volta: due cose che spaventano, ma sono normali

**Windows dice "Windows ha protetto il PC".**
Succede a qualunque programma scaricato da internet che non abbia pagato una firma
digitale (che costa, ogni anno). Non vuol dire che ci sia un virus: vuol dire che
Windows non conosce chi l'ha scritto. Clicca su **Ulteriori informazioni** e poi su
**Esegui comunque**. Solo la prima volta.

**Il computer chiede se consentire l'accesso alla rete.**
Qui bisogna dire di **sì** — anzi, è il passaggio più importante di tutti. È così che
i telefoni riescono a parlare col computer. Se clicchi *Annulla*, TableLAN si apre
lo stesso ma **i giocatori non riescono a entrare**, e non è ovvio capire perché.
Se ti è già scappato un *Annulla*: cerca "Firewall" nelle impostazioni di Windows,
voce "Consenti app attraverso il firewall", e spunta TableLAN sulla rete **privata**.

## I giocatori: come entrano

Nella finestra del Master c'è un pulsante **Mostra QR Invito**. Cliccalo: compare un
quadrato bianco e nero.

Ogni giocatore apre la **fotocamera** del telefono e la punta sul quadrato — senza
scattare la foto. Dopo un secondo compare una scritta da toccare, e si apre la scheda
nel browser. Fine: niente app da scaricare, niente password.

Se non succede niente, quasi sempre è una di queste due:

- il telefono è sul **4G/5G** invece che sul Wi-Fi di casa;
- il telefono è su una **rete Wi-Fi diversa** da quella del computer (capita nelle case
  con due reti, tipo una "casa" e una "casa-5G": devono stare sulla stessa).

Il quadrato cambia a ogni avvio, quindi non ha senso salvarlo o fotografarlo per la
volta dopo: va riletto ogni sera.

## La campagna è un file

Nella cartella di TableLAN, dopo il primo avvio, compare un file chiamato
**`tablelan.db`**. Quella è la vostra campagna: i personaggi, gli oggetti, tutto.

Siccome è un file normale, si tratta come un file normale:

- **Backup**: copialo da qualche parte. Tutto qui.
- **Archiviare una campagna e cominciarne un'altra**: rinominalo (per esempio
  `vecchia-campagna.db`). Al riavvio ne nasce uno nuovo e vuoto.
- **Riprendere una campagna vecchia**: rimetti il suo file al posto di `tablelan.db`.

TableLAN non sa cosa sia una "campagna" e non ha un menù per gestirle: decidi tu,
spostando i file.

## Aggiornarlo

Torna su
**[github.com/br1brown/TableLAN/releases/latest](https://github.com/br1brown/TableLAN/releases/latest)**,
scarica lo zip nuovo, estrai, e sostituisci il vecchio programma col nuovo.

**Non cancellare `tablelan.db`**: quello è la campagna e va tenuto. Si aggiorna il
programma, non i vostri personaggi.

Per sapere che versione stai usando, guarda in alto a destra nella finestra del
Master: c'è una scritta piccola tipo `v0.3+a1b2c3d`.

---
---

# Per chi mette le mani nel codice

Da qui in giù è roba tecnica: architettura, build, scelte di progetto. Se volevi solo
giocare, hai già finito sopra.

Il progetto implementa il [Documento Master di analisi](docs/PROGETTO-MASTER.md), che
resta il riferimento per le decisioni architetturali (i riferimenti "Capitolo N" nei
commenti del codice puntano lì). Le ricette di gioco — come si tira per colpire, dove
il motore si ferma — stanno in [COME-SI-GIOCA.md](docs/COME-SI-GIOCA.md).

## Riorientamento (feedback del Master)

Dopo il primo scaffold il Master ha chiarito che **non gli serve un VTT condiviso**: il
combattimento e l'iniziativa se li traccia su carta, i mostri li ha già sul suo PC, e
non gli interessa vedere in tempo reale i tiri altrui. Il prodotto è quindi la **scheda
snella e personale dei due giocatori**, più uno strumento con cui il **Master modifica
le loro schede** (mutazioni da contaminazione, oggetti, magie, statistiche) — o con cui
i giocatori se le aggiungono da soli. Di conseguenza:

- **Rimosso**: dashboard di combattimento del Master (il tracker d'iniziativa è poi
  tornato, in forma leggera: vedi più sotto).
- **Aggiunto**: authoring delle schede (Master e giocatore) e il ciclo di ricarica
  **"una volta per turno"** (lo Sneak Attack che Roll20/D&D Beyond gestiscono male).

  E vale la pena dire come: *per turno* significa **per ogni turno**, non per round.
  In un round ci sono N turni, e chi sa agire nel turno altrui — un attacco di
  opportunità, un'azione preparata — la capacità la riusa. Un Ladro può fare **due**
  Attacchi Furtivi per round: uno nel suo turno e uno con la reazione (Crawford:
  «Sneak Attack can occur once per turn, so it can potentially occur more than once in
  a round»). Il motore ricarica il "per turno" a **ogni** turno che comincia, di
  chiunque sia — l'economia del turno no, quella resta del giocatore.
- **Confermato**: la scheda collassabile con descrizioni lazy resta il cuore, e la
  validazione degli slot resta — ma a beneficio del giocatore, non del Master.

Dove batte D&D Beyond, con le parole del Master: vista Azioni **che funziona su
telefono** e mostra **azioni bonus e reazioni** (D&D Beyond le omette e su mobile non
va); "una volta per turno" gestito bene; nessun limite "solo sottoclassi" (l'entità
Fonte non se ne cura); magie senza slot per i non-caster, **senza il feat-hack** —
il costo di una feature è solo un dato.

## Architettura

```
┌─────────────────────────── PC del Master ───────────────────────────┐
│  TableLAN.exe — un solo processo                                    │
│  ├─ finestra nativa (Photino.NET) su http://127.0.0.1:PORT/admin/   │
│  └─ server ASP.NET Core / Kestrel, in-process                       │
│  ├─ /admin, /api/admin/*   → SOLO loopback (LoopbackOnlyMiddleware) │
│  │                           QR, join-info; console (4 pagine);      │
│  │                           bestiario, iniziativa, log dei tiri     │
│  ├─ /            → client Angular dei giocatori (statico)           │
│  ├─ /hub         → SignalR: canale di sola ricezione (server→client)│
│  ├─ /api/state   → snapshot collassato (senza testi esplicativi)    │
│  ├─ /api/descriptions/{id} → testi lazy-loaded on-demand            │
│  ├─ /api/characters/{id}/* → azioni (intent, roll, hp, reset, rest) │
│  │                           e authoring (feature, fonti, stat)      │
│  └─ SQLite       → intento validato scritto su disco PRIMA del sync │
│                                                                     │
│  TableLAN.Core — dominio puro, nessuna dipendenza                   │
│  ├─ GameProfile (meccaniche configurabili: turno, riserve, cicli)   │
│  ├─ Source (entità Fonte), Feature, UsageCounter, ActivationCost    │
│  ├─ Dice (formule `2d6+@Forza`: parse, statistiche, vantaggio)      │
│  └─ RuleEngine + TurnState + ResourceBook (guidati dal profilo)     │
└──────────────────────────────────────────────────────────────────────┘
                       ▲ Wi-Fi (QR con IP corrente)
              telefoni dei giocatori → browser, zero installazione
```

Le idee portanti, in breve:

- **Profilo di sistema** (`TableLAN.Core/Profile`): le meccaniche — economia del turno,
  riserve (slot a livelli o pool a punti), cicli di riposo — non sono cablate ma
  definite da un profilo editabile dal Master. Il default riproduce D&D 5e; il motore
  interpreta il costo di ogni feature tramite il profilo, così adattarlo a un altro
  sistema (senza azioni bonus, con mana/ki, con cicli "a scena") è cambiare il profilo,
  non il codice. Il costo di una feature è un id-stringa, non un enum. Il Master lo
  modifica da una **GUI a form** nella console (menù "Parti da un preset": D&D 5e,
  Pathfinder 2e, Call of Cthulhu, Vampiri, Savage Worlds, Fate); il JSON è un dettaglio
  interno, visibile solo dietro il toggle "JSON avanzato".
- **Entità Fonte** (Capitolo 7): il personaggio è una lista di Fonti attive (classe,
  sottoclasse, razza, oggetto, homebrew…). Il multiclasse non ha codice dedicato: è solo
  il caso in cui il filtro sulle Fonti di tipo "classe" restituisce più di un elemento.
  Una mutazione o un oggetto aggiunti dal Master sono semplicemente nuove Fonti.
- **Deduplicazione + lazy loading** (Capitolo 7): le feature portano un puntatore al
  testo condiviso; lo snapshot di rete non contiene mai le descrizioni, scaricate solo
  all'espansione dell'elemento.
- **Motore come validazione per il giocatore** (Capitolo 8): un intento impossibile
  (incantesimo senza slot, seconda Azione nello stesso turno, feature "una volta per
  turno" già usata) viene respinto dal motore; il rifiuto torna solo a chi ha agito.
- **Authoring come Fonti** (Capitolo 10): il Master aggiunge feature/mutazioni/oggetti/
  magie e statistiche alle schede via REST; ogni modifica è ridiffusa dal vivo ai
  client. Le magie non richiedono che il personaggio sia un caster — il costo è un dato.
- **Il tiro è un dato della feature** (`TableLAN.Core/Dice`): una feature può portare una
  formula (`1d20+@Destrezza`, `3d6`, `1d8+@{Sanità Mentale}`) che referenzia le
  statistiche della scheda con `@Nome`. Le statistiche si risolvono **prima** di tirare,
  così una formula rotta è un rifiuto e non un tiro sbagliato; la stessa formula è
  validata al salvataggio, perché scoprirla al tavolo è tardi. Gli effetti attivi
  entrano nel tiro senza codice dedicato: se un oggetto equipaggiato dà `Forza +2`,
  `@Forza` vale già 2 in più. Vantaggio/svantaggio sono un'opzione del tiro, non due
  formule diverse — il dado scartato resta visibile.
- **Confine di rete, non di UI** (Capitoli 5 e 11): le rotte `/admin` del Master
  rispondono solo su 127.0.0.1; un telefono in LAN riceve 403 a prescindere dall'UI.

## Struttura della repo

| Percorso | Contenuto |
|---|---|
| `src/TableLAN.Core` | Dominio e rule engine, C# puro senza dipendenze |
| `src/TableLAN.Server` | L'app del Master (`TableLAN.exe`): finestra Photino.NET + server Kestrel in-process — SignalR, SQLite (EF Core), QR, API, dashboard `/admin` |
| `clients/player` | Client Angular mobile-first dei giocatori (due rotte: scheda e editor) |
| `tests/TableLAN.Core.Tests` | Test xUnit del motore (validazione, economia del turno, Fonti, dadi) |
| `tests/TableLAN.Server.Tests` | Test xUnit del server (evoluzione dello schema SQLite) |
| `scripts/build-client.sh` | Compila il client Angular e lo pubblica nella `wwwroot` del server |
| `scripts/run.sh` | Lanciatore Linux: finestra nativa se ci sono WebKitGTK e libnotify, altrimenti browser. Viaggia nello zip della release |
| `scripts/installa-desktop.sh` | Mette TableLAN nel menù applicazioni di Ubuntu, sotto `~/.local`. Viaggia nello zip della release |
| `assets/icona/` | L'icona: `.ico` per Windows (exe e finestra), `.png` 256 per Linux (finestra e menù) |
| `docs/PROGETTO-MASTER.md` | Documento master di analisi e decisioni |
| `docs/COME-SI-GIOCA.md` | Le ricette (tiro per colpire, danni, abilità) e **dove il motore si ferma** |

## Requisiti di sviluppo

- .NET SDK 8.0
- Node.js 22 + npm (solo per il client Angular)

## Avvio rapido

Il client Angular dei giocatori **viene compilato automaticamente** quando si compila
il server (target MSBuild in `TableLAN.Server.csproj`): basta aprire la solution e
premere F5, oppure:

```bash
# L'app del Master: apre la finestra nativa sulla console di amministrazione,
# col server LAN avviato in-process. È quello che fa anche il doppio clic
# su TableLAN.exe. Al primo avvio compila anche il client dei giocatori
# e lo pubblica in wwwroot.
dotnet run --project src/TableLAN.Server

# …oppure senza finestra: server da terminale, con QR ASCII e URL di join.
dotnet run --project src/TableLAN.Server -- --headless
```

- Giocatori: scansionano il QR → `http://<ip-lan>:5000/`
- Master: la finestra nativa (o `http://127.0.0.1:5000/admin/` da browser)

La build del client è **incrementale** (rigira solo se cambiano i sorgenti del client)
e **disattivabile** con `dotnet build -p:SkipClientBuild=true` per una build .NET veloce
quando si lavora solo sul backend. Se Node/npm non è installato, il build avvisa e
prosegue: la console del Master funziona comunque, si perde solo la pagina dei giocatori.
In alternativa, `./scripts/build-client.sh` fa la stessa pubblicazione a mano (utile in
CI o per una pubblicazione a pacchetto).

Variabili d'ambiente: `TABLELAN_PORT` (default `5000`), `TABLELAN_DB` (default
`tablelan.db` accanto all'eseguibile).

## Pubblicazione: un file solo

```bash
dotnet publish src/TableLAN.Server -c Release              # Windows
dotnet publish src/TableLAN.Server -c Release -r linux-x64 # Linux
```

Produce **un unico `TableLAN.exe`** (~50 MB): dentro ci sono il runtime .NET, le DLL,
le librerie native (Photino, SQLite) e i file statici, incorporati come risorse
nell'assembly. Chi lo riceve non installa niente — l'unico prerequisito è il runtime
**WebView2**, già presente su Windows 11 e sui Windows 10 aggiornati.

Il csproj inchioda `win-x64`, ma `-r` da riga di comando è una proprietà globale e
vince: da qualsiasi macchina escono entrambi i target, senza toolchain aggiuntivi.

### Release automatiche

`.github/workflows/release.yml` compila i due target e pubblica una release **quando
si spinge un tag** — qualsiasi nome: `v0.3`, `2026-07-17`, quello che vuoi. Non c'è
una regola da ricordare, e il nome del tag è la versione.

Il tag è il cancello: `main` può stare rotto quanto vuole, a chi scarica non arriva
niente finché non taggi di proposito. La release nasce sul tag stesso ed è immutabile:
non si sovrascrive nulla, e il badge "Latest" e l'URL stabile
`/releases/latest/download/TableLAN-win-x64.zip` li gestisce GitHub da solo.

La pagina Releases si tiene corta da sé: dopo ogni pubblicazione il workflow elimina
quelle oltre le ultime cinque. **I tag non si cancellano mai** — sono un puntatore a
un commit e non pesano niente, quindi la storia resta intera: da qualsiasi tag vecchio
si ricostruisce quel pacchetto esatto con un `workflow_dispatch` dal tab Actions,
senza trascinarsi dietro anni di zip.

L'eseguibile sa quale build è: il workflow gli incide dentro `<tag>+<commit>`
(`v0.3+a1b2c3d`), e lo si legge nella testata della console. Un build locale dice
`dev`, che è l'informazione giusta — non è un rilascio.

Node e .NET servono solo al runner: chi scarica lo zip estrae e avvia, punto.

### Linux: la finestra è il browser

Su Windows la finestra nativa funziona perché WebView2 c'è già. Su Linux, Photino si
appoggia a **due** librerie di sistema: non entrano nel single-file e non sono
garantite su una macchina pulita. Perciò lo zip Linux contiene `run.sh`, che guarda
se ci sono — se sì apre la finestra nativa, se no parte `--headless` e apre la console
nel browser di sistema. Nessuna dipendenza, nessuna domanda.

Le librerie sono **WebKitGTK** (ovvia: è il browser dentro la finestra) e
**libnotify** (meno ovvia: `Photino.Native` ci si lega comunque, anche se TableLAN non
manda nessuna notifica, e senza non si carica). Su un Ubuntu desktop normale ci sono
già entrambe. Se vuoi la finestra nativa e ne manca una, `run.sh` te lo dice per nome:

```bash
sudo apt install libwebkit2gtk-4.1-0 libnotify4   # Mint 22 / Ubuntu 24.04
sudo apt install libwebkit2gtk-4.0-37 libnotify4  # Mint 21 / Ubuntu 22.04
```

La voce nel menù applicazioni di Ubuntu la installa `installa-desktop.sh` (nello zip):
scrive un `.desktop` e l'icona sotto `~/.local`, senza sudo. `StartupWMClass=TableLAN`
non è tirato a indovinare — è il `WM_CLASS` che Photino mette davvero sulla finestra,
verificato con `xprop`; senza quella riga GNOME non riconosce la finestra come "questa
app" e nel dock spunta una seconda icona anonima.

### La campagna: perché è un file solo

Come si usa sta [sopra](#la-campagna-è-un-file). Qui il perché: con `TABLELAN_DB` si
punta a un percorso qualsiasi.

Ed è **un file solo davvero**, anche col server acceso: all'avvio la base dati viene
portata a `journal_mode=DELETE`. Il driver SQLite di Microsoft accenderebbe il WAL da
sé, e col WAL la campagna vive in tre file (`.db`, `-wal`, `-shm`) con le scritture
recenti nel `-wal` — copiando il solo `.db` si otterrebbe un backup a cui manca l'ultima
ora di gioco, senza un avviso. Il prezzo (uno scrittore blocca i lettori) a un tavolo da
quattro persone non lo misura nessuno; la promessa qui sopra vale di più.

Il single-file vale **solo in pubblicazione**: `dotnet build` e F5 restano a file
sciolti, come serve mentre si sviluppa. In sviluppo, inoltre, se accanto all'eseguibile
c'è una `wwwroot` vera quella ha la precedenza sulle risorse incorporate: si modifica
la console del Master e basta ricaricare la finestra, senza ricompilare.

Al primo avvio il database viene creato e popolato con i due personaggi dei casi d'uso
target (Capitolo 3): un multiclasse Ladro/Monaco e un'incantatrice pura con slot e una
statistica homebrew ("Sanità Mentale") — utili per i playtest finché non esiste
l'editor di schede.

### Sviluppo del client con hot-reload

```bash
cd clients/player
npm start        # dev server su :4200, proxy /api e /hub verso :5000
```

## Test

```bash
dotnet test
```

I test coprono i punti architetturali chiave: incantesimo senza slot respinto alla
fonte, upcasting, doppia Azione nello stesso turno respinta, feature "una volta per
turno" che si ricarica a ogni nuovo turno, contatori d'uso e cicli di riposo, il
multiclasse come puro filtro sulle Fonti, la deduplicazione del testo condiviso, gli
effetti che modificano davvero i valori della scheda, le formule di dado (parsing,
statistiche risolte prima del tiro, vantaggio/svantaggio) e l'evoluzione dello schema
SQLite su un database già esistente.

Il progetto dei test del server imposta `SkipClientBuild=true`: `dotnet test` non deve
tirarsi dietro una build npm.

## Stato rispetto alla roadmap MVP (Capitolo 13)

1. ✅ **Modello dati collassabile e deduplicazione** — entità Fonte, puntatore di
   deduplicazione, descrizioni lazy-loaded.
2. ✅ **Server locale, bootstrap e QR** — Kestrel in-process, QR rigenerato a ogni
   avvio con l'IP corrente, rotte admin solo-loopback.
3. ✅ **Economia del turno** — FSM del turno per il giocatore, validazione degli
   intenti (incluso "una volta per turno"), persistenza dell'intento prima del sync.
4. ✅ **Gestione fuori combattimento e inventario** — risorse persistite tra le
   sessioni; PF/riposi/reset-turno gestiti dal giocatore; **inventario ordinabile**
   (quantità, riordino, note) nella scheda.
5. ✅ **Authoring / homebrew** — il Master (o il giocatore) aggiunge feature, Fonti,
   mutazioni, oggetti, magie e statistiche via console/scheda; colonne JSON per gli
   attributi custom, disegnati dinamicamente dal client. Le feature possono portare
   **effetti** (modificatori come `Forza +2`, `CA 15`, `PF max +5`) che il motore
   applica ai valori della scheda finché sono attive; gli **oggetti indossabili** hanno
   un interruttore on/off (equipaggia/rimuovi) che accende/spegne i loro effetti. Una
   feature può inoltre portare una **formula di tiro** (`1d20+@Destrezza`), validata al
   salvataggio.

Extra oltre la roadmap:

- **Tiri di dado** — una feature può portare una formula che referenzia le statistiche
  della scheda; il giocatore tira dalla scheda, con vantaggio/svantaggio. L'esito è un
  evento realtime (`rollMade`) e finisce nel **log dei tiri** della console del Master,
  che lo segue via SSE. Il log vive **solo in memoria** (ring buffer di 50): è il
  brusio del tavolo, non un dato della campagna — a fine sessione non serve più.
- **Bestiario del Master** — una lista di mostri (PF, CA, blocco statistico) con traccia
  PF facoltativa. Vive solo su loopback: i giocatori non lo vedono, coerente con il
  fatto che il Master traccia il combattimento per conto suo.
- **Tracker d'iniziativa** — una lista sola, **due punti d'ingresso**: i giocatori dal
  tavolo, le bestie dal bestiario. Vengono da posti diversi e si aggiungono in momenti
  diversi, ma nel giro sono mescolati, perché è lì che il combattimento li mescola.
  Ognuno porta il suo numero e **l'ordine lo decide il server**: "chi ha tirato più alto
  va prima" è una regola, non un'opinione del client, e due console aperte non possono
  mostrare due ordini diversi. Chi entra in turno si ricarica le risorse — ma solo i PG,
  perché i mostri una scheda nel motore non ce l'hanno. Niente round e niente entità
  aggregate: quelli restano sul foglio del Master. Come il bestiario, è opzionale e solo
  loopback.

> **Nota sul riorientamento**: lo strumento non è un VTT. Il canale realtime resta di
> sola ricezione: ogni mutazione entra via REST ed esce via broadcast, l'hub non ha
> metodi invocabili dal client — vale anche per i tiri di dado, che sono una POST come
> le altre. Il combattimento resta gestibile su carta, e gli strumenti che la console
> offre (bestiario, iniziativa, log dei tiri) sono aiuti che il Master può ignorare.

## Cambiare sistema di gioco (profilo)

Il Master modifica le meccaniche dalla GUI del profilo nella console. Ogni meccanica
(risorsa di turno, riserva, ciclo) ha un **codice interno stabile**, assegnato alla
creazione e mai mostrato: è la chiave con cui le feature agganciano la meccanica.
Rinominare l'etichetta ("Azione" → "Azione Principale") non cambia il codice, quindi non
orfana nulla; il Master lavora solo con i nomi. Il codice affiora solo nel toggle
"JSON avanzato", per chi vuole sbirciare.

C'è un unico caso che nessuna UI può nascondere, perché è **semantico e non di
plumbing**: se cambi sistema mentre i personaggi hanno già feature legate a una
meccanica che il nuovo sistema non prevede — per esempio una feature con costo
"Azione Bonus" quando passi a un sistema senza azioni bonus — quella feature non trova
più il suo costo nel profilo. Il comportamento è dichiarato, non un errore:

- sulla scheda del giocatore la feature finisce nella sezione **"Altro"**, in grigio e
  non attivabile;
- nella console del Master la stessa feature è evidenziata (costo in rosso con ⚠) e
  porta un menù **"rimappa a…"**: un click la riassegna a una meccanica esistente del
  profilo, senza rimuoverla e riaggiungerla. In alternativa la si può rimuovere.

In pratica: cambiare sistema *a campagna in corso* richiede di rimappare le feature
esistenti, ma è un menù a tendina per feature, non un lavoro manuale. Partire da un
preset e costruirci sopra da zero, invece, non ha nemmeno questo, perché le feature
nascono già agganciate alle meccaniche di quel profilo.
