# TableLAN

**La scheda del personaggio sul telefono, mentre giocate al tavolo. Di qualunque gioco si tratti.**

<p align="center">
  <img src="docs/screenshot/01-scheda-telefono.png" alt="La scheda di un personaggio sul telefono, in combattimento" width="260">
  &nbsp;&nbsp;
  <img src="docs/screenshot/02-console-master.png" alt="La console del Master sul computer" width="600">
</p>

<p align="center">
  <img src="docs/screenshot/03-tiro-daggerheart.png" alt="Tiri Duality di Daggerheart con gli esiti Speranza, Paura e Critico" width="600">
</p>

<p align="center"><em>A sinistra il telefono del giocatore, a destra il computer del Master. Sotto, un tiro Duality di Daggerheart — perché no, <strong>non è solo D&amp;D</strong>.</em></p>

<p align="center">
  <strong>Scarica</strong>:
  <a href="https://github.com/br1brown/TableLAN/releases/latest/download/TableLAN-win-x64.zip">Windows</a>
  ·
  <a href="https://github.com/br1brown/TableLAN/releases/latest/download/TableLAN-linux-x64.zip">Linux</a>
  — un file solo, niente da installare per i giocatori.
</p>

<p align="center"><sub><a href="https://github.com/br1brown/TableLAN/releases/latest">Tutte le versioni</a> · se invece sviluppi, la roba tecnica è <a href="#per-chi-mette-le-mani-nel-codice">più sotto</a>.</sub></p>

---

## In una riga

Il Master apre un programma sul suo computer. I giocatori inquadrano un quadratino bianco e nero, e si ritrovano la scheda del loro personaggio sul telefono: quanti punti ferita ha, cosa può fare in questo turno, cosa ha già speso. Tira anche i dadi, se vuole.

Niente internet. Niente iscrizioni. Niente da installare sul telefono. Basta che il computer del Master e i telefoni siano sulla **stessa Wi-Fi** — quella di casa va benissimo.

E poi c'è la cosa grossa, quella per cui vale la pena leggere oltre:

> **Le regole non sono cablate.** Sotto non c'è "D&D con lo skin cambiato". C'è un motore che non sa cosa sia una Classe, un Attacco Furtivo o uno Slot Incantesimo, e legge un **profilo** editabile dalla console. Cambi profilo, cambi gioco. Sette sono già pronti, e li vedi tutti qui sotto — con le loro schede vere, i loro dadi veri.

---

## Una serata, dal telefono del giocatore

### Si entra inquadrando un quadrato

Il Master mostra il QR; il giocatore lo punta con la fotocamera, sceglie chi è, ed è dentro. Nessuna app da scaricare, nessuna password da recuperare via mail.

<p align="center">
  <img src="docs/guida/02-qr-invito.png" alt="Il QR d'invito" width="360">
  <img src="docs/guida/03-chi-sei.png" alt="La scelta del personaggio" width="200">
</p>

### Tutta la scheda in una schermata

In cima i punti ferita — un tocco per danno o cura, con un cuscinetto di **PF temporanei** che il danno consuma per primo. Sotto, gli stati che ti hanno messo addosso, l'economia del turno, e la lista di cosa puoi fare: ognuna col costo e col tasto per usarla o per tirare.

<p align="center">
  <img src="docs/guida/04-scheda-intera.png" alt="La scheda intera" width="230">
  <img src="docs/guida/05-punti-ferita.png" alt="Danno, cura e PF temporanei" width="230">
</p>

### I dadi, a portata di pollice

Un tocco per il tiro; vantaggio e svantaggio senza dover cercare un menù. Il **vassoio** porta i dadi del sistema (il d20, la Sfida, quel che serve), e un campo per la formula che nessun bottone prevede — con un **`?`** che ricorda cosa sa scrivere: esplosivi, «tieni i migliori», pool a successi, Duality.

<p align="center">
  <img src="docs/guida/06-tiro-risultato.png" alt="L'esito di un tiro" width="230">
  <img src="docs/guida/07-vantaggio.png" alt="Vantaggio con un tocco" width="230">
</p>

### Le linguette: cosa hai, cosa porti, chi tira

Azioni, Tratti, lo Zaino ordinabile, e il **Tavolo** coi tiri di tutti in tempo reale. Un incantatore vede le sue riserve; chi non lo è, non ha slot fantasma tra i piedi.

<p align="center">
  <img src="docs/guida/08-incantatrice.png" alt="Le riserve di un'incantatrice" width="185">
  <img src="docs/guida/09-tab-tratti.png" alt="I tratti" width="185">
  <img src="docs/guida/10-tab-zaino.png" alt="Lo zaino" width="185">
  <img src="docs/guida/11-tab-tavolo.png" alt="Il tavolo coi tiri di tutti" width="185">
</p>

### La modalità combattimento

Lascia solo ciò che puoi fare adesso, avvisa quando **tocca a te**, e in fondo mette un tasto grande per chiudere il turno. Niente da scrollare per trovare l'azione giusta mentre gli altri aspettano.

<p align="center">
  <img src="docs/guida/13-in-combattimento.png" alt="In combattimento" width="230">
  <img src="docs/guida/14-termina-turno.png" alt="Termina il turno" width="230">
</p>

---

## Dall'altra parte: la console del Master

### L'iniziativa la ordina il server, non l'opinione

Una lista sola, giocatori e mostri mescolati — perché è lì che il combattimento li mescola. Il **round** si conta da sé, e su ogni voce si appuntano gli **stati** (col conto alla rovescia che cala). Due console aperte non possono mostrare due ordini diversi.

<p align="center">
  <img src="docs/guida/01-console-master.png" alt="La console del Master" width="720">
</p>
<p align="center">
  <img src="docs/guida/18-iniziativa.png" alt="L'iniziativa con round e stati" width="440">
  <img src="docs/guida/19-tiri.png" alt="Il log dei tiri" width="270">
</p>

### Le schede e il bestiario

Le schede si aprono tutte nello stesso editor. Il bestiario è solo del Master — e un mostro si **sdoppia in istanze** con un tocco: tre goblin, tre barre di PF separate, senza rinominarli a mano.

<p align="center">
  <img src="docs/guida/20-schede.png" alt="Le schede al tavolo" width="720">
</p>
<p align="center">
  <img src="docs/guida/21-bestiario.png" alt="Il bestiario" width="360">
  <img src="docs/guida/22-bestiario-aggiungi.png" alt="Aggiungere un mostro" width="360">
</p>

### Si sistema tutto da qui

Da *Modifica* si cambiano nome, PF, statistiche, riserve, e si regola ogni capacità: costo, usi, tiro, effetti. Una magia non richiede di essere un mago — il costo è solo un dato — e una scheda di D&D Beyond si importa dal suo PDF.

<p align="center">
  <img src="docs/guida/15-modifica-scheda.png" alt="Modifica scheda" width="230">
  <img src="docs/guida/16-modifica-capacita.png" alt="Modifica di una capacità" width="230">
</p>

---

## Sette giochi, un motore solo

Qui sta il grosso potenziale, ed è meglio mostrarlo che raccontarlo. **Lo stesso identico programma**, cambiando un profilo dalla console, diventa sette giochi diversi — coi loro dadi, le loro riserve, i loro stati, la loro *matematica*. Ogni coppia qui sotto è: com'è configurato il sistema (a sinistra) e come tira davvero al tavolo (a destra). Nessun fotomontaggio — sono scatti veri, generati guidando il vero client.

### D&D 5e — il d20 contro la CD, e il 4d6 che scarta il più basso

Le sei caratteristiche, la CA che si guarda e non si tira, la Competenza che scrivi tu salendo di livello. E `4d6kh3` — la generazione delle caratteristiche — in un bottone: quattro d6, il più basso sbarrato.

<p align="center">
  <img src="docs/guida/sys-dnd-sistema.png" alt="D&D 5e: il profilo" width="400">
  <img src="docs/guida/sys-dnd-tiri.png" alt="D&D 5e: i tiri" width="280">
</p>

### Daggerheart — i Duality Dice

Due d12, Speranza e Paura: conta il totale, ma conta anche **quale dei due vince** (Speranza, Paura, o Critico se pari). Niente slot: le riserve sono **Speranza** e **Stress**, i due contatori attorno a cui gira la fiction.

<p align="center">
  <img src="docs/guida/sys-daggerheart-sistema.png" alt="Daggerheart: il profilo" width="400">
  <img src="docs/guida/sys-daggerheart-tiri.png" alt="Daggerheart: i tiri Duality" width="280">
</p>

### Pathfinder 2e — tre azioni e il maluscolo

L'economia a **tre azioni** per turno, la competenza come punteggio che cresce di grado, e gli attacchi multipli con la penalità (−5, −10) che il giocatore si scrive nella formula.

<p align="center">
  <img src="docs/guida/sys-pf2e-sistema.png" alt="Pathfinder 2e: il profilo" width="400">
  <img src="docs/guida/sys-pf2e-tiri.png" alt="Pathfinder 2e: i tiri" width="280">
</p>

### Call of Cthulhu — il d100 e la Sanità che si sgretola

Il **percentile** in testa al vassoio, i Punti Magia come riserva, e gli stati che qui sono soprattutto mentali: pazzia temporanea, attacco di follia. (Il confronto "sotto il punteggio" lo giudica il tavolo — vedi *Cosa manca*.)

<p align="center">
  <img src="docs/guida/sys-coc-sistema.png" alt="Call of Cthulhu: il profilo" width="400">
  <img src="docs/guida/sys-coc-tiri.png" alt="Call of Cthulhu: i tiri d100" width="280">
</p>

### Vampiri / Mondo di Tenebra — i pool a successi

Tanti d10 quanto vale il punteggio, e **si contano** le facce da 6 in su — non si sommano (il totale è 4 successi, non 40). Il Sangue si spende, l'Umanità si tira, la Forza di Volontà fa entrambe.

<p align="center">
  <img src="docs/guida/sys-wod-sistema.png" alt="Vampiri: il profilo" width="400">
  <img src="docs/guida/sys-wod-tiri.png" alt="Vampiri: i pool a successi" width="280">
</p>

### Savage Worlds — i dadi che esplodono

I dadi di Tratto dal d4 al d12, e l'**Ace**: un dado che cade sul massimo si ri-tira e si somma (`1d4! → d4:4 d4:2 = 6`). I Bennies che si azzerano a fine sessione, i Punti Potere che no.

<p align="center">
  <img src="docs/guida/sys-savage-sistema.png" alt="Savage Worlds: il profilo" width="400">
  <img src="docs/guida/sys-savage-tiri.png" alt="Savage Worlds: i dadi esplosivi" width="280">
</p>

### Fate — i dadi Fudge

Quattro dadi da **−1 / 0 / +1**, sommati (totale da −4 a +4, più l'abilità). Si mostrano come segno, non come numero: `dF:＋ dF:0 dF:− dF:＋`.

<p align="center">
  <img src="docs/guida/sys-fate-sistema.png" alt="Fate: il profilo" width="400">
  <img src="docs/guida/sys-fate-tiri.png" alt="Fate: i dadi Fudge" width="280">
</p>

### E se il tuo gioco non è tra questi?

Parti dal preset più vicino e ritoccalo dalla console: statistiche, dadi, riserve, cicli di riposo, stati. Ogni partita è un file, e si cambia campagna **a caldo**, senza riavviare. Il JSON c'è — ma dietro un interruttore, per chi vuole sbirciare.

<p align="center">
  <img src="docs/guida/23-sistema.png" alt="L'editor di sistema" width="320">
  <img src="docs/guida/25-sistema-preset.png" alt="Partire da un preset" width="320">
  <img src="docs/guida/24-campagna.png" alt="Le campagne" width="320">
</p>

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

## Le campagne sono file

Una campagna è un file `.db` nella cartella di TableLAN. Dentro c'è tutto:
personaggi, oggetti, regole. Al primo avvio ne nasce uno, `tablelan.db`.

Puoi averne quante vuoi, coi nomi che vuoi. In **Sistema → Campagna** ne crei di
nuove e passi dall'una all'altra senza chiudere il programma. Stanno tutte nella
stessa cartella, una per partita.

Quando riapri il programma, riparte dall'ultima campagna che avevi aperto, non da
`tablelan.db`. Lo sa perché accanto ai `.db` c'è un file `.tablelan-campagna` con
scritto il nome dell'ultima: un segnalibro, niente più. Lo cancelli e riparti da
`tablelan.db`.

Nella cartella trovi quindi:

| File | Cos'è |
|---|---|
| `tablelan.db`, `drakkenheim.db`, … | le campagne (i dati) |
| `.tablelan-campagna` | il nome dell'ultima aperta (un segnalibro) |

Sono file normali: per il **backup** copi il `.db`; per **archiviare** lo sposti o
lo rinomini; per **riprendere** una vecchia campagna la rimetti nella cartella e
la apri da *Sistema → Campagna*. Se sposti quella aperta per ultima, alla
riapertura torni a `tablelan.db`.

## Aggiornarlo

Torna su
**[github.com/br1brown/TableLAN/releases/latest](https://github.com/br1brown/TableLAN/releases/latest)**,
scarica lo zip nuovo, estrai, e sostituisci il vecchio programma col nuovo.

**Non cancellare `tablelan.db`**: quello è la campagna e va tenuto. Si aggiorna il
programma, non i vostri personaggi.

Per sapere che versione stai usando, guarda in alto a destra nella finestra del
Master: c'è una scritta piccola tipo `v0.3+a1b2c3d`. Se è uscita una versione più
recente, lì accanto compare da sé un avviso: il programma confronta la versione in
uso con l'ultima release su GitHub (una volta ogni tanto, e senza far niente se
sei offline), così non devi controllare a mano.

---

## Cosa manca — le cose oneste

Un tavolo serio merita di sapere anche cosa **non** trova qui. Due liste, perché sono due cose diverse: quello che manca e prima o poi si aggiunge, e quello che manca *per scelta* — perché TableLAN vuole essere una cosa fatta bene, non tutte fatte a metà.

### Non c'è (ancora)

- **Il Mac.** Non è una dimenticanza: la versione per macOS semplicemente non viene costruita. Su un Mac, oggi, TableLAN non parte.
- **Il "sotto il punteggio" di Call of Cthulhu.** Il d100 lo tira; ma dire *riuscito/fallito* confrontando il tiro con l'abilità, quello no — mostra il numero e l'esito lo giudica il tavolo, come per ogni altro tiro. È l'unico dado, dei sette sistemi, che il motore non risolve fino in fondo.
- **L'auto-tiro dell'iniziativa.** I numeri li scrivi tu; il server li ordina. Il motore non tira l'iniziativa al posto tuo.
- **L'import di personaggi** oltre al PDF di D&D Beyond. Per gli altri sistemi la scheda si costruisce a mano dall'editor — una volta, poi resta.

### Non c'è per scelta (ed è meglio così)

- **Non è un VTT.** Niente mappa condivisa, niente pedine, niente nebbia di guerra, niente linee di vista. Il combattimento tattico resta sul tavolo o sul foglio del Master; TableLAN gli dà una mano (iniziativa, bestiario, log dei tiri), non lo sostituisce.
- **Non giudica le regole al posto vostro.** Mostra il totale, non "hai colpito". La CD, il successo, la conseguenza: le decide chi gioca. È un aiuto-memoria e un calcolatore di dadi onesto, non un arbitro.
- **Niente cloud, niente account, niente gioco a distanza.** Vive sulla Wi-Fi di casa, e basta. È il prezzo — e insieme il punto — del "niente iscrizioni, niente internet": funziona in cantina come in salotto, ma i giocatori devono essere nella stessa stanza (o quasi).
- **Un tavolo alla volta.** Una console del Master, una partita. Non è pensato per tre gruppi in parallelo.

Se cerchi Roll20 o Foundry, TableLAN non è quello — e va bene. Se cerchi *la scheda giusta sul telefono giusto, senza cerimonie, per il gioco che ti pare*, sei nel posto giusto.
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
│  ├─ finestra nativa (Photino.NET) su http://127.0.0.1:PORT/master   │
│  └─ server ASP.NET Core / Kestrel, in-process                       │
│  ├─ /            → una sola app Angular: giocatori e console Master │
│  │                 (rotta /master). L'UI è generica; il muro sono   │
│  │                 le API qui sotto.                                 │
│  ├─ /api/admin/* → SOLO loopback (LoopbackOnlyMiddleware): QR,       │
│  │                 join-info, bestiario, iniziativa, profilo, import │
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
  Daggerheart, Pathfinder 2e, Call of Cthulhu, Vampiri, Savage Worlds, Fate); il JSON è
  un dettaglio interno, visibile solo dietro il toggle "JSON avanzato".
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
- **Confine di rete, non di UI** (Capitoli 5 e 11): master e giocatori sono la
  stessa app Angular, e l'interfaccia del Master (`/master`) è servita in modo
  generico. A rispondere solo su 127.0.0.1 sono le **API** `/api/admin/*`: un
  telefono in LAN può caricare la console, ma senza quelle API resta vuota — il
  muro è di rete, non di pagina.

## Struttura della repo

| Percorso | Contenuto |
|---|---|
| `src/TableLAN.Core` | Dominio e rule engine, C# puro senza dipendenze |
| `src/TableLAN.Server` | L'app del Master (`TableLAN.exe`): finestra Photino.NET + server Kestrel in-process — SignalR, SQLite (EF Core), QR, API. Le API `/api/admin/*` rispondono solo su loopback |
| `clients/player` | L'app Angular (Bootstrap): scheda del giocatore su `/:playerGuid`, editor, e la console del Master su `/master` — una sola build |
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
- Master: la finestra nativa (o `http://127.0.0.1:5000/master` da browser)

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

Come si usa sta [sopra](#le-campagne-sono-file). Qui il perché: con `TABLELAN_DB` si
punta a un percorso qualsiasi.

Ed è **un file solo davvero**, anche col server acceso: all'avvio la base dati viene
portata a `journal_mode=DELETE`. Il driver SQLite di Microsoft accenderebbe il WAL da
sé, e col WAL la campagna vive in tre file (`.db`, `-wal`, `-shm`) con le scritture
recenti nel `-wal` — copiando il solo `.db` si otterrebbe un backup a cui manca l'ultima
ora di gioco, senza un avviso. Il prezzo (uno scrittore blocca i lettori) a un tavolo da
quattro persone non lo misura nessuno; la promessa qui sopra vale di più.

L'unica cosa che TableLAN scrive fuori dal `.db` è il segnalibro `.tablelan-campagna`:
non sono dati di gioco, è solo il nome dell'ultima campagna aperta, per riaprirla al
prossimo avvio (`CampaignService` lo scrive allo switch, `ResolveDbPath` lo rilegge —
sotto `TABLELAN_DB`, che se impostata vince). Cancellarlo non perde niente: si riparte
da `tablelan.db`.

Il single-file vale **solo in pubblicazione**: `dotnet build` e F5 restano a file
sciolti, come serve mentre si sviluppa. In sviluppo, inoltre, se accanto all'eseguibile
c'è una `wwwroot` vera quella ha la precedenza sulle risorse incorporate: il build del
client la ripopola, così una ricompilazione del client si riflette nella finestra senza
ricostruire l'exe. Per il ciclo rapido sull'UI (master e giocatori) c'è comunque
`npm start` col dev server, più sotto.

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
statistiche risolte prima del tiro, vantaggio/svantaggio, dadi esplosivi, «tieni i
migliori/peggiori N», pool a successi, la coppia Duality), il vassoio e gli stati di
ogni preset e l'evoluzione dello schema SQLite su un database già esistente.

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
  che lo segue via SSE. Ogni riga del log ha un **ri-tira** (↻): stessa formula, sotto
  lo stesso nome — un tiro del Master torna dal Master, quello di un giocatore si
  rilancia come suo. Il log vive **solo in memoria** (ring buffer di 50): è il brusio
  del tavolo, non un dato della campagna — a fine sessione non serve più.
- **Vassoio dei dadi** — i tiri rapidi «sul tavolo», fuori da ogni feature: un bottone
  per dado (con vantaggio/svantaggio), più un campo per la formula libera che nessun
  bottone copre. **Quali dadi ci sono lo dice il sistema** — è una lista nel profilo
  (`GameProfile.Dice`), configurabile dalla Sistema come statistiche e riserve, uguale
  per tutti i giocatori e diversa da gioco a gioco (il d100 di Call of Cthulhu, i dadi
  di Tratto di Savage Worlds, la coppia Duality di Daggerheart); vuota, il client ricade
  sui poliedrici standard. La formula libera capisce più di quanto un bottone lasci
  vedere — dadi **esplosivi** (`1d8!`, l'Ace di Savage), **«tieni i migliori/peggiori
  N»** (`4d6kh3`, la generazione delle caratteristiche di D&D), pool a successi
  (`@Destrezza d10 >= 6`), la coppia Duality — e un **`?`** accanto a «Tira» ne mostra
  esempi tappabili, così non restano poteri invisibili. Il giocatore tira dalla linguetta
  Tavolo, sotto il suo nome e con le sue statistiche (`1d20+@Forza` vale anche qui); il
  Master dalla console (`POST /api/admin/roll`, loopback), sotto il nome «Master».
  Stesso motore, stesso log, stesso `rollMade`: cambia solo chi ci mette il nome.
- **Bestiario del Master** — una lista di mostri (PF, CA, blocco statistico) con traccia
  PF facoltativa. Un mostro si **sdoppia in istanze** con un tocco (⧉): tre goblin, tre
  barre di PF separate, senza rinominarli a mano. Vive solo su loopback: i giocatori non
  lo vedono, coerente con il fatto che il Master traccia il combattimento per conto suo.
- **Tracker d'iniziativa** — una lista sola, **due punti d'ingresso**: i giocatori dal
  tavolo, le bestie dal bestiario. Vengono da posti diversi e si aggiungono in momenti
  diversi, ma nel giro sono mescolati, perché è lì che il combattimento li mescola.
  Ognuno porta il suo numero e **l'ordine lo decide il server**: "chi ha tirato più alto
  va prima" è una regola, non un'opinione del client, e due console aperte non possono
  mostrare due ordini diversi. Chi entra in turno si ricarica le risorse — ma solo i PG,
  perché i mostri una scheda nel motore non ce l'hanno. Un **contatore di round** avanza
  da sé quando il giro torna al primo, e su ogni voce si possono appuntare **stati** —
  dal vocabolario del sistema (Avvelenato, Scosso, Vulnerabile…) o scritti a mano, con
  un conto alla rovescia di round che cala da solo. Restano aiuti opzionali e solo
  loopback: il grosso del combattimento resta gestibile sul foglio del Master.
- **PF temporanei** — un cuscinetto anti-danno sopra i PF (l'*Aiuto* di D&D, i PF
  temporanei di mille altri sistemi): il danno lo consuma prima di intaccare i PF veri,
  la cura non lo tocca. Il giocatore se li imposta dal pannello PF; al tavolo del Master
  compaiono come `+N` accanto alla scheda.

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
