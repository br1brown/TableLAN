namespace TableLAN.Server.Services;

using TableLAN.Core.Characters;
using TableLAN.Core.Dice;
using TableLAN.Core.Engine;
using TableLAN.Core.Features;
using TableLAN.Core.Profile;
using TableLAN.Server.Data;

/// <summary>Esito di un tiro: il verdetto e, se è andato, la riga di log.</summary>
public sealed record RollOutcome(ValidationResult Verdict, RollEntry? Entry);

/// <summary>
/// Fonte di verità in memoria della sessione. Lo strumento è la scheda snella
/// personale dei giocatori, non un VTT condiviso; l'economia del turno resta
/// perché è utile al singolo giocatore.
///
/// Le meccaniche (economia del turno, riserve, cicli di riposo) non sono più
/// cablate: derivano dal <see cref="GameProfile"/> caricato dal DB. Il motore
/// interpreta i costi delle feature tramite il profilo, così lo strumento si
/// adatta ad altri sistemi cambiando profilo, non codice.
/// </summary>
public sealed class GameStateService(GameRepository repository, RollLogService rollLog)
{
    private readonly object _gate = new();
    private readonly RuleEngine _engine = new();
    private readonly IRandomSource _rng = SystemRandom.Instance;
    private readonly Dictionary<string, TurnState> _turns = new();
    private Dictionary<string, Character> _characters = new();

    /// <summary>
    /// I mostri: personaggi a tutti gli effetti, tenuti in un dizionario a
    /// parte per un motivo solo — <see cref="Require"/> guarda solo in
    /// <c>_characters</c>, quindi nessuna rotta LAN può indirizzarli. Il
    /// confine è di rete e di lookup, non di UI: un telefono non li vede e non
    /// li tocca nemmeno conoscendone l'id.
    /// </summary>
    private Dictionary<string, Character> _monsters = new();

    private GameProfile _profile = GameProfiles.Dnd5e();

    /// <summary>
    /// Un partecipante all'iniziativa.
    ///
    /// Il numero c'è perché senza non è iniziativa: era una lista da ordinare a
    /// mano, e il Master l'ordine ce l'ha già sul foglio. <see cref="Kind"/>
    /// distingue chi ha una scheda nel motore da chi il Master gioca per conto
    /// suo — è ciò che permette due punti d'ingresso separati (i giocatori al
    /// tavolo, le bestie dal bestiario) su un'unica lista.
    ///
    /// L'identità è <see cref="RefId"/>: ogni mostro del bestiario è una riga
    /// sua con i suoi PF, quindi due goblin sono già due voci distinte e non
    /// serve un id sintetico sopra a quello che c'è.
    /// </summary>
    public sealed class InitiativeEntry
    {
        public required string RefId { get; init; }
        public required string Kind { get; init; }   // "pc" | "monster"
        public required string Name { get; init; }
        public int Initiative { get; init; }
    }

    public sealed class InitiativeState
    {
        public List<InitiativeEntry> Order { get; set; } = new();
        public string? ActiveId { get; set; }        // RefId di chi è di turno
    }

    private readonly InitiativeState _initiative = new();

    /// <summary>
    /// L'iniziativa corrente, in copia: chi legge non tiene in mano la lista
    /// viva del servizio mentre un'altra richiesta la riordina.
    /// </summary>
    public (IReadOnlyList<InitiativeEntry> Order, string? ActiveId) InitiativeSnapshot()
    {
        lock (_gate)
            return ([.. _initiative.Order], _initiative.ActiveId);
    }

    /// <summary>Quanto resta di una risorsa di turno a un personaggio.</summary>
    public int TurnRemaining(string characterId, string resourceId)
    {
        lock (_gate)
            return Turn(characterId).Remaining(resourceId);
    }

    /// <summary>
    /// Le schede dei mostri, con lo stesso DTO dei giocatori — PF, capacità,
    /// costi, effetti, tiri. Prima <c>/api/admin/monsters</c> restituiva le
    /// righe grezze di SQLite (<c>featureIdsJson</c> e compagnia), e per questo
    /// la scheda di un mostro non si è mai potuta aprire davvero.
    ///
    /// Solo loopback: la rotta che chiama questo metodo sta dietro
    /// <c>LoopbackOnlyMiddleware</c>.
    /// </summary>
    public IReadOnlyList<object> MonsterDtos()
    {
        lock (_gate)
            return [.. _monsters.Values.Select(BuildDto)];
    }

    /// <summary>La scheda di un mostro, o null se non è nel bestiario.</summary>
    public object? MonsterDto(string monsterId)
    {
        lock (_gate)
            return _monsters.TryGetValue(monsterId, out var m) ? BuildDto(m) : null;
    }

    /// <summary>
    /// Un mostro esiste? Serve alle rotte admin, che a differenza di quelle LAN
    /// devono poterlo indirizzare.
    /// </summary>
    public bool MonsterExists(string id)
    {
        lock (_gate)
            return _monsters.ContainsKey(id);
    }

    /// <summary>
    /// Ricarica un mostro dal disco dopo una modifica. I mostri non passano da
    /// <see cref="ReloadAllCharactersAsync"/>, che carica i soli giocatori.
    /// </summary>
    public async Task ReloadMonstersAsync()
    {
        var monsters = await repository.LoadMonstersAsync();
        lock (_gate)
            _monsters = monsters.ToDictionary(c => c.Id);
    }

    public async Task InitializeAsync()
    {
        var profile = await repository.LoadProfileAsync() ?? GameProfiles.Dnd5e();

        // All'avvio, non solo al salvataggio del profilo: le schede possono
        // essere più vecchie delle statistiche che il sistema dichiara oggi.
        await repository.SyncCharactersWithProfileAsync(profile);

        var characters = await repository.LoadCharactersAsync();
        var monsters = await repository.LoadMonstersAsync();
        lock (_gate)
        {
            _profile = profile;
            _characters = characters.ToDictionary(c => c.Id);
            _monsters = monsters.ToDictionary(c => c.Id);
            _turns.Clear();
            foreach (var id in _characters.Keys)
                _turns[id] = new TurnState(_characters[id].EffectiveTurnLimits(_profile));
        }
    }

    public GameProfile Profile
    {
        get { lock (_gate) { return _profile; } }
    }

    /// <summary>
    /// Sostituisce il profilo di sistema attivo. Le economie del turno in corso
    /// vengono ricostruite sul nuovo vocabolario di risorse.
    /// </summary>
    public async Task SetProfileAsync(GameProfile profile)
    {
        await repository.SaveProfileAsync(profile);

        // Le statistiche sono del sistema: se il profilo dice che esistono, le
        // schede le hanno tutte — anche quelle nate prima. Altrimenti "tutti i
        // personaggi hanno esattamente quei valori" resterebbe vero solo per
        // chi è arrivato dopo.
        await repository.SyncCharactersWithProfileAsync(profile);
        var characters = await repository.LoadCharactersAsync();

        lock (_gate)
        {
            _profile = profile;
            _characters = characters.ToDictionary(c => c.Id);
            foreach (var id in _characters.Keys)
                _turns[id] = new TurnState(_characters[id].EffectiveTurnLimits(_profile));
        }
    }

    public async Task<ValidationResult> SubmitIntentAsync(Intent intent)
    {
        Character character;
        int? slotUsed = null;
        ValidationResult applied;

        lock (_gate)
        {
            if (!_characters.TryGetValue(intent.CharacterId, out character!))
                return ValidationResult.Fail($"Personaggio '{intent.CharacterId}' sconosciuto.");

            // Il livello di slot speso, per il registro: fra i costi della
            // feature si cerca quello pagato da una riserva a livelli.
            var feature = character.Features.FirstOrDefault(f => f.Id == intent.FeatureId);
            if (feature?.RealCosts.FirstOrDefault(c => _profile.Pool(c.Kind) is { Kind: PoolKind.Leveled })
                is { } leveledCost && !leveledCost.IsNone)
                slotUsed = intent.SlotLevelOverride ?? leveledCost.EffectiveAmount;

            applied = _engine.Apply(character, intent, Turn(character.Id), _profile);
            if (!applied.IsValid)
                return applied;
        }

        // Persistenza su disco PRIMA che l'esito torni al chiamante per il
        // broadcast: se la scrittura fallisce l'eccezione impedisce il sync.
        await repository.PersistIntentAsync(character, intent.FeatureId, slotUsed);

        // Si restituisce l'esito del motore, non un Ok() nuovo: quello buttava
        // via il Notice, e la Benedizione cadeva senza che nessuno lo dicesse.
        return applied;
    }

    /// <summary>
    /// Tira il dado di una feature, spendendone il costo se richiesto.
    ///
    /// Spendere è opzionale perché il tiro e il costo non coincidono: il Dardo
    /// Incantato spende uno slot e tira tre volte, e l'Attacco Furtivo si somma
    /// a un colpo già tirato. Un solo booleano copre entrambi i casi.
    ///
    /// L'ordine non è negoziabile — validare, poi spendere, poi tirare. Tirare
    /// prima vorrebbe dire, su un rifiuto, aver già bruciato dei dadi e mostrato
    /// nel log un tiro che non è mai avvenuto.
    /// </summary>
    public async Task<RollOutcome> RollAsync(string characterId, RollRequest request)
    {
        Character character;
        Feature feature;
        RollResult roll;

        lock (_gate)
        {
            if (!_characters.TryGetValue(characterId, out character!))
                return new RollOutcome(ValidationResult.Fail($"Personaggio '{characterId}' sconosciuto."), null);

            feature = character.Features.FirstOrDefault(f => f.Id == request.FeatureId)!;
            if (feature is null)
                return new RollOutcome(ValidationResult.Fail($"Feature '{request.FeatureId}' inesistente per '{character.Name}'."), null);

            if (string.IsNullOrWhiteSpace(feature.Roll))
                return new RollOutcome(ValidationResult.Fail($"'{feature.ShortName}' non ha un tiro."), null);

            if (!DiceFormula.TryParse(feature.Roll, out var formula, out var parseError))
                return new RollOutcome(ValidationResult.Fail($"Formula di '{feature.ShortName}' non valida: {parseError}"), null);

            // La formula si risolve prima di qualunque spesa: se referenzia una
            // statistica che non esiste, non si paga niente.
            var evaluation = formula!.TryEvaluate(
                StatsFor(character),
                new RollOptions(request.Times, request.KeepMode),
                _rng,
                character.Name,
                out var evaluated);
            if (!evaluation.IsValid)
                return new RollOutcome(evaluation, null);

            roll = evaluated!;
        }

        ValidationResult verdict = ValidationResult.Ok();
        if (request.Spend)
        {
            verdict = await SubmitIntentAsync(new Intent(characterId, request.FeatureId)
            {
                SlotLevelOverride = request.SlotLevelOverride,
            });
            if (!verdict.IsValid)
                return new RollOutcome(verdict, null); // costo non pagabile → il tiro non conta
        }

        // Il verdetto viaggia intero anche quando è andata: una magia di
        // concentrazione si tira *e* fa cadere la precedente, e il Notice è
        // l'unica cosa che lo dice.
        var entry = rollLog.Add(characterId, character.Name, feature.ShortName, request.Spend, roll);
        return new RollOutcome(verdict, entry);
    }

    /// <summary>
    /// Il tiro di un mostro.
    ///
    /// Stessa formula, stesse statistiche, stesso log del tavolo: un orso gufo
    /// che attacca non è un'altra cosa da un ladro che attacca. L'unica
    /// differenza è che qui non si spende niente — il turno di un mostro lo
    /// tiene il Master sul suo foglio, e il motore non lo conta.
    /// </summary>
    public async Task<RollOutcome> RollMonsterAsync(string monsterId, RollRequest request)
    {
        await Task.CompletedTask;

        lock (_gate)
        {
            if (!_monsters.TryGetValue(monsterId, out var monster))
                return new RollOutcome(ValidationResult.Fail($"Mostro '{monsterId}' sconosciuto."), null);

            var feature = monster.Features.FirstOrDefault(f => f.Id == request.FeatureId);
            if (feature is null)
                return new RollOutcome(ValidationResult.Fail($"Feature '{request.FeatureId}' inesistente per '{monster.Name}'."), null);

            if (string.IsNullOrWhiteSpace(feature.Roll))
                return new RollOutcome(ValidationResult.Fail($"'{feature.ShortName}' non ha un tiro."), null);

            if (!DiceFormula.TryParse(feature.Roll, out var formula, out var parseError))
                return new RollOutcome(ValidationResult.Fail($"Formula di '{feature.ShortName}' non valida: {parseError}"), null);

            var evaluation = formula!.TryEvaluate(
                StatsFor(monster),
                new RollOptions(request.Times, request.KeepMode),
                _rng,
                monster.Name,
                out var evaluated);
            if (!evaluation.IsValid)
                return new RollOutcome(evaluation, null);

            var entry = rollLog.Add(monsterId, monster.Name, feature.ShortName, spent: false, evaluated!);
            return new RollOutcome(ValidationResult.Ok(), entry);
        }
    }

    /// <summary>
    /// Statistiche visibili a una formula: quelle effettive (base + effetti
    /// attivi, così un anello equipaggiato cambia da solo ogni @riferimento),
    /// più la chiave riservata dei PF massimi.
    /// </summary>
    /// <summary>
    /// I modificatori delle statistiche che ne dichiarano uno, già calcolati
    /// sui valori effettivi — quindi un oggetto che dà Forza +2 può far salire
    /// anche il modificatore, e la scheda lo mostra senza saperne le regole.
    /// Le statistiche senza modificatore non compaiono affatto.
    /// </summary>
    private Dictionary<string, int> ModifiersFor(Character c)
    {
        var mods = new Dictionary<string, int>();
        foreach (var (label, raw) in c.EffectiveStats())
        {
            var def = _profile.Stats.FirstOrDefault(s => s.Label == label);
            if (def?.Modifier is not null && Character.TryReadNumber(raw, out var v))
                mods[label] = def.Modifier.Of(v);
        }
        return mods;
    }

    /// <summary>
    /// Le statistiche come le vede una formula: non il punteggio, ma **ciò che
    /// sommeresti al tavolo**.
    ///
    /// Per le caratteristiche con modificatore è il modificatore: Forza 18
    /// entra in <c>1d20+@Forza</c> come +4, non come +18. Per le altre — la CA,
    /// l'Umanità di Vampiri — è il valore intero, perché non hanno modificatore
    /// e sommarle per intero è esattamente ciò che si fa.
    ///
    /// La traduzione sta qui e non dentro <c>Dice</c>: il motore dei dadi sa
    /// fare i conti, non sa cosa sia la Forza, e continua a non saperlo.
    /// </summary>
    private Dictionary<string, object?> StatsFor(Character c)
    {
        var stats = new Dictionary<string, object?>();

        foreach (var (label, raw) in c.EffectiveStats())
        {
            var def = _profile.Stats.FirstOrDefault(s => s.Label == label);

            // Un valore illeggibile resta com'è: sarà DiceFormula a rifiutarlo
            // con un messaggio sensato, invece di vederselo azzerare qui.
            stats[label] = def?.Modifier is not null && Character.TryReadNumber(raw, out var v)
                ? def.Effective(v)
                : raw;
        }

        stats[DiceFormula.MaxHpRef] = c.EffectiveMaxHp();
        return stats;
    }

    public async Task ResetTurnAsync(string characterId)
    {
        var character = Require(characterId);
        lock (_gate)
        {
            Turn(characterId).ResetForNewTurn(character.EffectiveTurnLimits(_profile));
            character.StartNewTurn(_profile);
        }
        await repository.SaveCharacterAsync(character);
    }

    /// <summary>
    /// Lascia uno slot esclusivo: smetti di concentrarti. Nessuna validazione —
    /// in 5e mollare la concentrazione non costa niente e si può sempre.
    /// </summary>
    public async Task ReleaseExclusiveAsync(string characterId, string slotId)
    {
        var character = Require(characterId);
        lock (_gate)
            character.Release(slotId);
        await repository.SaveCharacterAsync(character);
    }

    public async Task RestAsync(string characterId, string cycleId)
    {
        var character = Require(characterId);
        lock (_gate)
        {
            character.CompleteRest(cycleId, _profile);
            Turn(characterId).ResetForNewTurn(character.EffectiveTurnLimits(_profile));
        }
        await repository.SaveCharacterAsync(character);
    }

    public async Task AdjustHpAsync(string characterId, int delta)
    {
        var character = Require(characterId);
        lock (_gate)
        {
            character.AdjustHp(delta);
        }
        await repository.SaveCharacterAsync(character);
    }

    /// <summary>
    /// Imposta i PF di una scheda: è authoring, non gioco.
    ///
    /// Diverso da <see cref="AdjustHpAsync"/>, che è il danno e la cura al
    /// tavolo. Qui si sale di livello, o si corregge un numero digitato male —
    /// e serviva, perché finora i PF massimi non si potevano cambiare affatto:
    /// li fissavi alla creazione e restavano quelli per sempre.
    ///
    /// Un massimo che scende porta con sé i PF correnti (non puoi avere 30 PF
    /// su un massimo di 20); uno che sale non cura da solo, perché salire di
    /// livello non è un riposo.
    /// </summary>
    public Task SetHpAsync(string characterId, int? maxHp, int? currentHp) =>
        SetHpCoreAsync(Require(characterId), maxHp, currentHp);

    /// <summary>
    /// Come <see cref="SetHpAsync"/>, ma per un mostro. Due porte separate e
    /// non un parametro: <see cref="Require"/> guarda solo fra i giocatori, ed
    /// è ciò che impedisce a un telefono di rifare i PF all'orso gufo. Questa
    /// la chiama solo una rotta admin, dietro loopback.
    /// </summary>
    public Task SetMonsterHpAsync(string monsterId, int? maxHp, int? currentHp)
    {
        Character mostro;
        lock (_gate)
        {
            if (!_monsters.TryGetValue(monsterId, out mostro!))
                throw new KeyNotFoundException($"Mostro '{monsterId}' sconosciuto.");
        }
        return SetHpCoreAsync(mostro, maxHp, currentHp);
    }

    private async Task SetHpCoreAsync(Character character, int? maxHp, int? currentHp)
    {
        lock (_gate)
        {
            if (maxHp is { } max)
                character.MaxHp = Math.Max(1, max);

            var tetto = character.EffectiveMaxHp();
            character.CurrentHp = Math.Clamp(currentHp ?? character.CurrentHp, 0, tetto);
        }
        await repository.SaveCharacterAsync(character);
    }

    /// <summary>Accende/spegne gli effetti di una feature indossabile (equipaggia/rimuovi).</summary>
    public async Task ToggleEffectAsync(string characterId, string featureId, bool active)
    {
        var character = Require(characterId);
        lock (_gate)
        {
            character.EffectStates[featureId] = active;
        }
        await repository.SaveCharacterAsync(character);
    }

    /// <summary>
    /// Rimpiazza l'inventario ordinato del personaggio. Il client invia
    /// l'intera lista: aggiunta, modifica quantità e riordino sono la stessa
    /// operazione. Gli id mancanti (voci nuove) vengono generati a monte.
    /// </summary>
    public async Task SetInventoryAsync(string characterId, IEnumerable<InventoryItem> items)
    {
        var character = Require(characterId);
        lock (_gate)
        {
            character.Inventory.Clear();
            foreach (var item in items)
                character.Inventory.Add(item);
        }
        await repository.SaveCharacterAsync(character);
    }

    /// <summary>
    /// Ricarica dal disco un personaggio dopo una modifica di authoring
    /// (nuova mutazione, oggetto, magia, statistica).
    /// </summary>
    public async Task ReloadCharacterAsync(string characterId)
    {
        var fresh = await repository.LoadCharacterAsync(characterId);
        lock (_gate)
        {
            if (fresh is null)
                _characters.Remove(characterId);
            else
                _characters[characterId] = fresh;
        }
    }

    /// <summary>
    /// Ricarica tutti i personaggi. Serve quando la modifica tocca il catalogo
    /// condiviso delle feature (es. la rimappatura di un costo), che può
    /// interessare più di un personaggio.
    /// </summary>
    public async Task ReloadAllCharactersAsync()
    {
        var characters = await repository.LoadCharactersAsync();
        lock (_gate)
        {
            _characters = characters.ToDictionary(c => c.Id);
        }
    }

    public bool Exists(string characterId)
    {
        lock (_gate) { return _characters.ContainsKey(characterId); }
    }

    /// <summary>Elenco sintetico (id, nome, PF correnti/massimi effettivi) per il tracker.</summary>
    public IReadOnlyList<(string Id, string Name, int Current, int Max)> CharactersBrief()
    {
        lock (_gate)
        {
            return _characters.Values
                .Select(c => (c.Id, c.Name, c.CurrentHp, c.EffectiveMaxHp()))
                .ToList();
        }
    }

    public object Snapshot()
    {
        lock (_gate)
        {
            return new
            {
                profile = ProfileDto(_profile),
                characters = _characters.Values.Select(BuildDto).ToList(),
                initiative = _initiative
            };
        }
    }

    private Character Require(string characterId)
    {
        lock (_gate)
        {
            if (!_characters.TryGetValue(characterId, out var character))
                throw new KeyNotFoundException($"Personaggio '{characterId}' sconosciuto.");
            return character;
        }
    }

    /// <summary>
    /// L'economia del turno di chiunque sia — giocatore o mostro.
    ///
    /// Cerca in entrambi i dizionari perché un orso gufo ha un'economia del
    /// turno come tutti: è il profilo a dire quante Azioni esistono, non la
    /// natura della creatura. Prima guardava solo fra i giocatori, e costruire
    /// la scheda di un mostro lanciava <c>KeyNotFoundException</c> — motivo per
    /// cui il bestiario non riusciva a mostrarne una.
    ///
    /// Che poi il Master la usi o si tenga il conto sul foglio è affar suo: il
    /// motore la offre, non la impone.
    /// </summary>
    private TurnState Turn(string characterId)
    {
        if (!_turns.TryGetValue(characterId, out var turn))
        {
            var owner = _characters.GetValueOrDefault(characterId)
                     ?? _monsters.GetValueOrDefault(characterId)
                     ?? throw new KeyNotFoundException($"'{characterId}' non è né un personaggio né un mostro.");

            turn = new TurnState(owner.EffectiveTurnLimits(_profile));
            _turns[characterId] = turn;
        }
        return turn;
    }

    /// <summary>
    /// Rimpiazza la lista dell'iniziativa.
    ///
    /// L'ordine lo decide il server, non il client: "chi ha tirato più alto va
    /// prima" è una regola, e ordinare qui vuol dire che due console aperte non
    /// possono mostrare due ordini diversi. A parità di numero vince chi è
    /// stato aggiunto prima — <see cref="Enumerable.OrderByDescending{T,K}"/> è
    /// stabile, quindi non serve altro: al tavolo il pareggio lo scioglie il
    /// Master decidendo l'ordine in cui li inserisce.
    ///
    /// Un <paramref name="activeId"/> che non è più in lista non vuol dire
    /// niente e viene azzerato: capita ogni volta che si toglie chi era di
    /// turno, ed è meglio "nessuno di turno" che un puntatore a un morto.
    /// </summary>
    public void UpdateInitiative(List<InitiativeEntry> entries, string? activeId)
    {
        lock (_gate)
        {
            // Lo stesso RefId due volte sarebbe la stessa creatura in due punti
            // del giro: l'ultimo inserito vince, così ri-aggiungere qualcuno ne
            // aggiorna il numero invece di sdoppiarlo.
            var deduped = entries
                .GroupBy(e => e.RefId)
                .Select(g => g.Last());

            _initiative.Order = [.. deduped.OrderByDescending(e => e.Initiative)];
            _initiative.ActiveId = _initiative.Order.Any(e => e.RefId == activeId) ? activeId : null;
        }
    }

    /// <summary>
    /// Passa a chi viene dopo, e ricomincia dall'alto quando il giro finisce.
    ///
    /// <strong>Non tocca le risorse di nessuno</strong>, ed è una scelta, non
    /// una dimenticanza. L'iniziativa è un promemoria di *chi tocca*: dice, non
    /// impone. Il turno del giocatore resta suo — è lui che lo chiude col
    /// pulsante sulla scheda, quando ha finito davvero.
    ///
    /// Prima ricaricavo qui le risorse di chi entrava in turno, e legava due
    /// cose che devono restare indipendenti: il Master che sposta il segnalino
    /// non può azzerare l'Azione di chi non ha ancora agito, e un giro
    /// d'iniziativa saltato o corretto a mano non deve avere effetti
    /// collaterali sulle schede. Al giocatore arriva la notifica che tocca a
    /// lui; cosa farne è affar suo.
    /// </summary>
    public void NextTurn()
    {
        lock (_gate)
        {
            if (_initiative.Order.Count == 0) return;

            int idx = _initiative.ActiveId is not null
                ? _initiative.Order.FindIndex(e => e.RefId == _initiative.ActiveId)
                : -1;
            idx = (idx + 1) % _initiative.Order.Count;

            _initiative.ActiveId = _initiative.Order[idx].RefId;

            // Comincia un turno — di chiunque sia — e "una volta per turno"
            // vuol dire esattamente questo.
            StartOfTurnForEveryone();
        }
    }

    /// <summary>
    /// Ricarica le feature "una volta per turno" di <em>tutti</em>, a ogni
    /// turno che comincia.
    ///
    /// È la regola alla lettera, ed è il motivo per cui D&amp;D dice "una volta
    /// per turno" e non "una volta per round": in un round ci sono N turni, e
    /// chi sa agire nel turno altrui — con una reazione, o un'azione preparata
    /// — la capacità la riusa. L'Attacco Furtivo del Ladro è il caso di
    /// scuola: uno nel proprio turno, uno con l'attacco di opportunità nel
    /// turno di un altro. Due per round, e sono legali (Crawford: «Sneak Attack
    /// can occur once per turn, so it can potentially occur more than once in a
    /// round»). Prima si ricaricava solo col pulsante "Termina Turno" del
    /// giocatore, cioè una volta per round: proprio l'errore che questo
    /// progetto rimprovera a Roll20 e D&amp;D Beyond.
    ///
    /// <strong>Non tocca l'economia del turno</strong>: Azione, Azione Bonus e
    /// Reazione restano del giocatore, che apre e chiude il suo turno da solo.
    /// Il segnalino del Master dice di chi è il turno, non comanda le schede —
    /// e questo resta vero. Qui si ricarica solo ciò che il gioco definisce
    /// *per turno*, che è una cosa diversa da *quello che puoi fare adesso*.
    /// </summary>
    private void StartOfTurnForEveryone()
    {
        foreach (var c in _characters.Values)
            c.StartNewTurn(_profile);
        foreach (var m in _monsters.Values)
            m.StartNewTurn(_profile);
    }

    private static object ProfileDto(GameProfile p) => new
    {
        id = p.Id,
        name = p.Name,
        turnResources = p.TurnResources.Select(t => new { id = t.Id, label = t.Label, perTurn = t.PerTurn }),
        pools = p.Pools.Select(pool => new { id = pool.Id, label = pool.Label, kind = pool.Kind.ToString(), rechargeCycle = pool.RechargeCycle }),
        cycles = p.Cycles.Select(c => new { id = c.Id, label = c.Label, rank = c.Rank, perTurn = c.PerTurn }),
        exclusiveSlots = p.ExclusiveSlots.Select(s => new { id = s.Id, label = s.Label }),
        stats = p.Stats.Select(s => new { id = s.Id, label = s.Label, @default = s.Default, roll = s.Roll }),
    };

    /// <summary>Le statistiche dichiarate dal profilo attivo, coi loro default.</summary>
    public IReadOnlyList<StatDef> ProfileStats()
    {
        lock (_gate)
            return [.. _profile.Stats];
    }

    private object BuildDto(Character c) => new
    {
        id = c.Id,
        name = c.Name,
        // PF massimi e statistiche mostrano i valori EFFETTIVI (base + effetti attivi).
        hp = new { current = c.CurrentHp, max = c.EffectiveMaxHp() },
        customStats = c.EffectiveStats(),
        // Il modificatore, per le sole statistiche che ne hanno uno. È il
        // numero che il giocatore usa davvero: "Forza 18" da solo non gli dice
        // cosa sommare al d20, e finora la scheda non glielo diceva affatto.
        statModifiers = ModifiersFor(c),
        // Riserve del personaggio, etichettate col profilo: ogni pool porta i
        // suoi livelli (o il solo livello 0 per i pool a punti).
        pools = c.Resources.Snapshot().ToDictionary(
            pool => pool.Key,
            pool => new
            {
                label = _profile.Pool(pool.Key)?.Label ?? pool.Key,
                kind = (_profile.Pool(pool.Key)?.Kind ?? PoolKind.Leveled).ToString(),
                tiers = pool.Value.ToDictionary(
                    t => t.Key.ToString(),
                    t => new { max = t.Value.Max, remaining = t.Value.Remaining }),
            }),
        inventory = c.Inventory.Select(i => new
        {
            id = i.Id,
            name = i.Name,
            quantity = i.Quantity,
            notes = i.Notes,
        }),
        // Economia del turno come dizionario risorsa → residui (dal profilo).
        turn = Turn(c.Id).Snapshot(),
        turnMax = c.EffectiveTurnLimits(_profile),
        // Slot esclusivi occupati, coi nomi già pronti da leggere: la scheda
        // mostra "Concentrazione: Benedizione", non due id da incrociare.
        occupied = _profile.ExclusiveSlots
            .Select(s => new { slot = s.Id, label = s.Label, feature = c.Occupant(s.Id) })
            .Where(x => x.feature is not null)
            .Select(x => new { x.slot, x.label, featureId = x.feature!.Id, featureName = x.feature.ShortName }),
        sources = c.Sources.Select(s => new
        {
            id = s.Id,
            name = s.Name,
            type = s.Type.ToString(),
            parentSourceId = s.ParentSourceId,
        }),
        // Payload "collassato": nome breve, costo, contatori. Il testo
        // esplicativo NON è incluso — è lazy-loaded (Capitolo 7).
        features = c.Features.Select(f => BuildFeatureDto(c, f)),
    };

    /// <summary>
    /// Proiezione di una feature, con il verdetto del motore già dentro.
    ///
    /// La validazione viaggia nello snapshot perché il motivo del rifiuto esiste
    /// già — <see cref="RuleEngine.Validate"/> lo produce per esteso — ma finora
    /// raggiungeva il giocatore solo DOPO il tap, come toast, mentre il client
    /// riduceva la stessa domanda a un booleano calcolato per conto suo. Erano
    /// due implementazioni della stessa regola, e quella visibile era la muta.
    /// Qui il verdetto è uno solo e arriva prima (Capitolo 8: il client non
    /// calcola meccaniche).
    ///
    /// Il costo è trascurabile: <c>Validate</c> è puro, un paio di lookup, e
    /// questo DTO già calcola EffectiveMaxHp/EffectiveStats, che costano di più.
    /// </summary>
    /// <summary>
    /// La formula coi punteggi dentro, o null se non c'è o non si risolve.
    /// Una formula rotta non è un caso da gestire qui: la scheda mostra quella
    /// grezza, e il rifiuto vero arriva quando si prova a tirarla.
    /// </summary>
    private string? RollLabel(Character c, string? roll)
    {
        if (string.IsNullOrWhiteSpace(roll)) return null;
        return DiceFormula.TryParse(roll, out var formula, out _)
            ? formula!.Describe(StatsFor(c))
            : null;
    }

    private object BuildFeatureDto(Character c, Feature f)
    {
        var verdict = _engine.Validate(c, f, Turn(c.Id), _profile);
        return new
        {
            id = f.Id,
            shortName = f.ShortName,
            sourceIds = f.SourceIds,
            // Lista: una magia costa l'Azione e lo slot.
            costs = f.Costs.Select(c => new { kind = c.Kind, amount = c.Amount }),
            // Il rovescio: cosa ti restituisce (Action Surge, Attacco Extra).
            grants = f.Grants.Select(g => new { kind = g.Kind, amount = g.Amount }),
            // Lo slot esclusivo che occuperebbe, e chi ne cadrebbe fuori: la
            // scheda lo dice *prima*, perché scoprire di aver perso Benedizione
            // dopo aver speso lo slot è tardi.
            occupies = f.Occupies,
            wouldReplace = _profile.Exclusive(f.Occupies) is { } slot
                && c.Occupant(slot.Id) is { } occupante
                && occupante.Id != f.Id
                    ? occupante.ShortName
                    : null,
            usage = f.Usage is null ? null : new
            {
                max = f.Usage.MaxUses,
                remaining = f.Usage.RemainingUses,
                recharge = f.Usage.Recharge,
            },
            descriptionId = f.DescriptionId,
            // Effetti e stato on/off: la scheda mostra "Forza +2", "CA 15", e per
            // gli oggetti indossabili un interruttore.
            effects = f.Effects.Select(e => new { target = e.Target, op = e.Op.ToString(), value = e.Value }),
            toggleable = f.Toggleable,
            active = c.IsEffectFeatureActive(f),
            customData = f.CustomData,
            // La formula: sulla scheda è il bersaglio da toccare per tirare.
            roll = f.Roll,
            // La stessa formula coi punteggi già dentro ("1d20+6"): è quella che
            // il giocatore legge sul bottone. La risolve il motore, con le stesse
            // statistiche del tiro vero — un secondo risolutore nel client
            // sarebbe la stessa regola scritta due volte, e prima o poi una
            // delle due mente.
            rollLabel = RollLabel(c, f.Roll),
            // Verdetto del motore: se no, perché. Null quando si può.
            canUse = verdict.IsValid,
            blockedReason = verdict.Reason,
        };
    }
}
