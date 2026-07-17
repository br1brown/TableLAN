namespace TableLAN.Core.Tests;

using TableLAN.Core.Characters;
using TableLAN.Core.Engine;
using TableLAN.Core.Features;
using TableLAN.Core.Profile;
using TableLAN.Core.Sources;

public class RuleEngineTests
{
    private static readonly GameProfile Profile = GameProfiles.Dnd5e();

    private static Character CasterWithSlots(int level1Slots)
    {
        var mago = new Source { Id = "src-mago", Name = "Mago", Type = SourceType.Class };
        var character = new Character
        {
            Id = "pg-caster",
            Name = "Incantatrice",
            Sources = [mago],
            Features =
            [
                new Feature
                {
                    Id = "ft-dardo",
                    ShortName = "Dardo Incantato",
                    SourceIds = ["src-mago"],
                    Costs = [new ActivationCost("SpellSlot", 1)],
                    DescriptionId = "txt-dardo",
                },
            ],
        };
        character.Resources.SetTier("SpellSlot", 1, level1Slots);
        return character;
    }

    /// <summary>Il Dardo com'è davvero: costa l'Azione E lo slot.</summary>
    private static Character CasterWithSpellCosts(int level1Slots)
    {
        var character = CasterWithSlots(level1Slots);
        character.Features[0] = new Feature
        {
            Id = "ft-dardo",
            ShortName = "Dardo Incantato",
            SourceIds = ["src-mago"],
            Costs = [new ActivationCost("Action", 1), new ActivationCost("SpellSlot", 1)],
            DescriptionId = "txt-dardo",
        };
        return character;
    }

    /// <summary>
    /// Col costo singolo di prima "Azione E slot" non era esprimibile, e il
    /// motore lasciava lanciare tre incantesimi nello stesso turno finché
    /// c'erano slot. Era un bug di regole, non di interfaccia.
    /// </summary>
    [Fact]
    public void Un_secondo_incantesimo_nello_stesso_turno_e_respinto_anche_con_slot_residui()
    {
        var character = CasterWithSpellCosts(level1Slots: 4);
        var engine = new RuleEngine();
        var turn = new TurnState(character.EffectiveTurnLimits(Profile));

        var first = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), turn, Profile);
        Assert.True(first.IsValid, first.Reason);

        // Gli slot ci sono ancora (4 → 3). È l'Azione a mancare.
        var second = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), turn, Profile);

        Assert.False(second.IsValid);
        Assert.Contains("Azione", second.Reason);
        Assert.Equal(3, character.Resources.Snapshot()["SpellSlot"][1].Remaining);
    }

    /// <summary>
    /// Se un costo non è pagabile non se ne paga nessuno: pagare l'Azione e poi
    /// scoprire che manca lo slot lascerebbe il turno mutilato per un intento
    /// che è stato comunque respinto.
    /// </summary>
    [Fact]
    public void Un_costo_impagabile_non_ne_fa_pagare_nessun_altro()
    {
        var character = CasterWithSpellCosts(level1Slots: 0);
        var engine = new RuleEngine();
        var turn = new TurnState(character.EffectiveTurnLimits(Profile));

        var result = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), turn, Profile);

        Assert.False(result.IsValid);
        Assert.Equal(1, turn.Remaining("Action")); // l'Azione è intatta
    }

    [Fact]
    public void Incantesimo_senza_slot_viene_respinto_alla_fonte()
    {
        var character = CasterWithSlots(level1Slots: 0);
        var engine = new RuleEngine();

        var result = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), new TurnState(character.EffectiveTurnLimits(Profile)), Profile);

        Assert.False(result.IsValid);
        Assert.Contains("slot", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Incantesimo_con_slot_consuma_lo_slot()
    {
        var character = CasterWithSlots(level1Slots: 2);
        var engine = new RuleEngine();

        var result = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), new TurnState(character.EffectiveTurnLimits(Profile)), Profile);

        Assert.True(result.IsValid);
        Assert.Equal(1, character.Resources.Remaining("SpellSlot", 1));
    }

    [Fact]
    public void Upcasting_spende_lo_slot_di_livello_superiore_richiesto()
    {
        var character = CasterWithSlots(level1Slots: 1);
        character.Resources.SetTier("SpellSlot", 2, 1);
        var engine = new RuleEngine();

        var result = engine.Apply(
            character,
            new Intent("pg-caster", "ft-dardo") { SlotLevelOverride = 2 },
            new TurnState(character.EffectiveTurnLimits(Profile)),
            Profile);

        Assert.True(result.IsValid);
        Assert.Equal(1, character.Resources.Remaining("SpellSlot", 1));
        Assert.Equal(0, character.Resources.Remaining("SpellSlot", 2));
    }

    [Fact]
    public void Seconda_azione_nello_stesso_turno_viene_respinta()
    {
        var fonte = new Source { Id = "src-x", Name = "X", Type = SourceType.Class };
        var attacco = new Feature
        {
            Id = "ft-attacco",
            ShortName = "Attacco",
            SourceIds = ["src-x"],
            Costs = [new ActivationCost("Action")],
            DescriptionId = "txt-attacco",
        };
        var character = new Character { Id = "pg", Name = "PG", Sources = [fonte], Features = [attacco] };
        var engine = new RuleEngine();
        var turn = new TurnState(character.EffectiveTurnLimits(Profile));

        Assert.True(engine.Apply(character, new Intent("pg", "ft-attacco"), turn, Profile).IsValid);
        var second = engine.Apply(character, new Intent("pg", "ft-attacco"), turn, Profile);

        Assert.False(second.IsValid);
        Assert.Equal(0, turn.Remaining("Action"));
        Assert.Equal(1, turn.Remaining("BonusAction"));
    }

    [Fact]
    public void Feature_esaurita_viene_respinta_finche_non_si_riposa()
    {
        var fonte = new Source { Id = "src-monaco", Name = "Monaco", Type = SourceType.Class };
        var raffica = new Feature
        {
            Id = "ft-raffica",
            ShortName = "Raffica di Colpi",
            SourceIds = ["src-monaco"],
            Costs = [new ActivationCost("BonusAction")],
            Usage = new UsageCounter { MaxUses = 1, RemainingUses = 1, Recharge = "ShortRest" },
            DescriptionId = "txt-raffica",
        };
        var character = new Character { Id = "pg", Name = "PG", Sources = [fonte], Features = [raffica] };
        var engine = new RuleEngine();

        var turn = new TurnState(character.EffectiveTurnLimits(Profile));
        Assert.True(engine.Apply(character, new Intent("pg", "ft-raffica"), turn, Profile).IsValid);

        turn.ResetForNewTurn(character.EffectiveTurnLimits(Profile));
        var exhausted = engine.Apply(character, new Intent("pg", "ft-raffica"), turn, Profile);
        Assert.False(exhausted.IsValid);
        Assert.Contains("usi residui", exhausted.Reason);

        character.CompleteRest("ShortRest", Profile);
        turn.ResetForNewTurn(character.EffectiveTurnLimits(Profile));
        Assert.True(engine.Apply(character, new Intent("pg", "ft-raffica"), turn, Profile).IsValid);
    }

    [Fact]
    public void Riposo_lungo_ricarica_anche_le_risorse_a_riposo_breve_e_gli_slot()
    {
        var character = CasterWithSlots(level1Slots: 1);
        character.Resources.Spend(Profile.Pool("SpellSlot")!, 1);

        character.CompleteRest("LongRest", Profile);

        Assert.Equal(1, character.Resources.Remaining("SpellSlot", 1));
    }

    [Fact]
    public void Feature_una_volta_per_turno_si_ricarica_a_ogni_nuovo_turno()
    {
        var fonte = new Source { Id = "src-ladro", Name = "Ladro", Type = SourceType.Class };
        var furtivo = new Feature
        {
            Id = "ft-furtivo",
            ShortName = "Attacco Furtivo",
            SourceIds = ["src-ladro"],
            Costs = [],
            Usage = new UsageCounter { MaxUses = 1, RemainingUses = 1, Recharge = "PerTurn" },
            DescriptionId = "txt-furtivo",
        };
        var character = new Character { Id = "pg", Name = "PG", Sources = [fonte], Features = [furtivo] };
        var engine = new RuleEngine();
        var turn = new TurnState(character.EffectiveTurnLimits(Profile));

        Assert.True(engine.Apply(character, new Intent("pg", "ft-furtivo"), turn, Profile).IsValid);

        turn.ResetForNewTurn(character.EffectiveTurnLimits(Profile));
        Assert.False(engine.Apply(character, new Intent("pg", "ft-furtivo"), turn, Profile).IsValid);

        character.StartNewTurn(Profile);
        turn.ResetForNewTurn(character.EffectiveTurnLimits(Profile));
        Assert.True(engine.Apply(character, new Intent("pg", "ft-furtivo"), turn, Profile).IsValid);
    }

    [Fact]
    public void Un_profilo_diverso_cambia_le_meccaniche_senza_toccare_il_motore()
    {
        // Profilo custom: niente slot a livelli, ma un pool di punti (stile ki/
        // mana) e un ciclo di riposo proprio. Stesso motore, meccaniche diverse.
        var profile = new GameProfile
        {
            Id = "custom",
            Name = "Sistema a Punti",
            TurnResources = [new TurnResource { Id = "Action", Label = "Azione" }],
            Pools =
            [
                new ResourcePoolDef { Id = "Ki", Label = "Punti Ki", Kind = PoolKind.Points, RechargeCycle = "Scena" },
            ],
            Cycles = [new RestCycle { Id = "Scena", Label = "Scena", Rank = 1 }],
        };

        var fonte = new Source { Id = "src-monaco", Name = "Monaco", Type = SourceType.Class };
        var colpo = new Feature
        {
            Id = "ft-colpo",
            ShortName = "Colpo Stordente",
            SourceIds = ["src-monaco"],
            Costs = [new ActivationCost("Ki", 2)], // costa 2 punti ki
            DescriptionId = "txt-colpo",
        };
        var character = new Character { Id = "pg", Name = "Monaco", Sources = [fonte], Features = [colpo] };
        character.Resources.SetTier("Ki", level: 0, max: 3);
        var engine = new RuleEngine();

        // 3 punti: primo colpo (−2) ok, secondo respinto (restano 1 < 2).
        Assert.True(engine.Apply(character, new Intent("pg", "ft-colpo"), new TurnState(character.EffectiveTurnLimits(profile)), profile).IsValid);
        Assert.Equal(1, character.Resources.Remaining("Ki"));
        Assert.False(engine.Apply(character, new Intent("pg", "ft-colpo"), new TurnState(character.EffectiveTurnLimits(profile)), profile).IsValid);

        // Una "Scena" ricarica il pool.
        character.CompleteRest("Scena", profile);
        Assert.Equal(3, character.Resources.Remaining("Ki"));
    }

    [Fact]
    public void Personaggio_Con_Action_Surge_Puo_Usare_Due_Azioni()
    {
        var character = CasterWithSpellCosts(level1Slots: 4); // Usa Dardo Incantato che costa 1 Azione e 1 Slot
        
        // Aggiungiamo un effetto attivo che dà +1 all'Azione (Action Surge simulato)
        character.Features.Add(new Feature
        {
            Id = "ft-action-surge",
            ShortName = "Azione Impetuosa",
            DescriptionId = "txt-action-surge",
            SourceIds = ["src-guerriero"],
            Effects = [new Effect("@turn:Action", EffectOp.Add, 1)],
            Toggleable = true,
        });
        character.EffectStates["ft-action-surge"] = true; // Acceso

        var engine = new RuleEngine();
        
        // Il TurnState legge i limiti effettivi (Azione: 1 base + 1 effetto = 2)
        var limits = character.EffectiveTurnLimits(Profile);
        var turn = new TurnState(limits);

        // Primo Dardo: valido
        var first = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), turn, Profile);
        Assert.True(first.IsValid);

        // Secondo Dardo nello STESSO turno: normalmente fallisce, ma ora deve riuscire
        var second = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), turn, Profile);
        Assert.True(second.IsValid, "Il secondo Dardo deve riuscire perché l'Azione Impetuosa dà +1 Azione.");

        // Terzo Dardo: fallisce per mancanza di Azioni
        var third = engine.Apply(character, new Intent("pg-caster", "ft-dardo"), turn, Profile);
        Assert.False(third.IsValid);
        Assert.Contains("Azione", third.Reason);
    }

    [Fact]
    public void Personaggio_Lento_Non_Ha_Azioni_Bonus()
    {
        var character = CasterWithSpellCosts(level1Slots: 4);
        character.Features.Add(new Feature
        {
            Id = "ft-bonus-spell",
            ShortName = "Magia Rapida",
            DescriptionId = "txt-bonus-spell",
            SourceIds = ["src-mago"],
            Costs = [new ActivationCost("BonusAction", 1)],
        });

        // Aggiungiamo un effetto di Debuff (es. Lentezza) che blocca l'Azione Bonus
        character.Features.Add(new Feature
        {
            Id = "ft-lento",
            ShortName = "Lento",
            DescriptionId = "txt-lento",
            SourceIds = ["src-malus"],
            Effects = [new Effect("@turn:BonusAction", EffectOp.Set, 0)],
            Toggleable = false, // Passiva finché c'è
        });

        var engine = new RuleEngine();
        var limits = character.EffectiveTurnLimits(Profile);
        var turn = new TurnState(limits); // BonusAction parte a 0!

        // Provare a usare l'Azione Bonus fallisce subito
        var action = engine.Apply(character, new Intent("pg-caster", "ft-bonus-spell"), turn, Profile);
        Assert.False(action.IsValid);
        Assert.Contains("Azione Bonus", action.Reason);
    }

    [Fact]
    public void Personaggio_Con_Eccesso_Azioni_Non_Va_In_Errore()
    {
        var character = CasterWithSpellCosts(level1Slots: 99);
        
        // Homebrew assurdo: 99 azioni bonus!
        character.Features.Add(new Feature
        {
            Id = "ft-homebrew",
            ShortName = "Homebrew OP",
            DescriptionId = "txt-homebrew",
            SourceIds = ["src-homebrew"],
            Effects = [new Effect("@turn:Action", EffectOp.Set, 99)],
            Toggleable = true,
        });
        character.EffectStates["ft-homebrew"] = true;

        var engine = new RuleEngine();
        var limits = character.EffectiveTurnLimits(Profile);
        var turn = new TurnState(limits);

        // Posso lanciare Dardo 99 volte (se ho gli slot)
        Assert.Equal(99, turn.Remaining("Action"));
    }
}

/// <summary>
/// Grants: una feature che <em>restituisce</em> risorse di turno.
///
/// Due regole della 5e che il solo costo non sa dire. Action Surge concede
/// «one additional action on top of your regular action»: un credito adesso,
/// non un tetto più alto domani. L'Attacco Extra è un'Azione che vale due
/// attacchi — e va fermato al terzo.
/// </summary>
public class GrantsTests
{
    private static readonly GameProfile Profile = GameProfiles.Dnd5e();
    private static readonly RuleEngine Engine = new();

    private static Character Guerriero()
    {
        var classe = new Source { Id = "src-guer", Name = "Guerriero", Type = SourceType.Class };
        return new Character
        {
            Id = "pg-guer",
            Name = "Kravax",
            Sources = [classe],
            Features =
            [
                // L'azione di Attacco: costa l'Azione, vale due attacchi (5° liv).
                new Feature
                {
                    Id = "ft-azione-attacco",
                    ShortName = "Azione di Attacco",
                    SourceIds = ["src-guer"],
                    Costs = [new ActivationCost("Action", 1)],
                    Grants = [new ActivationCost("Attack", 2)],
                    DescriptionId = "txt-aa",
                },
                // Il colpo vero: costa un attacco, non l'Azione.
                new Feature
                {
                    Id = "ft-spada",
                    ShortName = "Spada Lunga",
                    SourceIds = ["src-guer"],
                    Costs = [new ActivationCost("Attack", 1)],
                    DescriptionId = "txt-spada",
                },
                // Action Surge: non costa nulla, concede un'Azione intera.
                new Feature
                {
                    Id = "ft-surge",
                    ShortName = "Action Surge",
                    SourceIds = ["src-guer"],
                    Grants = [new ActivationCost("Action", 1)],
                    Usage = new UsageCounter { MaxUses = 1, RemainingUses = 1, Recharge = "ShortRest" },
                    DescriptionId = "txt-surge",
                },
            ],
        };
    }

    private static TurnState NuovoTurno(Character c) => new(c.EffectiveTurnLimits(Profile));
    private static ValidationResult Usa(Character c, TurnState t, string featureId) =>
        Engine.Apply(c, new Intent(c.Id, featureId), t, Profile);

    [Fact]
    public void Senza_azione_di_attacco_non_si_colpisce()
    {
        var c = Guerriero();
        var turno = NuovoTurno(c);
        // "Attacco" nasce a zero: gli attacchi li concede l'azione di Attacco.
        var esito = Usa(c, turno, "ft-spada");
        Assert.False(esito.IsValid);
        Assert.Contains("Attacco", esito.Reason);
    }

    [Fact]
    public void L_azione_di_attacco_concede_due_colpi_e_il_terzo_si_ferma()
    {
        var c = Guerriero();
        var turno = NuovoTurno(c);

        Assert.True(Usa(c, turno, "ft-azione-attacco").IsValid);
        Assert.Equal(2, turno.Remaining("Attack"));

        Assert.True(Usa(c, turno, "ft-spada").IsValid);
        Assert.True(Usa(c, turno, "ft-spada").IsValid);

        // Il terzo no: è il punto di tutto l'esercizio.
        var terzo = Usa(c, turno, "ft-spada");
        Assert.False(terzo.IsValid);
        Assert.Equal(0, turno.Remaining("Attack"));
    }

    [Fact]
    public void Action_surge_ridà_l_azione_nello_stesso_turno()
    {
        // Il turno "nova" del Guerriero 5°: Attacco (2 colpi), Action Surge,
        // Attacco di nuovo (altri 2). Quattro colpi in un turno, e il quinto no.
        var c = Guerriero();
        var turno = NuovoTurno(c);

        Assert.True(Usa(c, turno, "ft-azione-attacco").IsValid);
        Assert.Equal(0, turno.Remaining("Action"));
        Assert.True(Usa(c, turno, "ft-spada").IsValid);
        Assert.True(Usa(c, turno, "ft-spada").IsValid);
        Assert.Equal(0, turno.Remaining("Attack"));

        // Prima qui il motore diceva di no e la feature era una nota testuale.
        Assert.True(Usa(c, turno, "ft-surge").IsValid);
        Assert.Equal(1, turno.Remaining("Action"));

        // E l'Azione riavuta serve a qualcosa: altri due attacchi, non di più.
        Assert.True(Usa(c, turno, "ft-azione-attacco").IsValid);
        Assert.Equal(2, turno.Remaining("Attack"));
        Assert.True(Usa(c, turno, "ft-spada").IsValid);
        Assert.True(Usa(c, turno, "ft-spada").IsValid);
        Assert.False(Usa(c, turno, "ft-spada").IsValid);
    }

    [Fact]
    public void Action_surge_resta_una_volta_per_riposo()
    {
        var c = Guerriero();
        var turno = NuovoTurno(c);
        Assert.True(Usa(c, turno, "ft-surge").IsValid);
        Assert.False(Usa(c, turno, "ft-surge").IsValid);
    }

    [Fact]
    public void Il_credito_puo_superare_il_tetto_per_turno()
    {
        // Il tetto è 1 Azione. Action Surge ne dà una seconda *adesso*: è
        // proprio ciò che l'effetto @turn: non sapeva fare, perché i limiti
        // valgono al reset.
        var c = Guerriero();
        var turno = NuovoTurno(c);
        Assert.True(Usa(c, turno, "ft-surge").IsValid);
        Assert.Equal(2, turno.Remaining("Action"));
    }

    [Fact]
    public void Gli_attacchi_non_avanzano_al_turno_dopo()
    {
        var c = Guerriero();
        var turno = NuovoTurno(c);
        Usa(c, turno, "ft-azione-attacco");
        Assert.Equal(2, turno.Remaining("Attack"));

        turno.ResetForNewTurn(c.EffectiveTurnLimits(Profile));
        Assert.Equal(0, turno.Remaining("Attack"));
    }

    [Fact]
    public void Un_credito_verso_una_risorsa_inventata_viene_fermato()
    {
        var c = Guerriero();
        c.Features.Add(new Feature
        {
            Id = "ft-bugia",
            ShortName = "Promessa Vuota",
            SourceIds = ["src-guer"],
            Grants = [new ActivationCost("Teletrasporto", 1)],
            DescriptionId = "txt-bugia",
        });

        var esito = Engine.Validate(c, c.Features.Last(), NuovoTurno(c), Profile);
        Assert.False(esito.IsValid);
        Assert.Contains("Teletrasporto", esito.Reason);
    }
}

/// <summary>
/// Concentrazione: uno slot esclusivo, non un costo.
///
/// La regola 5e è che lanciarne una seconda chiude la prima subito, senza tiro
/// e senza trattativa. Quindi occupare non fallisce <em>mai</em>: il motore non
/// respinge, riferisce cos'è caduto.
/// </summary>
public class ConcentrazioneTests
{
    private static readonly GameProfile Profile = GameProfiles.Dnd5e();
    private static readonly RuleEngine Engine = new();

    private static Feature Magia(string id, string nome, bool concentra) => new()
    {
        Id = id,
        ShortName = nome,
        SourceIds = ["src-mago"],
        Costs = [new ActivationCost("Action", 1), new ActivationCost("SpellSlot", 1)],
        Occupies = concentra ? "Concentration" : null,
        DescriptionId = "txt-" + id,
    };

    private static Character Incantatrice()
    {
        var c = new Character
        {
            Id = "pg-mago",
            Name = "Lyra",
            Sources = [new Source { Id = "src-mago", Name = "Mago", Type = SourceType.Class }],
            Features =
            [
                Magia("ft-benedizione", "Benedizione", concentra: true),
                Magia("ft-immobilizza", "Immobilizzare Persone", concentra: true),
                Magia("ft-dardo", "Dardo Incantato", concentra: false),
            ],
        };
        c.Resources.SetTier("SpellSlot", 1, 9);
        return c;
    }

    private static ValidationResult Lancia(Character c, string id) =>
        Engine.Apply(c, new Intent(c.Id, id), new TurnState(c.EffectiveTurnLimits(Profile)), Profile);

    [Fact]
    public void Concentrarsi_occupa_lo_slot()
    {
        var c = Incantatrice();
        Assert.True(Lancia(c, "ft-benedizione").IsValid);
        Assert.Equal("ft-benedizione", c.Occupied["Concentration"]);
        Assert.Equal("Benedizione", c.Occupant("Concentration")?.ShortName);
    }

    [Fact]
    public void La_seconda_concentrazione_fa_cadere_la_prima_e_lo_dice()
    {
        var c = Incantatrice();
        Lancia(c, "ft-benedizione");

        var esito = Lancia(c, "ft-immobilizza");

        // Riesce: in 5e non c'è tiro per tenersi la prima.
        Assert.True(esito.IsValid);
        Assert.Equal("ft-immobilizza", c.Occupied["Concentration"]);
        // Ma il giocatore deve sapere cosa ha buttato.
        Assert.NotNull(esito.Notice);
        Assert.Contains("Benedizione", esito.Notice);
        Assert.Contains("Concentrazione", esito.Notice);
    }

    [Fact]
    public void Una_magia_che_non_concentra_non_tocca_lo_slot()
    {
        var c = Incantatrice();
        Lancia(c, "ft-benedizione");
        var esito = Lancia(c, "ft-dardo");

        Assert.True(esito.IsValid);
        Assert.Null(esito.Notice);
        Assert.Equal("ft-benedizione", c.Occupied["Concentration"]);
    }

    [Fact]
    public void Rilanciare_la_stessa_non_annuncia_una_caduta_finta()
    {
        var c = Incantatrice();
        Lancia(c, "ft-benedizione");
        var esito = Lancia(c, "ft-benedizione");

        Assert.True(esito.IsValid);
        Assert.Null(esito.Notice);   // non "'Benedizione' è terminata" mentre la rilanci
    }

    [Fact]
    public void Si_puo_smettere_quando_si_vuole()
    {
        var c = Incantatrice();
        Lancia(c, "ft-benedizione");
        c.Release("Concentration");
        Assert.Null(c.Occupant("Concentration"));
    }

    [Fact]
    public void La_concentrazione_sopravvive_al_turno()
    {
        // È l'unico stato che non si azzera a fine turno: dura finché regge.
        var c = Incantatrice();
        Lancia(c, "ft-benedizione");
        var turno = new TurnState(c.EffectiveTurnLimits(Profile));
        turno.ResetForNewTurn(c.EffectiveTurnLimits(Profile));
        Assert.Equal("Benedizione", c.Occupant("Concentration")?.ShortName);
    }

    [Fact]
    public void Un_profilo_senza_slot_esclusivi_ignora_la_cosa()
    {
        // Occupies resta scritto sulla feature, ma se il sistema non sa cosa
        // sia la concentrazione non deve succedere niente di strano.
        var senza = GameProfiles.Dnd5e();
        senza.ExclusiveSlots.Clear();
        var c = Incantatrice();

        var esito = Engine.Apply(c, new Intent(c.Id, "ft-benedizione"),
            new TurnState(c.EffectiveTurnLimits(senza)), senza);

        Assert.True(esito.IsValid);
        Assert.Empty(c.Occupied);
    }

    // ---- Il ki: una riserva condivisa, non tre contatori ----

    /// <summary>
    /// Un monaco di 3° come vuole l'SRD: tre punti ki, e tre modi di spenderli.
    /// Ognuna costa un'azione bonus e 1 ki, e pescano tutte dagli stessi punti.
    /// </summary>
    private static Character Monaco(int ki = 3)
    {
        var monaco = new Source { Id = "src-monaco", Name = "Monaco", Type = SourceType.Class };
        var c = new Character
        {
            Id = "pg-monaco",
            Name = "Monaco",
            Sources = [monaco],
            Features =
            [
                KiFeature("ft-raffica", "Raffica di Colpi"),
                KiFeature("ft-difesa", "Difesa Paziente"),
                KiFeature("ft-passo", "Passo del Vento"),
            ],
        };
        c.Resources.SetTier("Ki", 0, ki);
        return c;

        static Feature KiFeature(string id, string nome) => new()
        {
            Id = id,
            ShortName = nome,
            SourceIds = ["src-monaco"],
            Costs = [new ActivationCost("BonusAction", 1), new ActivationCost("Ki", 1)],
            DescriptionId = "txt-" + id,
        };
    }

    /// <summary>
    /// Il motivo per cui il ki è un pool. Con un contatore per feature ognuna
    /// aveva i suoi tre usi: nove spese con tre punti in tasca, e il motore
    /// zitto. Le azioni bonus qui non c'entrano — se ne concede una per turno.
    /// </summary>
    [Fact]
    public void Le_tre_opzioni_ki_pescano_dagli_stessi_punti()
    {
        var c = Monaco(ki: 3);

        Spendi(c, "ft-raffica");
        Assert.Equal(2, c.Resources.Remaining("Ki"));

        Spendi(c, "ft-difesa");
        Spendi(c, "ft-passo");
        Assert.Equal(0, c.Resources.Remaining("Ki"));

        // Il quarto tentativo non ha più niente da spendere, quale che sia.
        var esito = Engine.Validate(c, c.Features[0], TurnoNuovo(c), Profile);
        Assert.False(esito.IsValid);
        Assert.Contains("Punti Ki", esito.Reason);
    }

    [Fact]
    public void Il_ki_finito_ferma_la_raffica_anche_con_l_azione_bonus_libera()
    {
        var c = Monaco(ki: 0);
        var turno = TurnoNuovo(c);

        var esito = Engine.Validate(c, c.Features[0], turno, Profile);

        Assert.False(esito.IsValid);
        Assert.Equal(1, turno.Remaining("BonusAction"));   // l'azione bonus è ancora lì
    }

    [Fact]
    public void Un_riposo_breve_ricarica_il_ki()
    {
        var c = Monaco(ki: 3);
        Spendi(c, "ft-raffica");
        Spendi(c, "ft-difesa");

        c.CompleteRest("ShortRest", Profile);

        Assert.Equal(3, c.Resources.Remaining("Ki"));
    }

    /// <summary>
    /// Il guerriero non ha punti ki, e non deve vederseli comparire a zero:
    /// la riserva sta sulla scheda solo di chi ce l'ha.
    /// </summary>
    [Fact]
    public void Chi_non_ha_capacita_da_ki_non_ha_la_riserva()
    {
        var guerriero = new Character
        {
            Id = "pg-guerriero",
            Name = "Guerriero",
            Sources = [new Source { Id = "src-guerriero", Name = "Guerriero", Type = SourceType.Class }],
        };

        Assert.False(guerriero.Resources.Has("Ki"));
        Assert.DoesNotContain("Ki", guerriero.Resources.Snapshot().Keys);
    }

    private static TurnState TurnoNuovo(Character c) => new(c.EffectiveTurnLimits(Profile));

    /// <summary>Spende una feature in un turno tutto suo: qui si guarda il ki, non il turno.</summary>
    private static void Spendi(Character c, string featureId)
    {
        var esito = Engine.Apply(c, new Intent(c.Id, featureId), TurnoNuovo(c), Profile);
        Assert.True(esito.IsValid, esito.Reason);
    }
}
