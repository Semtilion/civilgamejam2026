using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// In-dive leveling (on by default, resets every dive): XP gems -> level up -> pause and pick 1 of 3 random cards,
// by clicking or pressing 1 / 2 / 3. Cards: a new weapon (Sword, Dagger, Spear or Crystal Orbit; arrives at Lv 1), a
// level-up for a weapon you have (damage, plus its bonus at Lv 3 and 5), a stat boost, or, for a Lv 5 weapon whose
// evolution the town has unlocked, its evolution (gold title; always one of the 3 when there is one; at most
// maxEvolutions per dive). Stat and weapon level-up cards can come Blue (x1.5 the boost) or Gold (x2): title, frame
// and a tint in that color. The same panel shows a single relic card when a relic pickup is collected.
public class LevelUp : MonoBehaviour
{
    // Stat cards: what each one raises and by how much per pick (units as in Player.AddStat).
    static readonly StatType[] CardStats = { StatType.MaxHp, StatType.MoveSpeed, StatType.Damage, StatType.AttackSpeed, StatType.Area, StatType.PickupRange };
    static readonly float[] CardAmounts = { 10f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f };
    static readonly string[] CardNames = { "Max HP", "Move Speed", "Damage", "Attack Speed", "Area", "Pickup Range" };
    static readonly Key[] PickKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
    static readonly Key[] PickKeysNumpad = { Key.Numpad1, Key.Numpad2, Key.Numpad3 };
    const int MaxStatRank = 5;
    // Rarities: common, Blue, Gold (index = Card.rarity): how much more the card gives, its color, and the chances.
    static readonly float[] RarityScale = { 1f, 1.5f, 2f };
    static readonly Color[] RarityColors = { Color.white, new Color(0.30f, 0.65f, 1f), new Color(1f, 0.80f, 0.30f) };
    const float BlueChance = 0.25f, GoldChance = 0.10f;
    const float RarityTint = 0.3f; // how far the card's background leans toward its rarity color

    class Card
    {
        public string title;
        public string description;
        public System.Action apply;
        public bool evolution;
        public int rarity; // 0 common, 1 Blue, 2 Gold
        public bool relic; // a relic card: taking it doesn't use up a level-up pick
    }

    [Tooltip("XP gems and level-up cards during dives. Off = the Sword only, all other power from the town.")]
    public bool levelingEnabled = true;

    [Tooltip("Evolutions per dive (a relic or boost could raise this later).")]
    public int maxEvolutions = 3;

    [Header("XP needed for the next level = xpBase + xpPerLevel x current level")]
    public int xpBase = 5;
    public int xpPerLevel = 5;

    [Header("Scene references")]
    [Tooltip("Every weapon on the hero.")]
    public Weapon[] weapons;
    public Player player;
    public GameObject xpHud;
    public RectTransform xpFill;
    public Text levelText;
    public GameObject cardPanel;
    [Tooltip("The panel's title: LEVEL UP! or RELIC FOUND.")]
    public Text panelTitle;
    public Button[] cardButtons;
    public Text[] cardTitles;
    public Text[] cardDescriptions;

    public bool IsChoosing => cardPanel.activeSelf;
    public int Level => level;
    // XP gem multiplier (the Scrying Pool building), set by the arena at the start of every dive.
    public float XpGain { get; set; } = 1f;

    int level = 1;
    float xp; // fractions add up with XpGain
    int pendingPicks;
    readonly int[] statRanks = new int[CardStats.Length];
    readonly List<Card> shownCards = new List<Card>();
    bool[] evolutionUnlocked = new bool[0]; // by WeaponId, from the town
    int evolutionsTaken;
    Color cardColor, frameColor; // a common card's background and frame (as built)

    int XpToNext => xpBase + xpPerLevel * level;

    void Awake()
    {
        cardColor = cardButtons[0].colors.normalColor;
        ButtonLabelDim dim = cardButtons[0].GetComponent<ButtonLabelDim>();
        frameColor = dim != null ? dim.frameColor : Color.white;
    }

    // Level 1, no XP, no stat cards, no evolutions. `evolutions` = the ones the town has unlocked
    // (Campaign.evolutionUnlocked).
    public void ResetForNewRun(bool[] evolutions)
    {
        evolutionUnlocked = evolutions;
        evolutionsTaken = 0;
        level = 1;
        xp = 0;
        pendingPicks = 0;
        System.Array.Clear(statRanks, 0, statRanks.Length);
        cardPanel.SetActive(false);
    }

    void Update()
    {
        if (xpHud.activeSelf != levelingEnabled) xpHud.SetActive(levelingEnabled);
        xpFill.anchorMax = new Vector2(Mathf.Clamp01(xp / XpToNext), 1f);
        levelText.text = $"Lv {level}";

        if (IsChoosing) PickWithKeys();
    }

    void PickWithKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        for (int i = 0; i < shownCards.Count && i < PickKeys.Length; i++)
        {
            if (keyboard[PickKeys[i]].wasPressedThisFrame || keyboard[PickKeysNumpad[i]].wasPressedThisFrame)
            {
                Pick(shownCards[i]);
                return;
            }
        }
    }

    // Free level-up picks (the final horde starts with some).
    public void GrantPicks(int count)
    {
        if (!levelingEnabled) return;
        pendingPicks += count;
        TryShowCards();
    }

    // XP from gems (x XpGain).
    public void AddXp(int amount)
    {
        Gain(amount * XpGain);
    }

    // A share of the XP needed for the current level (the XP shrine: 0.25 = a quarter of the bar; XpGain doesn't apply).
    public void AddLevelShare(float share)
    {
        Gain(XpToNext * share);
    }

    // Debug (F2): exactly enough XP for the next level.
    public void DebugLevelUp()
    {
        if (!levelingEnabled) return;
        xp = XpToNext;
        Gain(0f);
    }

    void Gain(float amount)
    {
        if (!levelingEnabled) return;

        xp += amount;
        while (xp >= XpToNext)
        {
            xp -= XpToNext;
            level++;
            pendingPicks++;
        }
        TryShowCards();
    }

    // Shows the cards if a level-up is waiting and no other panel is open.
    // ArenaManager calls this again when its own panel closes (e.g. after "Go deeper").
    public void TryShowCards()
    {
        if (pendingPicks == 0 || IsChoosing || player.arena.PanelOpen) return;

        List<Card> cards = AvailableCards();
        if (cards.Count == 0)
        {
            pendingPicks = 0; // everything is maxed out
            return;
        }
        Shuffle(cards);
        int evolution = cards.FindIndex(c => c.evolution); // an evolution always gets the first slot
        if (evolution > 0) (cards[0], cards[evolution]) = (cards[evolution], cards[0]);
        Show("LEVEL UP!", cards);
    }

    // A relic pickup was collected (the relic already works): one Gold card with it; taking it just closes the panel.
    public void ShowRelic(string name, string description)
    {
        Show("RELIC FOUND", new List<Card> { new Card { title = name, description = description, apply = () => { }, rarity = 2, relic = true } });
    }

    // Pauses and shows the first 3 `cards`, each in its rarity's colors.
    void Show(string heading, List<Card> cards)
    {
        shownCards.Clear();
        Time.timeScale = 0f;
        panelTitle.text = heading;
        cardPanel.SetActive(true);
        for (int i = 0; i < cardButtons.Length; i++)
        {
            bool show = i < cards.Count;
            cardButtons[i].gameObject.SetActive(show);
            if (!show) continue;

            Card card = cards[i];
            shownCards.Add(card);
            string title = card.rarity == 0 ? card.title
                : $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColors[card.rarity])}>{card.title}</color>";
            cardTitles[i].text = $"[{i + 1}] {title}";
            cardDescriptions[i].text = card.description;
            ButtonLabelDim dim = cardButtons[i].GetComponent<ButtonLabelDim>();
            if (dim != null) dim.frameColor = card.rarity == 0 ? frameColor : RarityColors[card.rarity];
            ColorBlock colors = cardButtons[i].colors;
            colors.normalColor = card.rarity == 0 ? cardColor : Color.Lerp(cardColor, RarityColors[card.rarity], RarityTint);
            colors.selectedColor = colors.normalColor;
            cardButtons[i].colors = colors;
            cardButtons[i].onClick.RemoveAllListeners();
            cardButtons[i].onClick.AddListener(() => Pick(card));
        }
    }

    void Pick(Card card)
    {
        card.apply();
        if (!card.relic) pendingPicks--;
        shownCards.Clear();
        cardPanel.SetActive(false);
        Time.timeScale = 1f;
        TryShowCards(); // several level-ups at once (e.g. after clearing a depth)
    }

    List<Card> AvailableCards()
    {
        List<Card> cards = new List<Card>();

        foreach (Weapon weapon in weapons)
        {
            if (!weapon.enabled && !Weapon.IsRelicWeapon(weapon.id)) // relic weapons only come from relics
            {
                cards.Add(new Card
                {
                    title = "NEW: " + weapon.DisplayName,
                    description = weapon.description,
                    apply = () => { weapon.level = 1; weapon.enabled = true; },
                });
            }
            else if (weapon.enabled && weapon.level < Weapon.MaxLevel)
            {
                int next = weapon.level + 1;
                string bonus = next == 3 || next == 5 ? "\n" + weapon.bonusText : "";
                int rarity = RollRarity();
                int gain = Mathf.CeilToInt(weapon.damagePerLevel * RarityScale[rarity]); // Blue / Gold: more damage, still one level
                cards.Add(new Card
                {
                    title = $"{weapon.DisplayName} Lv {next}",
                    description = $"+{gain} damage{bonus}",
                    rarity = rarity,
                    apply = () =>
                    {
                        weapon.level++;
                        weapon.bonusDamage += gain - weapon.damagePerLevel;
                    },
                });
            }
            else if (weapon.enabled && CanEvolve(weapon))
            {
                cards.Add(new Card
                {
                    title = $"<color=#FFCC4D>EVOLVE: {weapon.EvolutionName}</color>",
                    description = $"{weapon.DisplayName}: {weapon.evolutionText}",
                    apply = () =>
                    {
                        weapon.evolved = true;
                        evolutionsTaken++;
                    },
                    evolution = true,
                });
            }
        }

        for (int i = 0; i < CardStats.Length; i++)
        {
            if (statRanks[i] >= MaxStatRank) continue;
            int index = i;
            int rarity = RollRarity();
            float amount = CardAmounts[i] * RarityScale[rarity]; // a Blue / Gold one still counts as one rank
            cards.Add(new Card
            {
                title = $"{CardNames[i]} {statRanks[i] + 1}/{MaxStatRank}",
                description = Player.StatText(CardStats[i], amount),
                rarity = rarity,
                apply = () =>
                {
                    statRanks[index]++;
                    player.AddStat(CardStats[index], amount);
                },
            });
        }
        return cards;
    }

    // Gold (GoldChance), Blue (BlueChance) or common.
    static int RollRarity()
    {
        float roll = Random.value;
        return roll < GoldChance ? 2 : roll < GoldChance + BlueChance ? 1 : 0;
    }

    // At Lv 5, not evolved yet, its evolution unlocked by the town, and this dive's evolutions not used up.
    bool CanEvolve(Weapon weapon)
    {
        int id = (int)weapon.id;
        return weapon.level >= Weapon.MaxLevel && !weapon.evolved && Weapon.HasEvolution(weapon.id) &&
               id < evolutionUnlocked.Length && evolutionUnlocked[id] && evolutionsTaken < maxEvolutions;
    }

    static void Shuffle(List<Card> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
