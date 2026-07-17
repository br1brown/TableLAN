namespace TableLAN.Server.Data;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TableLAN.Core.Profile;

/// <summary>
/// Dati dimostrativi allineati ai due casi d'uso più difficili: un multiclasse
/// Ladro/Monaco — che porta insieme riserva a punti, capacità "una volta per
/// turno" e Fonti multiple — e un'incantatrice pura con slot a livelli e
/// upcasting. Servono a provare il motore su una scheda vera dal primo avvio.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Tiri dei personaggi dimostrativi, per id. Servono anche a chi ha già un
    /// database: il seed completo si ferma al primo avvio, quindi una capacità
    /// aggiunta dopo non arriverebbe mai alle schede di playtest esistenti —
    /// l'Attacco Furtivo resterebbe senza i suoi d6, che è esattamente il
    /// problema da risolvere.
    /// </summary>
    private static readonly (string FeatureId, string Roll)[] SeedRolls =
    [
        ("ft-attacco-furtivo", "1d6"),
        ("ft-dardo-incantato", "1d4+1"),
        ("ft-dardo-fuoco", "1d10"),
    ];

    public static async Task EnsureSeededAsync(AppDb db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureSingleFileAsync(db);
        await AddMissingColumnsAsync(db);
        await BackfillCostsAsync(db);
        await BackfillSpellActionCostAsync(db);
        await BackfillSeedRollsAsync(db);
        await BackfillProfileStatsAsync(db);

        // Profilo di sistema di default: D&D 5e. Indipendente dai personaggi,
        // così esiste sempre; il Master può poi editarlo dalla console.
        if (await db.Profiles.FindAsync("active") is null)
        {
            db.Profiles.Add(new ProfileRow
            {
                Id = "active",
                Json = JsonSerializer.Serialize(GameProfiles.Dnd5e(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            });
            await db.SaveChangesAsync();
        }

        await UpgradeDnd5eProfileAsync(db);
        await BackfillKiCostAsync(db);

        if (await db.Characters.AnyAsync())
            return;

        string Ids(params string[] ids) => JsonSerializer.Serialize(ids);

        db.Sources.AddRange(
            new SourceRow { Id = "src-ladro", Name = "Ladro", Type = "Class" },
            new SourceRow { Id = "src-monaco", Name = "Monaco", Type = "Class" },
            new SourceRow { Id = "src-mago", Name = "Mago", Type = "Class" },
            new SourceRow { Id = "src-evocazione", Name = "Scuola di Evocazione", Type = "Subclass", ParentSourceId = "src-mago" },
            // Oggetto che concede una magia a un non-caster: nel nostro modello
            // è solo una Fonte con una feature a cariche, nessuno slot richiesto.
            new SourceRow { Id = "src-anello-fuoco", Name = "Anello del Fuoco", Type = "Item" },
            // Oggetto indossabile con un effetto: si equipaggia/rimuove (on/off).
            new SourceRow { Id = "src-mantello", Name = "Mantello della Protezione", Type = "Item" });

        db.SharedTexts.AddRange(
            new SharedTextRow
            {
                Id = "txt-attacco-furtivo",
                Text = "Una volta per turno puoi infliggere danni extra a una creatura che colpisci "
                     + "se hai vantaggio al tiro per colpire e usi un'arma accurata o a distanza.",
            },
            new SharedTextRow
            {
                Id = "txt-schivata-prodigiosa",
                Text = "Quando un attaccante che puoi vedere ti colpisce, puoi usare la reazione "
                     + "per dimezzare i danni dell'attacco.",
            },
            new SharedTextRow
            {
                Id = "txt-raffica-di-colpi",
                Text = "Subito dopo l'azione di Attacco puoi spendere 1 punto ki per effettuare "
                     + "due colpi senza armi come azione bonus.",
            },
            new SharedTextRow
            {
                Id = "txt-difesa-paziente",
                Text = "Puoi spendere 1 punto ki per compiere l'azione di Schivata come azione "
                     + "bonus nel tuo turno.",
            },
            new SharedTextRow
            {
                Id = "txt-passo-del-vento",
                Text = "Puoi spendere 1 punto ki per compiere l'azione di Disimpegno o di Scatto "
                     + "come azione bonus; la distanza dei tuoi salti raddoppia per il turno.",
            },
            new SharedTextRow
            {
                Id = "txt-dardo-incantato",
                Text = "Crei tre dardi di energia magica che colpiscono automaticamente bersagli "
                     + "a tua scelta entro gittata, infliggendo 1d4+1 danni da forza ciascuno.",
            },
            new SharedTextRow
            {
                Id = "txt-scudo",
                Text = "Reazione al momento di essere colpito: +5 alla CA fino all'inizio del tuo "
                     + "prossimo turno, anche contro l'attacco scatenante.",
            },
            new SharedTextRow
            {
                Id = "txt-dardo-fuoco",
                Text = "Come azione, spendi una carica dell'anello per scagliare un dardo di fuoco: "
                     + "1d10 danni da fuoco a un bersaglio entro 18 metri. 3 cariche, si ricaricano "
                     + "all'alba.",
            },
            new SharedTextRow
            {
                Id = "txt-mantello",
                Text = "Mentre lo indossi, hai +1 alla Classe Armatura. Effetto attivo solo da "
                     + "equipaggiato.",
            });

        db.Features.AddRange(
            new FeatureRow
            {
                Id = "ft-attacco-furtivo",
                ShortName = "Attacco Furtivo",
                // Non costa risorse di turno: si somma a un colpo già tirato.
                CostsJson = "[]",
                // "Una volta per turno": si ricarica all'inizio del turno, non
                // col riposo — il caso che Roll20/D&D Beyond gestiscono male.
                MaxUses = 1,
                RemainingUses = 1,
                Recharge = "PerTurn",
                DescriptionId = "txt-attacco-furtivo",
                SourceIdsJson = Ids("src-ladro"),
                // I d6 dei danni extra. È il motivo per cui il campo Roll esiste:
                // erano un numero che il giocatore non poteva tirare da nessuna
                // parte, e che la descrizione non nominava nemmeno.
                Roll = "1d6",
            },
            new FeatureRow
            {
                Id = "ft-schivata-prodigiosa",
                ShortName = "Schivata Prodigiosa",
                CostsJson = """[{"kind":"Reaction","amount":1}]""",
                DescriptionId = "txt-schivata-prodigiosa",
                // Feature comune a due Fonti: esempio vivo del Puntatore di
                // Deduplicazione — un solo testo, due colonne che lo referenziano.
                SourceIdsJson = Ids("src-ladro", "src-monaco"),
            },
            // Le tre opzioni ki del monaco (SRD 5.1). Stanno insieme perché
            // insieme dicono cosa sia una riserva: costano un'azione bonus e
            // **lo stesso punto ki**, quindi spendere una toglie le altre. Con
            // un contatore per feature — com'era la Raffica — un monaco di 3°
            // ne faceva tre di ciascuna, nove in tutto, con tre punti in tasca.
            new FeatureRow
            {
                Id = "ft-raffica-di-colpi",
                ShortName = "Raffica di Colpi",
                CostsJson = """[{"kind":"BonusAction","amount":1},{"kind":"Ki","amount":1}]""",
                DescriptionId = "txt-raffica-di-colpi",
                SourceIdsJson = Ids("src-monaco"),
            },
            new FeatureRow
            {
                Id = "ft-difesa-paziente",
                ShortName = "Difesa Paziente",
                CostsJson = """[{"kind":"BonusAction","amount":1},{"kind":"Ki","amount":1}]""",
                DescriptionId = "txt-difesa-paziente",
                SourceIdsJson = Ids("src-monaco"),
            },
            new FeatureRow
            {
                Id = "ft-passo-del-vento",
                ShortName = "Passo del Vento",
                CostsJson = """[{"kind":"BonusAction","amount":1},{"kind":"Ki","amount":1}]""",
                DescriptionId = "txt-passo-del-vento",
                SourceIdsJson = Ids("src-monaco"),
            },
            new FeatureRow
            {
                Id = "ft-dardo-incantato",
                ShortName = "Dardo Incantato",
                // Azione E slot. Prima il modello sapeva dire solo lo slot, così
                // il fatto che lanciare un incantesimo consumi anche l'azione
                // andava perduto — e il motore ne lasciava lanciare tre per turno.
                CostsJson = """[{"kind":"Action","amount":1},{"kind":"SpellSlot","amount":1}]""",
                DescriptionId = "txt-dardo-incantato",
                SourceIdsJson = Ids("src-mago"),
                // Un dardo. Sono tre: si spende lo slot una volta sola e si tira
                // tre volte — ecco perché tirare e spendere restano separabili.
                Roll = "1d4+1",
            },
            new FeatureRow
            {
                Id = "ft-scudo",
                ShortName = "Scudo",
                // Scudo si lancia come reazione, non come azione: il costo
                // doppio permette finalmente di dire anche questo.
                CostsJson = """[{"kind":"Reaction","amount":1},{"kind":"SpellSlot","amount":1}]""",
                DescriptionId = "txt-scudo",
                SourceIdsJson = Ids("src-mago"),
            },
            new FeatureRow
            {
                Id = "ft-dardo-fuoco",
                ShortName = "Dardo di Fuoco",
                CostsJson = """[{"kind":"Action","amount":1}]""",
                MaxUses = 3,
                RemainingUses = 3,
                Recharge = "LongRest",
                DescriptionId = "txt-dardo-fuoco",
                SourceIdsJson = Ids("src-anello-fuoco"),
                Roll = "1d10",
            },
            new FeatureRow
            {
                Id = "ft-mantello",
                ShortName = "Mantello della Protezione",
                CostsJson = "[]",
                DescriptionId = "txt-mantello",
                SourceIdsJson = Ids("src-mantello"),
                // Effetto: +1 CA, attivo solo da equipaggiato.
                EffectsJson = """[{"target":"CA","op":"Add","value":1}]""",
                Toggleable = true,
            });

        db.Characters.AddRange(
            new CharacterRow
            {
                Id = "pg-kael",
                Name = "Kael (Ladro/Monaco)",
                MaxHp = 38,
                CurrentHp = 38,
                SourceIdsJson = Ids("src-ladro", "src-monaco", "src-anello-fuoco", "src-mantello"),
                FeatureIdsJson = Ids("ft-attacco-furtivo", "ft-schivata-prodigiosa", "ft-raffica-di-colpi", "ft-difesa-paziente", "ft-passo-del-vento", "ft-dardo-fuoco", "ft-mantello"),
                // Punti ki pari al livello da monaco (3), come vuole l'SRD.
                ResourcesJson = """{"Ki":{"0":[3,3]}}""",
                // Statistiche di campagna: Contaminazione (Drakkenheim) e una CA base,
                // su cui il mantello applica il suo +1 quando è equipaggiato.
                CustomStatsJson = """{"Contaminazione":0,"CA":15}""",
            },
            new CharacterRow
            {
                Id = "pg-maga",
                Name = "Lyra (Maga)",
                MaxHp = 26,
                CurrentHp = 26,
                SourceIdsJson = Ids("src-mago", "src-evocazione"),
                FeatureIdsJson = Ids("ft-dardo-incantato", "ft-scudo"),
                ResourcesJson = """{"SpellSlot":{"1":[4,4],"2":[2,2]}}""",
                CustomStatsJson = """{"Sanità Mentale":10}""",
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dà al profilo salvato le statistiche, se è nato prima che esistessero.
    ///
    /// Il profilo è JSON in una colonna: aggiungere un campo al modello non lo
    /// aggiunge ai profili già scritti, che si deserializzano con la lista
    /// vuota. È la stessa lezione dei tiri sulle feature — qui uno schema nuovo
    /// vuole sempre anche una migrazione dei *dati*, non solo delle colonne.
    ///
    /// Si riempie solo se vuota, e solo copiando dal preset con lo stesso id:
    /// un profilo che il Master ha costruito a mano non corrisponde a nessun
    /// preset e resta intatto — le statistiche se le aggiunge lui da Sistema.
    /// </summary>
    private static async Task BackfillProfileStatsAsync(AppDb db)
    {
        var row = await db.Profiles.FindAsync("active");
        if (row is null)
            return;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var profile = JsonSerializer.Deserialize<GameProfile>(row.Json, options);
        if (profile is null)
            return;

        var preset = GameProfiles.Presets().FirstOrDefault(p => p.Id == profile.Id);
        if (preset is null || preset.Stats.Count == 0)
            return;

        var changed = false;

        if (profile.Stats.Count == 0)
        {
            profile.Stats.AddRange(preset.Stats);
            changed = true;
        }
        else if (BackfillModifiers(profile, preset))
        {
            changed = true;
        }

        if (!changed)
            return;

        row.Json = JsonSerializer.Serialize(profile, options);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dà il modificatore alle caratteristiche dei profili nati prima che il
    /// modificatore esistesse.
    ///
    /// Serve perché il profilo è JSON su disco: il preset in codice può
    /// guadagnare un campo, ma la campagna del Master continua a girare col
    /// JSON che aveva. Senza questo, una campagna iniziata ieri avrebbe
    /// <c>1d20+@Forza</c> che somma 18 per sempre — il bug per cui il campo è
    /// nato, sopravvissuto alla sua stessa correzione.
    ///
    /// Si aggancia per <see cref="StatDef.Id"/> — il codice interno stabile,
    /// che non cambia se il Master rinomina l'etichetta — e riempie solo dove
    /// il modificatore manca. Il campo è nuovo, quindi "manca" non può voler
    /// dire "tolto apposta": nessuna UI ha mai permesso di toglierlo.
    /// </summary>
    private static bool BackfillModifiers(GameProfile profile, GameProfile preset)
    {
        var changed = false;

        for (int i = 0; i < profile.Stats.Count; i++)
        {
            var stat = profile.Stats[i];
            if (stat.Modifier is not null)
                continue;

            var fromPreset = preset.Stats.FirstOrDefault(s => s.Id == stat.Id);
            if (fromPreset?.Modifier is null)
                continue;

            // StatDef è immutabile (init-only): si sostituisce la voce, non la
            // si muta. Tutto il resto — etichetta, default, tiro — resta come
            // il Master l'ha lasciato.
            profile.Stats[i] = new StatDef
            {
                Id = stat.Id,
                Label = stat.Label,
                Default = stat.Default,
                Roll = stat.Roll,
                Modifier = fromPreset.Modifier,
            };
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Dà ai personaggi dimostrativi i tiri che il seed assegna solo alle
    /// installazioni nuove.
    ///
    /// Tocca esclusivamente gli id creati dal seed stesso, e solo dove il campo
    /// è ancora vuoto: se il Master ha già scritto "2d6" sull'Attacco Furtivo
    /// del suo ladro di quinto livello, resta il suo. Le feature che ha creato
    /// lui non compaiono in questa lista e non vengono sfiorate.
    /// </summary>
    private static async Task BackfillSeedRollsAsync(AppDb db)
    {
        var touched = false;
        foreach (var (featureId, roll) in SeedRolls)
        {
            if (await db.Features.FindAsync(featureId) is { Roll: null or "" } feature)
            {
                feature.Roll = roll;
                touched = true;
            }
        }
        if (touched)
            await db.SaveChangesAsync();
    }

    /// <summary>
    /// Colonne introdotte dopo il primo rilascio. <c>EnsureCreated</c> le crea
    /// solo su un database nuovo: su uno già esistente è un no-op, e il
    /// caricamento morirebbe con "no such column". Qui si aggiungono a mano.
    ///
    /// Perché non le migration EF: il database del Master contiene il suo
    /// bestiario, la sua config e le feature che ha creato lui — dati che
    /// nessun seed rigenera. Un <c>ADD COLUMN</c> in SQLite tocca solo i
    /// metadati (istantaneo anche a tabella piena) e mantiene la postura
    /// "EnsureCreated" scelta dal progetto, senza introdurre `dotnet ef`.
    ///
    /// Ogni riga qui è idempotente: si aggiunge solo ciò che manca davvero.
    /// </summary>
    /// <summary>
    /// Tiene la campagna in <strong>un file solo</strong>.
    ///
    /// Il driver SQLite di Microsoft accende il WAL da sé — nessuno lo chiede
    /// nel codice, e il default di SQLite sarebbe <c>delete</c>. Col WAL la
    /// campagna vive in <em>tre</em> file (<c>.db</c>, <c>-wal</c>, <c>-shm</c>)
    /// mentre l'app è accesa, e le scritture recenti stanno nel <c>-wal</c>.
    ///
    /// Questo rompe in silenzio la promessa che il README fa al Master: «è un
    /// file normale, si copia per fare un backup». Copiando il solo <c>.db</c>
    /// col server acceso si ottiene uno scatto vecchio — verificato: un mostro
    /// appena creato non c'era. Un backup che perde l'ultima ora di gioco senza
    /// dirlo è peggio di nessun backup, perché ci conti.
    ///
    /// <c>journal_mode</c> è persistente nell'intestazione del database, quindi
    /// questa riga converte anche le campagne già esistenti e fa sparire il
    /// <c>-wal</c>. Il prezzo del rollback journal è che uno scrittore blocca i
    /// lettori: a un tavolo da quattro persone, su loopback, non lo misura
    /// nessuno. La promessa vale più della concorrenza che non ci serve.
    /// </summary>
    private static async Task EnsureSingleFileAsync(AppDb db)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
    }

    /// <summary>
    /// Meccaniche 5e introdotte dopo il primo rilascio, su un profilo già
    /// salvato. È la stessa lezione delle colonne: il profilo si scrive una
    /// volta sola, alla creazione della campagna, e non lo tocca più nessuno —
    /// così una campagna di ieri non avrebbe mai né la Concentrazione né gli
    /// Attacchi, e l'Attacco Extra non funzionerebbe <em>in silenzio</em>.
    ///
    /// Solo per i profili di lignaggio "dnd5e": se il Master si è costruito il
    /// suo sistema, non gli si mette dentro roba di D&amp;D. Ed è additivo per
    /// costruzione — una risorsa che nessuna feature paga e uno slot che
    /// nessuna feature occupa non possono orfanare niente. Chi ha cancellato
    /// una delle due apposta se la ritrova: il prezzo per non lasciare le
    /// campagne vecchie con un motore muto.
    /// </summary>
    private static async Task UpgradeDnd5eProfileAsync(AppDb db)
    {
        var row = await db.Profiles.FindAsync("active");
        if (row is null) return;

        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var profile = JsonSerializer.Deserialize<GameProfile>(row.Json, opts);
        if (profile is null || profile.Id != "dnd5e") return;

        var preset = GameProfiles.Dnd5e();
        var touched = false;

        foreach (var res in preset.TurnResources)
        {
            if (profile.TurnResources.Any(t => t.Id == res.Id)) continue;
            profile.TurnResources.Add(res);
            touched = true;
        }

        foreach (var slot in preset.ExclusiveSlots)
        {
            if (profile.ExclusiveSlots.Any(s => s.Id == slot.Id)) continue;
            profile.ExclusiveSlots.Add(slot);
            touched = true;
        }

        // Anche le riserve, per lo stesso motivo delle altre due: il preset in
        // codice guadagna il ki, ma la campagna in corso continua col JSON che
        // ha su disco — e senza il pool il costo in ki della Raffica sarebbe un
        // "costo non definito nel profilo", cioè una feature che non si usa più.
        foreach (var pool in preset.Pools)
        {
            if (profile.Pools.Any(p => p.Id == pool.Id)) continue;
            profile.Pools.Add(pool);
            touched = true;
        }

        if (!touched) return;

        row.Json = JsonSerializer.Serialize(profile, opts);
        await db.SaveChangesAsync();
    }

    private static async Task AddMissingColumnsAsync(AppDb db)
    {
        await AddColumnIfMissingAsync(db, "Features", "Roll", "TEXT NULL");
        await AddColumnIfMissingAsync(db, "Characters", "StatRollsJson", "TEXT NOT NULL DEFAULT '{}'");
        await AddColumnIfMissingAsync(db, "Features", "CostsJson", "TEXT NULL");
        await AddColumnIfMissingAsync(db, "Features", "GrantsJson", "TEXT NOT NULL DEFAULT '[]'");
        await AddColumnIfMissingAsync(db, "Features", "Occupies", "TEXT NULL");
        await AddColumnIfMissingAsync(db, "Characters", "OccupiedJson", "TEXT NOT NULL DEFAULT '{}'");

        // Il bestiario ha reso Characters una tabella TPH (personaggi + mostri).
        // Il default 'Character' non è cosmetico: senza discriminatore EF non
        // materializza le righe già esistenti, e sono le schede dei giocatori.
        await AddColumnIfMissingAsync(db, "Characters", "Type", "TEXT NOT NULL DEFAULT 'Character'");
        await AddColumnIfMissingAsync(db, "Characters", "Notes", "TEXT NULL");
    }

    /// <summary>
    /// Porta i costi dal vecchio campo singolo alla lista.
    ///
    /// Null = riga mai migrata: si legge CostKind/SlotLevel e si scrive la
    /// lista equivalente. Una passiva diventa "[]", che è diverso da null — è
    /// il motivo per cui la colonna è nullable.
    ///
    /// Non indovina nulla: una magia che prima diceva solo "SpellSlot" diventa
    /// "[SpellSlot]", non "[Azione, SpellSlot]". Aggiungere l'Azione è una
    /// decisione di gioco, e la prende il Master da Schede — tranne che per le
    /// magie del seed, che sono nostre e sappiamo cosa sono.
    /// </summary>
    private static async Task BackfillCostsAsync(AppDb db)
    {
        var rows = await db.Features.Where(f => f.CostsJson == null).ToListAsync();
        if (rows.Count == 0)
            return;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var row in rows)
        {
            var costs = string.IsNullOrEmpty(row.CostKind) || row.CostKind == "None"
                ? []
                : new List<object> { new { kind = row.CostKind, amount = row.SlotLevel } };
            row.CostsJson = JsonSerializer.Serialize(costs, options);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dà alla Raffica di Colpi del seed il suo vero costo: un'azione bonus e
    /// <em>un punto ki</em>.
    ///
    /// Nasceva con tre usi a riposo breve e un <c>{"risorsa":"ki"}</c> in
    /// CustomJson — una stringa che il motore trasporta e non guarda. Il ki
    /// però è una riserva condivisa: con un contatore per feature, spendere la
    /// Difesa Paziente non toglieva niente alla Raffica, e un monaco di 3° ne
    /// aveva tre di ciascuna invece di tre in tutto.
    ///
    /// Si applica solo dove la riga è ancora quella del seed — costo la sola
    /// azione bonus e contatore 3/riposo breve. Se il Master l'ha già
    /// sistemata, resta la sua: stessa regola di
    /// <see cref="BackfillSpellActionCostAsync"/>.
    /// </summary>
    private static async Task BackfillKiCostAsync(AppDb db)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var touched = false;

        if (await db.Features.FindAsync("ft-raffica-di-colpi") is { CostsJson: not null } raffica)
        {
            var costs = JsonSerializer.Deserialize<List<CostEntry>>(raffica.CostsJson, options) ?? [];
            if (costs is [{ Kind: "BonusAction" }] && raffica is { MaxUses: 3, Recharge: "ShortRest" })
            {
                costs.Add(new CostEntry { Kind = "Ki", Amount = 1 });
                raffica.CostsJson = JsonSerializer.Serialize(costs, options);

                // Il contatore va via: adesso il limite è la riserva, e tenerli
                // entrambi vorrebbe dire pagare due volte la stessa cosa.
                raffica.MaxUses = null;
                raffica.RemainingUses = null;
                raffica.Recharge = null;
                raffica.CustomJson = "{}";
                touched = true;
            }
        }

        // I punti ki al personaggio dimostrativo: senza, la Raffica appena
        // migrata sarebbe impagabile — corretta e inutilizzabile.
        if (await db.Characters.FindAsync("pg-kael") is { } kael)
        {
            var pools = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int[]>>>(
                kael.ResourcesJson, options) ?? [];
            if (!pools.ContainsKey("Ki"))
            {
                pools["Ki"] = new Dictionary<string, int[]> { ["0"] = [3, 3] };
                kael.ResourcesJson = JsonSerializer.Serialize(pools, options);
                touched = true;
            }
        }

        if (touched)
            await db.SaveChangesAsync();
    }

    /// <summary>
    /// Le magie del seed costano anche l'Azione (o la Reazione, per Scudo).
    /// Prima non potevano dirlo — il modello aveva un costo solo — e il motore
    /// lasciava lanciare tre incantesimi nello stesso turno. Si applica solo
    /// dove il costo è ancora esattamente il solo slot: se il Master l'ha già
    /// sistemato, resta il suo.
    /// </summary>
    private static readonly (string FeatureId, string TurnResource)[] SeedSpellActions =
    [
        ("ft-dardo-incantato", "Action"),
        ("ft-scudo", "Reaction"),
    ];

    private static async Task BackfillSpellActionCostAsync(AppDb db)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var touched = false;

        foreach (var (featureId, turnResource) in SeedSpellActions)
        {
            if (await db.Features.FindAsync(featureId) is not { } row || row.CostsJson is null)
                continue;

            var costs = JsonSerializer.Deserialize<List<CostEntry>>(row.CostsJson, options) ?? [];
            if (costs.Count != 1 || costs[0].Kind != "SpellSlot")
                continue;

            costs.Insert(0, new CostEntry { Kind = turnResource, Amount = 1 });
            row.CostsJson = JsonSerializer.Serialize(costs, options);
            touched = true;
        }

        if (touched)
            await db.SaveChangesAsync();
    }

    private sealed class CostEntry
    {
        public string Kind { get; set; } = "None";
        public int Amount { get; set; }
    }

    private static async Task AddColumnIfMissingAsync(AppDb db, string table, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
            AddParameter(probe, "$table", table);
            AddParameter(probe, "$column", column);
            if (Convert.ToInt64(await probe.ExecuteScalarAsync()) > 0)
                return;
        }

        // Comando grezzo e non ExecuteSqlRaw: quest'ultimo passa la stringa da
        // String.Format, e una definizione come DEFAULT '{}' verrebbe letta
        // come segnaposto di formato (FormatException all'avvio).
        //
        // ALTER TABLE non accetta parametri per gli identificatori. Non è
        // un'iniezione: tabella, colonna e definizione sono costanti di questo
        // file, mai input di rete.
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync();
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
