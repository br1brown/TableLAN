# RPG LAN Companion — Documento Master di Progetto

> Analisi, decisioni, architettura. Documento di riferimento unico: consolida analisi
> originale, ricerca di mercato e decisioni architetturali emerse. Pensato per essere
> ripreso a distanza di mesi, o passato a un altro sviluppatore, senza perdere contesto.
>
> *Conversione in markdown del documento `RPG_LAN_Companion_MASTER.docx`.*

## 0. Executive Summary

**Problema**: chi gioca di ruolo al tavolo fisico non ha uno strumento che sia insieme
locale, istantaneo e intelligente. Le piattaforme cloud (Roll20, D&D Beyond, Foundry)
e gli overlay leggeri (Owlbear Rodeo) risolvono pezzi del problema, mai tutti insieme.

**Soluzione**: un server locale che il master avvia sul proprio PC; i giocatori si
uniscono scansionando un QR rigenerato a ogni avvio; un motore valida ogni intento di
gioco come fonte di verità; la scheda si legge per livelli collassabili, non come
elenco integrale.

### I cinque motivi che giustificano il progetto

1. **Sovraccarico cognitivo**: le schede digitali attuali mostrano sempre tutto il testo;
   un personaggio multiclasse accumula 40+ tra abilità e incantesimi, illeggibili in
   modalità espansa.
2. **Dipendenza dal cloud**: account, login, connessione stabile; al tavolo ogni
   dipendenza esterna è un punto di rottura.
3. **Setup macchinoso**: Foundry VTT è generalista e potente, ma richiede porte, inviti,
   configurazioni; il gruppo vuole giocare, non amministrare un server.
4. **Esperienza frammentata**: un tool per il master offline, un altro per il giocatore
   online; mai un motore condiviso in tempo reale sulla stessa rete.
5. **Nessuna validazione reale**: le piattaforme esistenti si fidano dell'utente; un
   intento impossibile (incantesimo senza slot) arriva comunque al tavolo.

**Cosa rende il progetto difendibile**: non compete sulle funzionalità, che esistono già
sparse tra decine di strumenti; compete sulla combinazione — nessun concorrente unisce
assenza di cloud, join istantaneo, validazione attiva e modello dati agnostico nello
stesso motore. Vedi Capitolo 12 per il dettaglio della ricerca comparativa.

## 1. Perché questo progetto: analisi del problema e panorama attuale

La ricerca di mercato conferma che, ad oggi, non esiste uno strumento che soddisfi
contemporaneamente i cinque requisiti fondamentali del progetto: un server locale senza
account né cloud, un sistema di join istantaneo via QR code, la sincronizzazione
realtime bidirezionale, un motore di validazione attiva delle regole e un supporto
homebrew dinamico.

### Criticità dell'ecosistema attuale

- **Sovraccarico cognitivo**: piattaforme proprietarie come D&D Beyond mancano di
  personalizzazione e mostrano costantemente l'intero testo esplicativo delle abilità,
  duplicandolo tra livelli o classi, senza offrire una gerarchia a scomparsa
  (Progressive Disclosure). Questo rende la lettura a schermo, specialmente su mobile,
  caotica durante le sessioni.
- **Dipendenza dall'infrastruttura esterna**: applicazioni come Owlbear Rodeo o
  Tableward sono cloud-based, impongono la creazione di account e richiedono una
  connessione internet attiva. Questo approccio si scontra con la filosofia
  "local first" e introduce rischi di latenza o disconnessione al tavolo.
- **Complessità di setup**: Foundry Virtual Tabletop è uno strumento generalista
  estremamente potente, ma il suo accesso al server risulta macchinoso, richiede codici
  di invito, tempi di attesa lunghi e, spesso, configurazioni complesse di
  port-forwarding sui router domestici.
- **Frammentazione dell'esperienza**: tool come D&D Battle Tracker, Questweaver o
  DM Companion risolvono solo metà dell'equazione, supportando unicamente il master
  offline o il giocatore online. Manca un motore condiviso in tempo reale sulla stessa LAN.

## 2. Priorità del progetto: UX guidata dalle esigenze del Master

Il vero collo di bottiglia informatico al tavolo da gioco non è la mancanza di
funzionalità, ma l'eccesso di informazioni sempre visibili. Un personaggio multiclasse
di medio-alto livello accumula facilmente oltre 40 tra abilità e incantesimi. Mostrare
tutte queste informazioni in modalità espansa rende la scheda digitalizzata di fatto
inutilizzabile durante l'azione.

**Requisito guida**: la deduplicazione dei dati e un'interfaccia utente collassabile
hanno la precedenza assoluta su funzioni avanzate come la gestione homebrew o
l'inventario ordinabile. Il design dovrà essere Mobile-First per i giocatori, garantendo
un'accessibilità immediata alle "Azioni", "Azioni Bonus" e "Reazioni" per ridurre i
tempi morti del turno.

## 3. Vincoli di progetto e Timeline

- **Risorse umane**: lo sviluppo è interamente a carico di una sola persona. Gli utenti
  finali non sono contributori tecnici, il che impone una UI autoesplicativa e priva di
  configurazioni sistemistiche per l'utente finale.
- **Casi d'uso target**: il sistema deve supportare fin da subito i profili più
  complessi: il personaggio multiclasse e una classe incantatore puro con lista di
  incantesimi dedicata. Il modello dati non può focalizzarsi solo sui tratti marziali o
  magici, ma deve coprirli entrambi.
- **Roadmap**: la timeline prevede una prima versione (MVP) utilizzabile
  orientativamente entro settembre. Questa data orienta lo sviluppo strettamente verso
  le funzionalità Core (Capitolo 13), posticipando l'abbellimento estetico.

## 4. Architettura Local-First: scelta, pro e contro

Si è scartata l'ipotesi di un'applicazione web esposta su internet (che avrebbe
richiesto gestione di autenticazione, crittografia avanzata e server in hosting) a
favore di un applicativo locale. Il programma espone una pagina web, basata sui dati del
Master, accessibile solo quando in esecuzione e solo dai dispositivi connessi alla
stessa rete Wi-Fi. Questa scelta garantisce sicurezza passiva (isolamento di rete) ed
elimina i costi di hosting.

### Pro

- **Sicurezza passiva**: nessun dato lascia la rete locale; l'isolamento di rete
  sostituisce autenticazione e crittografia, azzerando una superficie di attacco intera.
- **Zero costi ricorrenti**: nessun hosting, nessun abbonamento, nessuna dipendenza da
  un servizio terzo che può cambiare pricing o chiudere.
- **Zero configurazione di rete**: niente port-forwarding, niente inviti con tempi di
  attesa; il QR Code si rigenera a ogni avvio con l'IP corretto (Capitolo 6).
- **Nessuna latenza esterna**: il canale realtime (SignalR/WebSocket) resta dentro la
  LAN; l'esperienza al tavolo non dipende dalla qualità della connessione internet di casa.
- **Privacy per design**: i dati di personaggi, homebrew e campagne restano su un disco
  fisico, non su un server di terzi.

### Contro

- **Portabilità zero**: la sessione esiste solo se tutti sono fisicamente sulla stessa
  rete Wi-Fi; niente gioco a distanza, niente giocatori da remoto.
- **Dipendenza da una singola macchina**: se il PC del master si spegne o va in
  sospensione, il server cade con lui; non c'è ridondanza.
- **Nessun backup automatico in cloud**: la persistenza è locale (SQLite); un disco
  danneggiato senza backup manuale è una perdita reale di dati.
- **Onere di gestione sul master**: è lui l'unico responsabile dell'infrastruttura,
  anche minima; non c'è un servizio terzo a cui delegare uptime o manutenzione.
- **Reti ostili o instabili**: Wi-Fi domestici affollati o isolamento client
  (AP isolation) in reti pubbliche possono bloccare la scoperta dei dispositivi.

Il compromesso è dichiarato, non subito: si rinuncia al gioco remoto per guadagnare
sicurezza, costo zero e immediatezza. Per un tavolo fisico settimanale, è lo scambio
corretto.

## 5. Struttura: Master desktop / Player web

Il sistema si divide in due ruoli asimmetrici per responsabilità e superficie di
accesso, non per funzionalità.

### Master — applicazione desktop

- È il bootstrapper: avvia il server locale, ne supervisiona il ciclo di vita, mostra
  il QR aggiornato.
- Ha accesso esclusivo alle rotte di amministrazione, servite solo su loopback
  (127.0.0.1): i giocatori in LAN non possono raggiungerle nemmeno per errore.
- Gestisce il modello dati, l'homebrew, l'economia del turno come fonte di verità.

### Player — client web mobile

- Nessuna installazione: si apre nel browser del telefono dopo la scansione del QR.
- Nessuno stato persistente locale: il client si ridisegna in reazione ai soli messaggi
  WebSocket ricevuti dal master.
- UI mobile-first, pensata per l'azione rapida: accesso immediato ad Azioni, Azioni
  Bonus, Reazioni.

Il confine tra i due ruoli è anche un confine di sicurezza: separare "chi amministra"
da "chi gioca" a livello di rotta di rete, non solo di interfaccia, è ciò che rende
superfluo un sistema di permessi complesso.

## 6. Discovery di rete e QR Code Dinamico

Nei router domestici (DHCP), l'indirizzo IP locale assegnato alla macchina del Master è
soggetto a variazioni continue in base alla rete Wi-Fi o all'ordine di connessione dei
dispositivi. Di conseguenza, il QR Code necessario per connettere i dispositivi dei
giocatori viene rigenerato dinamicamente a ogni avvio del server locale, inglobando il
nuovo indirizzo IP corretto.

## 7. Modello dati strutturale: EAV, JSON dinamico e l'entità Fonte

Il database utilizzerà un pattern ibrido Entity-Attribute-Value (EAV) basato su JSON
dinamico per massimizzare la flessibilità ed esaudire il requisito della UI collassabile.

### Parametri obbligatori di ogni entità

- **Identificativo e Nome Breve**: un'etichetta visibile a colpo d'occhio
  (es. "Attacco Furtivo").
- **Categoria**: permette il raggruppamento semantico dell'elemento (es. razza, classe,
  sottoclasse, equipaggiamento).
- **Costo di Attivazione**: codificato per integrarsi con l'economia del turno (Azione,
  Azione Bonus, Reazione, Interazione, Nessuno, Slot di Livello N).
- **Contatore di Utilizzo**: traccia gli usi residui (es. "1/Riposo Breve") e il
  relativo ciclo di ripristino delle risorse.
- **Testo Esplicativo Lazy-Loaded**: la descrizione completa non viene inviata nel
  payload di rete iniziale, ma caricata asincronamente solo quando l'utente clicca per
  espandere l'elemento.
- **Puntatore di Deduplicazione**: se un elemento è comune a più fonti, un riferimento
  univoco impedisce la duplicazione del testo.

### Evoluzione concettuale: da Categoria piatta a entità Fonte

Insight emerso in fase di analisi: trattare il multiclasse come funzione a sé porta a un
errore classico — scrivere codice per "unire classe A e classe B" invece che per
"aggregare N fonti qualsiasi". Il multiclasse non è un caso a parte; è semplicemente il
caso in cui il personaggio ha più di una fonte dello stesso tipo (classe). Il codice non
dovrebbe saperlo.

La generalizzazione proposta: il campo "Categoria" viene promosso da tag descrittivo a
entità relazionale vera e propria — **l'entità Fonte**. Ogni Fonte ha: un tipo (classe,
sottoclasse, razza, background, oggetto, homebrew...), un nome, e possibilmente una
Fonte "genitore" (la sottoclasse referenzia la classe, per esempio). Ogni
abilità/tratto/incantesimo è collegato a una o più Fonti tramite il Puntatore di
Deduplicazione già previsto.

**Conseguenza pratica**: il personaggio è, a tutti gli effetti, una lista di Fonti
attive. Un ladro/monaco multiclasse ha due Fonti di tipo "classe"; un personaggio
mono-classe ne ha una sola; la logica di rendering è identica in entrambi i casi —
raggruppa per Fonte, collassa, deduplica. Zero codice dedicato al multiclasse: è solo un
filtro sull'array di Fonti, che nel caso mono-classe restituisce lunghezza 1.

**Vantaggio collaterale — portabilità**: un motore che ragiona in termini di "Fonti che
generano feature con costo di attivazione e contatore d'uso" non sa nulla di specifico
su D&D 5e. Pathfinder (ancestry + background + classe), o sistemi con discipline/skill
invece di incantesimi, rientrano nello stesso schema senza refactoring — cambia solo
quali Fonti vengono caricate, non come il motore le tratta. L'homebrew (Capitolo 10) si
aggancia allo stesso schema: una regola homebrew è semplicemente una nuova Fonte, non un
sistema parallelo.

**Validazione esterna di questo pattern**: l'estensione "Forge" per Owlbear Rodeo
implementa un initiative tracker con colonne personalizzabili (HP, AC, contatori di
risorse, effetti di stato) configurabili senza toccare il codice — stesso principio di
generalizzazione, applicato in un prodotto già sul mercato. Vedi Capitolo 12.

### Visualizzazione

- **Personaggi Multiclasse**: l'interfaccia presenterà colonne dedicate a ciascuna
  classe, inizialmente collassate. L'espansione del nodo "classe" rivelerà solo i nomi
  delle feature per livello; un'ulteriore espansione mostrerà i dettagli (es. colonne
  separate per "Ladro" e "Monaco").
- **Personaggi Incantatori (Caster)**: la gestione degli incantesimi noti e preparati
  utilizzerà le stesse primitive strutturali: liste collassabili e deduplicazione dei
  testi condivisi.

## 8. Motore di Combattimento (Rule Engine)

L'economia del turno sarà implementata attraverso il pattern architetturale della
Macchina a Stati Finiti. Il motore agirà come fonte assoluta di verità (Source of Truth).

La validazione degli intenti avviene rigorosamente lato motore: se un personaggio tenta
di lanciare un incantesimo ma non possiede gli slot richiesti, l'interfaccia utente
blocca l'operazione alla fonte e l'intento non viene nemmeno inoltrato a SignalR.
Questo previene discrepanze di stato e desincronizzazioni.

## 9. Gestione Fuori dal Combattimento

Il sistema manterrà in memoria lo stato delle risorse limitate (cariche di oggetti
magici, slot incantesimi) anche al di fuori dei turni di iniziativa. Una volta
implementato e stabilizzato il modello dati (Capitolo 7), si prevede l'aggiunta di una
vista per l'inventario ordinabile, considerata un'estensione a basso impatto tecnico.

## 10. Estensibilità e Homebrew Dinamico

L'uso delle funzioni native JSON1 di SQLite è fondamentale per supportare regole e
parametri personalizzati (Homebrew) in modo efficiente. Avere una singola colonna JSON
per gli attributi custom risulta nettamente più performante e manutenibile rispetto alla
creazione di innumerevoli tabelle in stile EAV puro.

L'interfaccia frontend (Angular) rimarrà del tutto agnostica: interpreterà al volo le
direttive JSON inviate dal Master (es. l'aggiunta di una statistica chiamata
"Sanità Mentale") e genererà i widget UI (bottoni, barre HP, counter) senza richiedere
alcuna modifica al codice sorgente dell'applicativo.

## 11. Comparto tecnico proposto

| Componente | Ruolo e responsabilità |
|---|---|
| **C# + Photino.NET** | Bootstrapper e finestra nativa del Master; supervisiona il ciclo di vita dei servizi in background. |
| **ASP.NET Core / Kestrel** | Server LAN in-process; file statici per il client Angular; nessuna finestra terminale separata. |
| **SignalR** | Hub realtime tramite WebSocket; broadcast/multicast di stato; canale degli "intenti" master ↔ giocatori. |
| **Angular** | Client mobile dei giocatori; nessuno stato persistente locale; si ridisegna in reazione ai messaggi WebSocket. |
| **SQLite** | Persistenza locale embedded; ogni intento validato è scritto su disco prima del broadcast; garantisce atomicità e sicurezza. |

**Sicurezza e isolamento**: il middleware server (Kestrel) è configurato per rispondere
alle rotte di amministrazione del Master esclusivamente tramite loopback locale
(127.0.0.1). Questo meccanismo impedisce ai giocatori connessi via LAN di accedere
inavvertitamente alle schermate di controllo del Master.

## 12. Ricerca di mercato: cosa fanno già gli altri strumenti

Il mercato si divide in tre fasce; nessuna copre combattimento, inventario e local-first
nello stesso motore stateful.

### VTT generalisti — Foundry VTT

Foundry gestisce combat tracker e inventario, ma come due sistemi separati collegati da
drag&drop: il loot va trascinato manualmente dal token alla scheda, e serve un modulo
dedicato (Loot Sheet NPC) solo per automatizzare quella sincronizzazione. Non esiste una
fonte di verità unica che collega danno inflitto, oggetto droppato e inventario
aggiornato senza intervento manuale del master: è potenza bruta, non un motore di stato.

### Overlay leggeri — Owlbear Rodeo

L'ecosistema è a moduli indipendenti: un'estensione per l'HP, una per l'iniziativa, una
per il loot, una per il QR di invito. Esiste un'estensione (Game Master's Grimoire) che
offre gestione di inventario di gruppo con sincronizzazione istantanea tra giocatori —
ma il sync passa da un servizio cloud esterno (Tabletop Almanac), non da un server
locale: è la controprova diretta della tesi di frammentazione, perché anche chi ha
provato a unificare l'ha fatto solo appoggiandosi al cloud.

*Nota di validazione architetturale*: l'estensione "Forge" per Owlbear implementa
colonne personalizzabili (HP, AC, risorse, effetti) senza toccare codice — stesso
principio dell'entità Fonte generalizzata (Capitolo 7): righe/colonne generiche invece
di campi fissi per sistema.

### Schede cloud — Roll20, D&D Beyond, Shard Tabletop

Ottime su spell e inventory management, ma sempre con account, sempre online, e con lo
stesso difetto isolato nel Capitolo 1: mostrano il testo completo di ogni abilità invece
di una gerarchia collassabile. Nessuna delle tre ha un vero motore di validazione degli
intenti lato server: il controllo "hai lo slot per lanciare questo incantesimo" è
demandato all'utente o a script client-side facilmente aggirabili, non a una state
machine come quella descritta nel Capitolo 8.

### Il vuoto confermato

Combinando i tre livelli: nessuno strumento ha insieme persistenza locale senza account,
join via QR dinamico, motore di validazione come fonte di verità, inventario e
combattimento nello stesso stato sincronizzato in realtime, homebrew senza refactoring.
Chi ha le prime due è troppo semplice (overlay); chi ha le altre due è cloud-based con
account. La combinazione proposta resta uno spazio libero, non solo per assenza di
concorrenti diretti ma perché richiede scelte architetturali (loopback, EAV, SignalR)
che nessun prodotto sul mercato ha fatto insieme.

## 13. Ordine di priorità per il Prodotto Minimo Funzionante (MVP)

Per rispettare la deadline indicativa di settembre, il lavoro procederà seguendo una
gerarchia di completamento rigorosa:

1. **Modello dati collassabile e deduplicazione** (Capitolo 7, inclusa l'entità Fonte):
   massima priorità in quanto risolve il dolore principale (pain point) attualmente
   avvertito al tavolo da gioco.
2. **Server locale, Bootstrap e QR** (Capitoli 5-6): fondamentale per permettere la
   connessione di rete; senza di questo, i playtest con i giocatori sono impossibili.
3. **Economia del turno** (Capitolo 8): sviluppo del nucleo logico di validazione, le
   cui regole base sono già state definite a livello teorico.
4. **Gestione Fuori Combattimento e Inventario** (Capitolo 9): funzionalità derivata dal
   completamento del punto 1, che richiede costi di sviluppo marginali.
5. **Motore Homebrew** (Capitolo 10): questa fase può essere ragionevolmente posticipata
   dopo l'uscita dell'MVP. Grazie alla flessibilità architetturale garantita dalla
   colonna JSON del punto 1, attivare i contenuti homebrew in futuro non richiederà
   refactoring strutturali del database.

## 14. Conclusione

Il progetto non compete sulle funzionalità — quelle esistono già, sparse tra decine di
strumenti diversi — ma sulla combinazione: nessun concorrente unisce assenza di cloud,
join istantaneo, validazione attiva delle regole e modello dati agnostico nello stesso
motore (Capitolo 12).

Il prezzo da pagare è dichiarato: si rinuncia al gioco a distanza per guadagnare
sicurezza, costo zero e immediatezza al tavolo (Capitolo 4). Per l'uso previsto — un
master, una giocatrice, una sessione settimanale in presenza — è lo scambio giusto, non
un compromesso subito.

La priorità resta il modello dati (Capitolo 7): è lì che si gioca la vera differenza
percepita al tavolo, ed è lì che la generalizzazione in entità Fonte garantisce che il
lavoro fatto per il multiclasse non vada rifatto per homebrew, portabilità ad altri
sistemi, o casi futuri non ancora previsti.

---

*Fine documento. Per il dettaglio implementativo dei singoli capitoli, fare riferimento
a questo documento come indice: ogni sezione può essere espansa in una specifica tecnica
separata quando si arriva a implementarla.*
