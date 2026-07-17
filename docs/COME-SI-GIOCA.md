# Come si gioca — cosa fa il motore, e cosa no

Questa pagina risponde a domande concrete: *come si tira un'abilità? come si tira per
colpire? dove sta il tiro dei danni?* — e dice, senza sconti, dove il motore si ferma e
il Master deve fare da sé.

Ogni riga qui è stata **verificata giocando**, non dedotta dal codice. Le regole di D&D
citate vengono dall'[SRD 5.1](https://media.wizards.com/2016/downloads/DND/SRD-OGL_V5.1.pdf).

---

## L'idea in una riga

**Tutto è una feature con una formula.** Un attacco, i danni, una prova di abilità, un
incantesimo: cambia solo cosa costa e cosa tira. Non ci sono entità separate per
"abilità" o "arma" — sono tutte righe della stessa lista, ed è il motivo per cui la
scheda resta corta.

## I due tipi di numero

| Nel profilo | Nelle formule `@Nome` vale | Esempi |
|---|---|---|
| statistica **con** modificatore | il **modificatore** | Forza 18 → `@Forza` = **+4** |
| statistica **senza** modificatore | il **valore intero** | CA 16 → `@CA` = **16**; Competenza 3 → **3** |

È la distinzione che fa funzionare tutto il resto. `1d20+@Destrezza+@Competenza` dà
`1d20 + 3 + 2` = **1d20+5**, che è esattamente il bonus d'attacco di un Ladro di 3°.

Il modificatore è **un dato, non codice**: `floor((valore − base) / passo)`, dichiarato
dal profilo e modificabile dalla console (Sistema → Statistiche). D&D e Pathfinder 2e
usano `base 10, passo 2`; Vampiri non lo dichiara affatto, e i suoi punteggi valgono
interi — perché lì un 4 vale *quattro dadi*, non «+4».

Cambiarlo è una spunta e due numeri: messo `base 0, passo 5`, Forza 18 smette di valere
+4 e vale +3, **in ogni formula, senza ricompilare**. È stato verificato così.

---

## Le ricette

### Un tiro per colpire

Feature con costo **Azione** e formula:

```
1d20+@Destrezza+@Competenza      → Ladro con arma finesse
1d20+@Forza+@Competenza          → Guerriero in mischia
```

Il motore tira. **Il confronto con la CA lo fai tu**: il motore non sa chi stai
attaccando (vedi *Dove si ferma*).

### I danni, dopo aver colpito

Una feature **a parte**, **senza costo** — perché i danni non consumano un'altra Azione:

```
1d8+@Forza      → spada lunga
2d8+@Forza      → artigli dell'orso gufo
```

Nota che la **competenza non entra nei danni**: è la regola, ed è per questo che sono
due formule e non una.

### Una prova di abilità

Una feature **senza costo** (le abilità non consumano il turno) con la caratteristica
che le compete:

```
Furtività    →  1d20+@Destrezza+@Competenza
Percezione   →  1d20+@Saggezza+@Competenza
Atletica     →  1d20+@Forza+@Competenza
```

**Eccezione: l'azione Cercare costa l'Azione intera** (RAW). Quella la si scrive con
costo Azione — verificato: dopo averla usata, attaccare viene respinto.

### Un'arma che porta le sue azioni

Crea una **Fonte** di tipo oggetto ("Arco lungo +1"), poi le sue feature con quel
`sourceId`. Attacco e danni arrivano insieme all'arma e si raggruppano sotto di lei.
È lo stesso meccanismo delle classi: un'arma è una Fonte come il Ladro.

### Un oggetto che cambia i numeri

Feature **indossabile** con **effetti**:

```
Guanti di Destrezza → effetto: Destrezza +2, indossabile
```

Acceso, `@Destrezza` vale già di più **in ogni formula**, senza che i dadi sappiano cosa
sia un guanto. Verificato: Destrezza 20 (+5) → indossati → 22 (+6) → il tiro per colpire
passa da +15 a +16 da solo.

### Una riserva condivisa (il ki, il mana, i punti focus)

Quando più capacità pescano dagli **stessi** punti, quei punti sono una *riserva*, non
un contatore. Si dichiara una volta in **Sistema → Riserve** (tipo *a punti*, ricarica
*Riposo breve*), poi ogni capacità la mette fra i suoi costi:

```
Raffica di Colpi   →  Azione Bonus + Ki 1
Difesa Paziente    →  Azione Bonus + Ki 1
Passo del Vento    →  Azione Bonus + Ki 1
```

Il punto è che sono **gli stessi tre punti**: spenderne uno con la Raffica lo toglie
anche alle altre due. Col contatore per capacità — "3 usi a riposo breve" su ciascuna —
un monaco di 3° ne farebbe nove, e il motore non avrebbe niente da obiettare. Verificato:
finito il ki, la Raffica viene respinta con «Punti insufficienti» *mentre l'azione bonus
è ancora libera*, e un intento respinto non scala nulla.

La riserva compare sulla scheda solo di chi ce l'ha: un massimo a 0 la toglie del tutto,
quindi un guerriero non si vede nessun "Punti Ki 0".

### Una capacità "una volta per turno"

Usi = 1, ricarica = **"A ogni turno"**.

*Per turno* significa **per ogni turno**, non per round: in un round ci sono N turni, e
chi agisce nel turno altrui (attacco di opportunità, azione preparata) la riusa. Un Ladro
ha diritto a **due** Attacchi Furtivi per round — uno suo, uno con la reazione. Il motore
ricarica il "per turno" a ogni turno che comincia, di chiunque sia.

---

## Dove si ferma il motore

Non sono bug: sono il confine di cosa il progetto fa. Il Master fa il resto, come già fa
sul suo foglio.

| Cosa | Stato |
|---|---|
| **Bersaglio e CA** | il motore tira, **non sa chi stai colpendo**. Il confronto è tuo. |
| **Critico (20 naturale)** | non esiste. La regola vera (raddoppia i **dadi**, non il modificatore) la applichi a mano. |
| **Multiattacco** | nessuna meccanica: si modella col primo attacco che costa l'Azione e gli altri a costo zero. |
| **Azioni leggendarie** | non esistono. |
| **Tiri salvezza** | sono feature con formula come le altre; la CD la tieni tu. |
| **Livello e classi** | il motore non li conosce. La Competenza è un numero che scrivi salendo di livello. |

### I dadi: somme e pool

| Sistema | Cosa vuole | Il motore |
|---|---|---|
| D&D 5e, **Pathfinder 2e** | `1d20 + mod + competenza` vs CD | ✅ |
| **Vampiri / Mondo di Tenebra** | N d10, conta i successi ≥6 | ✅ pool |
| Call of Cthulhu | d100 **sotto** il punteggio | ❌ confronto |
| Savage Worlds | dadi che esplodono | ❌ |
| Fate | 4dF | ❌ |

Restano fuori i dadi che **esplodono** (Savage Worlds) e il **confronto** di Call of
Cthulhu: i primi cambiano quanti dadi cadono *mentre cadono*, il secondo chiede al
motore di dire "riuscito/fallito" invece di un numero. Sono due motori diversi, non due
opzioni.

## I pool a successi (Vampiri e simili)

Due forme, che si compongono:

```
@Destrezza d10                        tanti d10 quanto vale Destrezza
@Destrezza d10 + @{Furtività} d10     Attributo + Abilità: 4+3 = sette d10
@Destrezza d10 + @{Furtività} d10 >= 6    …e ogni faccia da 6 in su è un successo
```

`>= N` cambia cosa significa il totale: non «quanto fa», ma **quanti dadi ce l'hanno
fatta**. Con `Destrezza 4 + Furtività 3` escono sette d10 e un risultato tipo
`[7, 10, 8, 10, 7, 9, 1] → 6 successi`.

Perché il pool e non `7d10` scritto a mano: `7d10` resta a sette dadi anche quando il
punteggio cambia. Il pool segue la scheda — e gli effetti attivi entrano gratis, come
ovunque.

---

## Cosa è stato verificato giocando

Combattimento reale contro il motore, sei avventurieri su tre fasce di livello e quattro
mostri (Goblin e Orso Gufo dall'SRD; Falsa Idra e Bagman sono homebrew — nessun blocco
ufficiale esiste, i loro numeri sono una scelta dichiarata nelle note).

- bonus d'attacco di un Ladro 3: **+5** (Des +3, comp +2) ✅
- bonus d'attacco dell'Orso Gufo: **+7** — identico al blocco SRD ✅
- seconda Azione nello stesso turno: **respinta** ✅
- le tre opzioni ki del Monaco pescano dagli **stessi** 3 punti, e a zero si fermano tutte
  anche con l'azione bonus libera ✅
- il ki **non** torna a inizio turno (ricarica: riposo breve); l'anello a cariche **non**
  torna col breve ma col lungo ✅
- azione Cercare: **consuma l'Azione intera** ✅
- incantesimo senza slot: **respinto** ✅
- Attacco Furtivo **due volte in un round** (proprio turno + reazione) ✅
- riposo breve ricarica il breve, non il giornaliero ✅
- riposo lungo ricarica entrambi ✅
- level-up: PF, punteggi e competenza nuovi entrano **subito** in attacchi, danni e
  abilità ✅
