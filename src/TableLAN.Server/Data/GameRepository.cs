namespace TableLAN.Server.Data;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TableLAN.Core.Characters;
using TableLAN.Core.Features;
using TableLAN.Core.Profile;
using TableLAN.Core.Sources;

/// <summary>
/// Traduce tra le righe SQLite e il modello di dominio del Core.
/// Il Core resta ignaro della persistenza; il repository resta ignaro
/// delle regole.
/// </summary>
public sealed class GameRepository(IDbContextFactory<AppDb> dbFactory)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Le sole schede dei giocatori.
    ///
    /// <c>Characters</c> è una tabella TPH: i mostri del bestiario sono righe
    /// della stessa tabella (<see cref="MonsterRow"/>), e il DbSet base le
    /// restituisce <em>tutte</em>. Le rotte dei mostri filtrano già con
    /// <c>OfType&lt;MonsterRow&gt;()</c>; senza il filtro speculare, il
    /// bestiario del Master finiva nello snapshot di <c>/api/state</c> — che è
    /// una rotta LAN, non loopback — cioè sui telefoni dei giocatori, con nome
    /// e PF. E siccome <c>GameStateService</c> si popola da qui, i mostri erano
    /// anche indirizzabili da <c>/api/characters/{id}/*</c>: un giocatore
    /// poteva curare l'orso.
    ///
    /// Non se n'era accorto nessuno perché il bestiario era sempre vuoto: il
    /// primo mostro creato è stato anche il primo a fuggire.
    /// </summary>
    private static IQueryable<CharacterRow> Players(AppDb db) =>
        db.Characters.Where(c => !(c is MonsterRow));

    public async Task<List<Character>> LoadCharactersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var sources = await db.Sources.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var features = await db.Features.AsNoTracking().ToDictionaryAsync(f => f.Id);
        var rows = await Players(db).AsNoTracking().ToListAsync();

        return rows.Select(row => ToDomain(row, sources, features)).ToList();
    }

    /// <summary>
    /// L'etichetta della statistica "classe armatura" secondo il profilo attivo.
    ///
    /// Si aggancia al <strong>codice interno</strong> <c>ca</c>, non al prefisso
    /// dell'etichetta. Il prefisso era un disastro silenzioso: le statistiche di
    /// D&amp;D in italiano sono Forza, Destrezza, Costituzione, Intelligenza,
    /// Saggezza, <em>Carisma</em>, CA — e <c>"Carisma".StartsWith("CA")</c> è
    /// vero, quindi ogni mostro creato si vedeva scrivere la Classe Armatura
    /// dentro il Carisma. Con lo stesso bug in lettura, il che lo rendeva
    /// coerente e invisibile.
    ///
    /// L'id è la chiave stabile — è dichiarato apposta perché rinominare
    /// un'etichetta non rompa niente. Indovinare dal nome mostrato è
    /// esattamente ciò che l'id esiste per evitare.
    /// </summary>
    private static string ArmorStatLabel(GameProfile? profile) =>
        profile?.Stat("ca")?.Label ?? "CA";

    /// <summary>
    /// I mostri del bestiario, come personaggi di dominio.
    ///
    /// Sono <see cref="Character"/> a tutti gli effetti — è il senso della
    /// tabella TPH: un orso gufo ha PF, CA, capacità e tiri come un PG, e il
    /// motore non ha ragione di trattarlo diversamente. La differenza non è di
    /// natura, è di **confine**: escono da <c>/api/admin/*</c>, che risponde
    /// solo su loopback, e mai dallo snapshot LAN.
    /// </summary>
    public async Task<List<Character>> LoadMonstersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var sources = await db.Sources.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var features = await db.Features.AsNoTracking().ToDictionaryAsync(f => f.Id);
        var rows = await db.Characters.OfType<MonsterRow>().AsNoTracking().OrderBy(m => m.Name).ToListAsync();

        return rows.Select(row => ToDomain(row, sources, features)).ToList();
    }

    /// <summary>
    /// Le note libere dei mostri (il blocco statistico), mappa id→testo. Vivono
    /// solo su <see cref="MonsterRow"/> e non nel dominio puro, che non le
    /// conosce: il DTO le riattacca per id. Solo le voci non vuote — un mostro
    /// senza note non compare.
    /// </summary>
    public async Task<Dictionary<string, string>> LoadMonsterNotesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Characters.OfType<MonsterRow>().AsNoTracking()
            .Where(m => m.Notes != "")
            .ToDictionaryAsync(m => m.Id, m => m.Notes);
    }

    public async Task<Character?> LoadCharacterAsync(string characterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await Players(db).AsNoTracking().FirstOrDefaultAsync(c => c.Id == characterId);
        if (row is null)
            return null;

        var sources = await db.Sources.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var features = await db.Features.AsNoTracking().ToDictionaryAsync(f => f.Id);
        return ToDomain(row, sources, features);
    }

    public async Task<string?> LoadDescriptionAsync(string descriptionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.SharedTexts.FindAsync(descriptionId);
        return row?.Text;
    }

    // ---- Profilo di sistema (Capitolo 7/10) ----

    public async Task<GameProfile?> LoadProfileAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.Profiles.FindAsync("active");
        return row is null ? null : JsonSerializer.Deserialize<GameProfile>(row.Json, JsonOpts);
    }

    public async Task SaveProfileAsync(GameProfile profile)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var json = JsonSerializer.Serialize(profile, JsonOpts);
        var row = await db.Profiles.FindAsync("active");
        if (row is null)
            db.Profiles.Add(new ProfileRow { Id = "active", Json = json });
        else
            row.Json = json;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Scrive su disco l'intento validato e lo stato aggiornato del
    /// personaggio. Chiamato PRIMA del broadcast SignalR (Capitolo 11).
    /// </summary>
    public async Task PersistIntentAsync(Character character, string featureId, int? slotLevelUsed)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        db.IntentLog.Add(new IntentLogRow
        {
            CharacterId = character.Id,
            FeatureId = featureId,
            SlotLevelUsed = slotLevelUsed,
            TimestampUtc = DateTime.UtcNow,
        });

        await SyncCharacterRowAsync(db, character);
        await db.SaveChangesAsync();
    }

    public async Task SaveCharacterAsync(Character character)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await SyncCharacterRowAsync(db, character);
        await db.SaveChangesAsync();
    }

    // ---- Authoring (Capitolo 10): il Master aggiunge mutazioni, oggetti,
    // magie sulle schede; il giocatore può auto-aggiungere le proprie. ----

    /// <summary>
    /// Crea una scheda. Nasce senza Fonti e senza feature — coerente col
    /// Capitolo 7: un personaggio *è* la sua lista di Fonti, e all'inizio quella
    /// lista è vuota.
    ///
    /// Le statistiche invece ci sono già, prese dal profilo coi loro valori di
    /// default: sono del sistema, non del personaggio. Prima ogni scheda doveva
    /// reinventarsele digitando i nomi a mano, una per una.
    /// </summary>
    public async Task<string> AddCharacterAsync(string name, int maxHp, IReadOnlyList<StatDef> profileStats)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var stats = profileStats.ToDictionary(s => s.Label, s => (object?)s.Default);
        var rolls = profileStats
            .Where(s => !string.IsNullOrWhiteSpace(s.Roll))
            .ToDictionary(s => s.Label, s => s.Roll!);

        var id = "pg-" + Guid.NewGuid().ToString("N");
        db.Characters.Add(new CharacterRow
        {
            Id = id,
            Name = name,
            MaxHp = maxHp,
            CurrentHp = maxHp,
            SourceIdsJson = "[]",
            FeatureIdsJson = "[]",
            CustomStatsJson = JsonSerializer.Serialize(stats, JsonOpts),
            StatRollsJson = JsonSerializer.Serialize(rolls, JsonOpts),
        });

        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Rimuove una scheda e le feature che le appartengono in esclusiva.
    ///
    /// Le feature sono un catalogo condiviso: una concessa anche a un altro
    /// personaggio resta: cancellarla svuoterebbe la scheda di qualcun altro.
    /// Le Fonti restano comunque (sono economiche, e riferite per id altrove).
    /// </summary>
    public async Task RemoveCharacterAsync(string characterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Players() e non FindAsync: sulla base TPH FindAsync trova anche i
        // mostri, e questa è la rotta *dei giocatori*. Finché il bestiario
        // filtrava nello snapshot, un mostro compariva fra i personaggi con
        // accanto il suo pulsante "elimina" — ed è così che un Goblin è stato
        // cancellato per davvero. Il guard sulla rotta ora regge, ma la difesa
        // sta bene anche qui: una rotta futura non deve poter far rinascere il
        // bug. Per i mostri c'è DeleteMonsterAsync.
        var row = await Players(db).FirstOrDefaultAsync(c => c.Id == characterId);
        if (row is null)
            return;

        var mine = JsonSerializer.Deserialize<List<string>>(row.FeatureIdsJson, JsonOpts) ?? [];
        var others = await db.Characters
            .Where(c => c.Id != characterId)
            .Select(c => c.FeatureIdsJson)
            .ToListAsync();

        var stillUsed = others
            .SelectMany(json => JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [])
            .ToHashSet();

        var orphaned = mine.Where(id => !stillUsed.Contains(id)).ToList();
        db.Features.RemoveRange(db.Features.Where(f => orphaned.Contains(f.Id)));
        db.Characters.Remove(row);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Aggiunge una Fonte alla scheda del personaggio (es. una mutazione da
    /// contaminazione, un oggetto magico, una sottoclasse). Restituisce l'id
    /// della Fonte creata.
    /// </summary>
    public async Task<string> AddSourceAsync(string characterId, string name, string type, string? parentSourceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var character = await RequireCharacterRowAsync(db, characterId);

        var id = "src-" + Guid.NewGuid().ToString("N");
        db.Sources.Add(new SourceRow
        {
            Id = id,
            Name = name,
            Type = NormalizeSourceType(type),
            ParentSourceId = parentSourceId,
        });

        character.SourceIdsJson = AppendId(character.SourceIdsJson, id);
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Aggiunge una feature (abilità, tratto, magia) alla scheda. Il costo è
    /// solo un dato: una "magia" con costo Azione o Nessuno non richiede che
    /// il personaggio sia un caster — così un non-caster può ricevere magie
    /// da oggetti o feat senza gli hack di D&D Beyond.
    /// </summary>
    public async Task<string> AddFeatureAsync(string characterId, FeatureDraft draft)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var character = await RequireCharacterRowAsync(db, characterId);

        var descriptionId = string.Empty;
        if (!string.IsNullOrWhiteSpace(draft.Description))
        {
            descriptionId = "txt-" + Guid.NewGuid().ToString("N");
            db.SharedTexts.Add(new SharedTextRow { Id = descriptionId, Text = draft.Description.Trim() });
        }

        // Se non è indicata una Fonte, la feature finisce nel raccoglitore
        // "Aggiunte" del personaggio (una Fonte homebrew per scheda).
        var sourceId = !string.IsNullOrWhiteSpace(draft.SourceId)
            ? draft.SourceId!
            : await GetOrCreateAdditionsSourceAsync(db, character);

        var id = "ft-" + Guid.NewGuid().ToString("N");
        db.Features.Add(new FeatureRow
        {
            Id = id,
            ShortName = draft.ShortName,
            CostsJson = SerializeCosts(draft.Costs),
            GrantsJson = SerializeCosts(draft.Grants),
            Occupies = Trimmed(draft.Occupies),
            MaxUses = draft.MaxUses,
            RemainingUses = draft.MaxUses,
            Recharge = draft.MaxUses is null ? null : NormalizeRecharge(draft.Recharge),
            DescriptionId = descriptionId,
            SourceIdsJson = JsonSerializer.Serialize(new[] { sourceId }),
            CustomJson = string.IsNullOrWhiteSpace(draft.CustomJson) ? "{}" : draft.CustomJson!,
            EffectsJson = JsonSerializer.Serialize(draft.Effects ?? [], JsonOpts),
            Toggleable = draft.Toggleable,
            Roll = string.IsNullOrWhiteSpace(draft.Roll) ? null : draft.Roll!.Trim(),
        });

        character.FeatureIdsJson = AppendId(character.FeatureIdsJson, id);
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Imposta (o cancella, con null) la formula di tiro di una feature.</summary>
    /// <summary>
    /// Modifica una feature esistente: nome, costo, usi, ricarica, tiro,
    /// descrizione, effetti.
    ///
    /// Una sola rotta invece delle tre parziali di prima (costo, tiro, e nient'
    /// altro): "cambiare l'Attacco Furtivo" non è tre operazioni diverse, è
    /// una. E ciò che non si poteva cambiare — nome, usi, ricarica, testo —
    /// obbligava a cancellare e rifare.
    ///
    /// Gli usi residui si conservano se il massimo non cambia: modificare la
    /// descrizione non deve ricaricare un potere già speso. Se il massimo
    /// cambia, si taglia al nuovo tetto.
    /// </summary>
    public async Task UpdateFeatureAsync(string featureId, FeatureDraft draft)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var feature = await db.Features.FindAsync(featureId);
        if (feature is null)
            return;

        feature.ShortName = draft.ShortName.Trim();
        feature.CostsJson = SerializeCosts(draft.Costs);
        feature.GrantsJson = SerializeCosts(draft.Grants);
        feature.Occupies = Trimmed(draft.Occupies);
        feature.Roll = string.IsNullOrWhiteSpace(draft.Roll) ? null : draft.Roll!.Trim();
        feature.Toggleable = draft.Toggleable;
        feature.EffectsJson = JsonSerializer.Serialize(draft.Effects ?? [], JsonOpts);

        if (draft.MaxUses is int max)
        {
            var wasUnlimited = feature.MaxUses is null;
            feature.RemainingUses = wasUnlimited || feature.MaxUses != max
                ? max
                : Math.Min(feature.RemainingUses ?? max, max);
            feature.MaxUses = max;
            feature.Recharge = NormalizeRecharge(draft.Recharge);
        }
        else
        {
            feature.MaxUses = null;
            feature.RemainingUses = null;
            feature.Recharge = null;
        }

        await SetDescriptionAsync(db, feature, draft.Description);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Aggiorna il testo condiviso della feature, creandolo se non c'era.
    /// Il testo è deduplicato: la riga si tocca solo se questa feature è la
    /// sola a puntarci — altrimenti se ne crea una sua, o si rischia di
    /// riscrivere la descrizione anche a chi condivide il puntatore.
    /// </summary>
    private static async Task SetDescriptionAsync(AppDb db, FeatureRow feature, string? description)
    {
        var text = description?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            feature.DescriptionId = string.Empty;
            return;
        }

        var sharedWith = await db.Features
            .CountAsync(f => f.DescriptionId == feature.DescriptionId && f.Id != feature.Id);

        if (!string.IsNullOrEmpty(feature.DescriptionId) && sharedWith == 0
            && await db.SharedTexts.FindAsync(feature.DescriptionId) is { } row)
        {
            row.Text = text;
            return;
        }

        var id = "txt-" + Guid.NewGuid().ToString("N");
        db.SharedTexts.Add(new SharedTextRow { Id = id, Text = text });
        feature.DescriptionId = id;
    }

    /// <summary>
    /// Allinea le schede alle statistiche del profilo.
    ///
    /// Se il sistema dice che esistono Forza…Carisma, allora ogni scheda le ha:
    /// non è una proprietà del personaggio, è una proprietà del gioco. Le schede
    /// nate prima non le avrebbero mai, e "tutti hanno esattamente quei valori"
    /// sarebbe falso.
    ///
    /// Aggiunge solo ciò che manca, col valore di default, e non tocca mai un
    /// valore già scritto. Le statistiche in più non si cancellano: possono
    /// essere homebrew di quel personaggio, e cancellare dati del Master per
    /// zelo di coerenza è un pessimo affare — la console le mostra a parte.
    /// </summary>
    public async Task SyncCharactersWithProfileAsync(GameProfile profile)
    {
        if (profile.Stats.Count == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Characters.ToListAsync();
        var touched = false;

        foreach (var row in rows)
        {
            var stats = JsonSerializer.Deserialize<Dictionary<string, object?>>(row.CustomStatsJson, JsonOpts) ?? [];
            var rolls = JsonSerializer.Deserialize<Dictionary<string, string>>(row.StatRollsJson, JsonOpts) ?? [];
            var changed = false;

            foreach (var stat in profile.Stats)
            {
                if (!stats.ContainsKey(stat.Label))
                {
                    stats[stat.Label] = stat.Default;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(stat.Roll) && !rolls.ContainsKey(stat.Label))
                {
                    rolls[stat.Label] = stat.Roll!;
                    changed = true;
                }
            }

            if (!changed)
                continue;

            row.CustomStatsJson = JsonSerializer.Serialize(stats, JsonOpts);
            row.StatRollsJson = JsonSerializer.Serialize(rolls, JsonOpts);
            touched = true;
        }

        if (touched)
            await db.SaveChangesAsync();
    }

    /// <summary>Rinomina una scheda e/o ne cambia i PF massimi.</summary>
    public async Task RenameCharacterAsync(string characterId, string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Characters.FindAsync(characterId) is not { } row)
            return;
        row.Name = name;
        await db.SaveChangesAsync();
    }

    /// <summary>Rimuove una statistica dalla scheda (per le homebrew di troppo).</summary>
    public async Task RemoveStatAsync(string characterId, string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.Characters.FindAsync(characterId);
        if (row is null)
            return;

        var stats = JsonSerializer.Deserialize<Dictionary<string, object?>>(row.CustomStatsJson, JsonOpts) ?? [];
        var rolls = JsonSerializer.Deserialize<Dictionary<string, string>>(row.StatRollsJson, JsonOpts) ?? [];
        stats.Remove(name);
        rolls.Remove(name);
        row.CustomStatsJson = JsonSerializer.Serialize(stats, JsonOpts);
        row.StatRollsJson = JsonSerializer.Serialize(rolls, JsonOpts);
        await db.SaveChangesAsync();
    }

    /// <summary>Rimuove una feature dalla scheda (la riga di catalogo resta).</summary>
    public async Task RemoveFeatureAsync(string characterId, string featureId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var character = await RequireCharacterRowAsync(db, characterId);
        character.FeatureIdsJson = RemoveId(character.FeatureIdsJson, featureId);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Imposta una statistica dinamica homebrew (es. "Contaminazione",
    /// "Sanità Mentale"). Valore null la rimuove.
    /// </summary>
    public async Task SetCustomStatAsync(string characterId, string name, object? value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var character = await RequireCharacterRowAsync(db, characterId);

        var stats = JsonSerializer.Deserialize<Dictionary<string, object?>>(character.CustomStatsJson, JsonOpts) ?? [];
        if (value is null)
            stats.Remove(name);
        else
            stats[name] = value;

        character.CustomStatsJson = JsonSerializer.Serialize(stats, JsonOpts);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Imposta un livello di una riserva; <paramref name="max"/> nullo o ≤ 0 lo
    /// rimuove. Livello 0 per i pool a punti (ki, mana), 1..N per gli slot.
    ///
    /// Le riserve erano l'unica parte della scheda che il motore consuma e che
    /// nessuna rotta sapeva scrivere: esistevano solo nel seed, quindi un
    /// incantatore creato al tavolo nasceva senza slot e non lanciava nulla.
    /// </summary>
    public async Task SetPoolTierAsync(string characterId, string poolId, int level, int? max, int? remaining)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var character = await RequireCharacterRowAsync(db, characterId);

        var pools = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int[]>>>(
            character.ResourcesJson, JsonOpts) ?? [];

        if (max is null or <= 0)
        {
            if (pools.TryGetValue(poolId, out var existing))
            {
                existing.Remove(level.ToString());
                if (existing.Count == 0) pools.Remove(poolId);
            }
        }
        else
        {
            if (!pools.TryGetValue(poolId, out var tiers))
                pools[poolId] = tiers = [];
            tiers[level.ToString()] = [max.Value, Math.Clamp(remaining ?? max.Value, 0, max.Value)];
        }

        character.ResourcesJson = JsonSerializer.Serialize(pools, JsonOpts);
        await db.SaveChangesAsync();
    }

    // ---- Bestiario del Master (Capitolo 12): solo lato loopback. ----

    public async Task<List<MonsterRow>> ListMonstersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Characters.OfType<MonsterRow>().AsNoTracking().OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<string> AddMonsterAsync(string name, int maxHp, int armorClass, string? notes)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        
        // Carichiamo il profilo per inizializzare le statistiche come i PG
        var profileRow = await db.Profiles.FindAsync("active");
        var profile = profileRow is not null ? JsonSerializer.Deserialize<GameProfile>(profileRow.Json, JsonOpts) : null;
        
        var stats = profile?.Stats.ToDictionary(s => s.Label, s => (object?)s.Default) ?? new Dictionary<string, object?>();
        var rolls = profile?.Stats
            .Where(s => !string.IsNullOrWhiteSpace(s.Roll))
            .ToDictionary(s => s.Label, s => s.Roll!) ?? new Dictionary<string, string>();

        stats[ArmorStatLabel(profile)] = armorClass;

        var id = "mon-" + Guid.NewGuid().ToString("N");
        db.Characters.Add(new MonsterRow
        {
            Id = id,
            Name = name,
            MaxHp = maxHp,
            CurrentHp = maxHp,
            Notes = notes ?? string.Empty,
            SourceIdsJson = "[]",
            FeatureIdsJson = "[]",
            CustomStatsJson = JsonSerializer.Serialize(stats, JsonOpts),
            StatRollsJson = JsonSerializer.Serialize(rolls, JsonOpts),
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Sdoppia un mostro: una nuova istanza con PF pieni e un nome numerato
    /// (<c>Goblin</c> → <c>Goblin (2)</c>). È il caso di ogni scontro con più
    /// nemici uguali — tre goblin con PF separati, senza rifarli a mano. Copia
    /// il blocco (PF, CA, statistiche, note); le feature restano vuote perché
    /// sono entità a parte e condividerne i riferimenti legherebbe le copie.
    /// </summary>
    public async Task<string?> DuplicateMonsterAsync(string monsterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var src = await db.Characters.OfType<MonsterRow>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == monsterId);
        if (src is null)
            return null;

        var names = await db.Characters.OfType<MonsterRow>().Select(m => m.Name).ToListAsync();
        var id = "mon-" + Guid.NewGuid().ToString("N");
        db.Characters.Add(new MonsterRow
        {
            Id = id,
            Name = NextInstanceName(src.Name, names),
            MaxHp = src.MaxHp,
            CurrentHp = src.MaxHp,      // istanza fresca: PF pieni, non quelli scalati del primo
            Notes = src.Notes,
            SourceIdsJson = src.SourceIdsJson,
            FeatureIdsJson = "[]",
            CustomStatsJson = src.CustomStatsJson,
            StatRollsJson = src.StatRollsJson,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Il prossimo nome libero della serie: «Goblin» / «Goblin (2)» → «Goblin (3)».</summary>
    private static string NextInstanceName(string name, IEnumerable<string> existing)
    {
        var root = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\(\d+\)\s*$", "").Trim();
        var rx = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(root) + @"(?:\s*\((\d+)\))?$");
        var max = 1;
        foreach (var n in existing)
        {
            var m = rx.Match((n ?? string.Empty).Trim());
            if (!m.Success) continue;
            var k = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 1;
            if (k > max) max = k;
        }
        return $"{root} ({max + 1})";
    }

    public async Task AdjustMonsterHpAsync(string monsterId, int delta)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var monster = await db.Characters.OfType<MonsterRow>().FirstOrDefaultAsync(m => m.Id == monsterId);
        if (monster is null)
            return;
        monster.CurrentHp = Math.Clamp(monster.CurrentHp + delta, 0, monster.MaxHp);
        await db.SaveChangesAsync();
    }

    public async Task DeleteMonsterAsync(string monsterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var monster = await db.Characters.OfType<MonsterRow>().FirstOrDefaultAsync(m => m.Id == monsterId);
        if (monster is null)
            return;
        db.Characters.Remove(monster);
        await db.SaveChangesAsync();
    }

    private static async Task<CharacterRow> RequireCharacterRowAsync(AppDb db, string characterId) =>
        await db.Characters.FindAsync(characterId)
            ?? throw new KeyNotFoundException($"Personaggio '{characterId}' non trovato.");

    private static async Task<string> GetOrCreateAdditionsSourceAsync(AppDb db, CharacterRow character)
    {
        var ids = JsonSerializer.Deserialize<List<string>>(character.SourceIdsJson, JsonOpts) ?? [];
        var existing = await db.Sources
            .FirstOrDefaultAsync(s => ids.Contains(s.Id) && s.Type == nameof(SourceType.Homebrew) && s.Name == "Aggiunte");
        if (existing is not null)
            return existing.Id;

        var id = "src-" + Guid.NewGuid().ToString("N");
        db.Sources.Add(new SourceRow { Id = id, Name = "Aggiunte", Type = nameof(SourceType.Homebrew) });
        character.SourceIdsJson = AppendId(character.SourceIdsJson, id);
        return id;
    }

    private static string AppendId(string json, string id)
    {
        var ids = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        if (!ids.Contains(id))
            ids.Add(id);
        return JsonSerializer.Serialize(ids);
    }

    private static string RemoveId(string json, string id)
    {
        var ids = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        ids.Remove(id);
        return JsonSerializer.Serialize(ids);
    }

    private static string NormalizeSourceType(string type) =>
        Enum.TryParse<SourceType>(type, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : nameof(SourceType.Homebrew);

    // Costo e ciclo di ricarica sono id-stringa governati dal profilo di
    // sistema: qui li si accetta così come sono, con un default sensato se
    // mancanti. La coerenza col profilo la valida il motore a runtime.
    /// <summary>
    /// Serializza i costi, scartando i "None": una lista che contiene "nessun
    /// costo" insieme a un costo vero non vuol dire niente, e l'interfaccia usa
    /// "None" come voce di menù per dire "togli questo costo".
    /// </summary>
    private static string SerializeCosts(IReadOnlyList<ActivationCost>? costs) =>
        JsonSerializer.Serialize(
            (costs ?? []).Where(c => !c.IsNone).Select(c => new ActivationCost(c.Kind.Trim(), Math.Max(0, c.Amount))),
            JsonOpts);

    /// <summary>Stringa opzionale ripulita: il vuoto e il bianco valgono "niente".</summary>
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRecharge(string? recharge) =>
        string.IsNullOrWhiteSpace(recharge) ? "LongRest" : recharge.Trim();

    private static async Task SyncCharacterRowAsync(AppDb db, Character character)
    {
        var row = await db.Characters.FindAsync(character.Id)
            ?? throw new InvalidOperationException($"Personaggio '{character.Id}' non trovato su disco.");

        row.CurrentHp = character.CurrentHp;
        row.MaxHp = character.MaxHp;
        row.TempHp = character.TempHp;
        // Riserve multi-pool: { poolId: { livello: [max, residui] } }.
        row.ResourcesJson = JsonSerializer.Serialize(
            character.Resources.Snapshot().ToDictionary(
                pool => pool.Key,
                pool => pool.Value.ToDictionary(
                    tier => tier.Key.ToString(),
                    tier => new[] { tier.Value.Max, tier.Value.Remaining })),
            JsonOpts);
        row.CustomStatsJson = JsonSerializer.Serialize(character.CustomStats, JsonOpts);
        row.InventoryJson = JsonSerializer.Serialize(character.Inventory, JsonOpts);
        row.EffectStateJson = JsonSerializer.Serialize(character.EffectStates, JsonOpts);
        row.OccupiedJson = JsonSerializer.Serialize(character.Occupied, JsonOpts);

        // Contatori d'uso: le feature sono condivise nel catalogo, i residui
        // vengono aggiornati sulle righe feature corrispondenti.
        foreach (var feature in character.Features.Where(f => f.Usage is not null))
        {
            var featureRow = await db.Features.FindAsync(feature.Id);
            if (featureRow is not null)
                featureRow.RemainingUses = feature.Usage!.RemainingUses;
        }
    }

    private static Character ToDomain(
        CharacterRow row,
        IReadOnlyDictionary<string, SourceRow> sources,
        IReadOnlyDictionary<string, FeatureRow> features)
    {
        var sourceIds = JsonSerializer.Deserialize<List<string>>(row.SourceIdsJson, JsonOpts) ?? [];
        var featureIds = JsonSerializer.Deserialize<List<string>>(row.FeatureIdsJson, JsonOpts) ?? [];

        var character = new Character
        {
            Id = row.Id,
            Name = row.Name,
            MaxHp = row.MaxHp,
            CurrentHp = row.CurrentHp,
            TempHp = row.TempHp,
            Sources = sourceIds
                .Where(sources.ContainsKey)
                .Select(id => ToDomain(sources[id]))
                .ToList(),
            Features = featureIds
                .Where(features.ContainsKey)
                .Select(id => ToDomain(features[id]))
                .ToList(),
            CustomStats = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                row.CustomStatsJson, JsonOpts) ?? [],
            Inventory = JsonSerializer.Deserialize<List<InventoryItem>>(
                row.InventoryJson, JsonOpts) ?? [],
            EffectStates = JsonSerializer.Deserialize<Dictionary<string, bool>>(
                row.EffectStateJson, JsonOpts) ?? [],
            Occupied = JsonSerializer.Deserialize<Dictionary<string, string>>(
                row.OccupiedJson, JsonOpts) ?? [],
        };

        var pools = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int[]>>>(
            row.ResourcesJson, JsonOpts) ?? [];
        foreach (var (poolId, tiers) in pools)
            foreach (var (level, maxRemaining) in tiers)
                character.Resources.SetTier(poolId, int.Parse(level), maxRemaining[0], maxRemaining[1]);

        return character;
    }

    private static Source ToDomain(SourceRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Type = Enum.Parse<SourceType>(row.Type),
        ParentSourceId = row.ParentSourceId,
    };

    /// <summary>
    /// I costi di una feature: sono id-stringa interpretati dal profilo, e più
    /// d'uno (una magia costa l'Azione E lo slot). Se <c>CostsJson</c> è null la
    /// riga non è ancora passata dal backfill e si legge il vecchio campo
    /// singolo. Estratto da ToDomain di proposito: la stessa espressione inline
    /// (un condizionale annidato con collection expression dentro un object
    /// initializer) non si parsa su alcuni SDK .NET 8; qui è anche più chiara.
    /// </summary>
    private static List<ActivationCost> ParseCosts(FeatureRow row)
    {
        if (row.CostsJson is not null)
            return JsonSerializer.Deserialize<List<ActivationCost>>(row.CostsJson, JsonOpts) ?? [];

        if (row.CostKind is null or "" or ActivationCost.NoneKind)
            return [];

        return [new ActivationCost(row.CostKind, row.SlotLevel)];
    }

    private static Feature ToDomain(FeatureRow row) => new()
    {
        Id = row.Id,
        ShortName = row.ShortName,
        DescriptionId = row.DescriptionId,
        SourceIds = JsonSerializer.Deserialize<List<string>>(row.SourceIdsJson, JsonOpts) ?? [],
        Costs = ParseCosts(row),
        Grants = JsonSerializer.Deserialize<List<ActivationCost>>(row.GrantsJson, JsonOpts) ?? [],
        Occupies = row.Occupies,
        Usage = row.MaxUses is int max
            ? new UsageCounter
            {
                MaxUses = max,
                RemainingUses = row.RemainingUses ?? max,
                Recharge = row.Recharge ?? ActivationCost.NoneKind,
            }
            : null,
        Effects = JsonSerializer.Deserialize<List<Effect>>(row.EffectsJson, JsonOpts) ?? [],
        Toggleable = row.Toggleable,
        Roll = string.IsNullOrWhiteSpace(row.Roll) ? null : row.Roll,
        CustomData = JsonSerializer.Deserialize<Dictionary<string, object?>>(row.CustomJson, JsonOpts) ?? [],
    };
}
