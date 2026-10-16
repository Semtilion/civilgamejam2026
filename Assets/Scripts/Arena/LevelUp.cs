using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// In-dive leveling (on by default, resets every dive): XP gems -> level up -> pause and pick 1 of 3 random cards,
// by clicking or pressing 1 / 2 / 3. Cards: a weapon the town has unlocked (arrives at Lv 1), a level-up for a weapon
// you have (damage, plus its bonus at Lv 3 and 5), or a small stat boost.
public class LevelUp : MonoBehaviour
{
    // Stat cards: what each one raises and by how much per pick (units as in Player.AddStat).
    static readonly StatType[] CardStats = { StatType.MaxHp, StatType.MoveSpeed, StatType.Damage, StatType.AttackSpeed, StatType.Area, StatType.PickupRange };
    static readonly float[] CardAmounts = { 4f, 0.02f, 0.02f, 0.02f, 0.02f, 0.05f };
    static readonly string[] CardNames = { "Max HP", "Move Speed", "Damage", "Attack Speed", "Area", "Pickup Range" };
    static readonly Key[] PickKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
    static readonly Key[] PickKeysNumpad = { Key.Numpad1, Key.Numpad2, Key.Numpad3 };
    const int MaxStatRank = 5;

    class Card
    {
        public string title;
        public string description;
        public System.Action apply;
    }

    [Tooltip("XP gems and level-up cards during dives. Off = the Sword only, all other power from the town.")]
    public bool levelingEnabled = true;

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
    public Button[] cardButtons;
    public Text[] cardTitles;
    public Text[] cardDescriptions;

    public bool IsChoosing => cardPanel.activeSelf;
    // XP gem multiplier (the Scrying Pool building), set by the arena at the start of every dive.
    public float XpGain { get; set; } = 1f;

    int level = 1;
    float xp; // fractions add up with XpGain
    int pendingPicks;
    readonly int[] statRanks = new int[CardStats.Length];
    readonly List<Card> shownCards = new List<Card>();
    bool[] unlockedWeapons = new bool[0]; // by WeaponId, from the town

    int XpToNext => xpBase + xpPerLevel * level;

    // Level 1, no XP, no stat cards. `unlocked` = the weapons the town has unlocked (Campaign.weaponUnlocked).
    public void ResetForNewRun(bool[] unlocked)
    {
        unlockedWeapons = unlocked;
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

        shownCards.Clear();
        Time.timeScale = 0f;
        cardPanel.SetActive(true);
        for (int i = 0; i < cardButtons.Length; i++)
        {
            bool show = i < cards.Count;
            cardButtons[i].gameObject.SetActive(show);
            if (!show) continue;

            Card card = cards[i];
            shownCards.Add(card);
            cardTitles[i].text = $"[{i + 1}] {card.title}";
            cardDescriptions[i].text = card.description;
            cardButtons[i].onClick.RemoveAllListeners();
            cardButtons[i].onClick.AddListener(() => Pick(card));
        }
    }

    void Pick(Card card)
    {
        card.apply();
        pendingPicks--;
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
            bool unlocked = (int)weapon.id < unlockedWeapons.Length && unlockedWeapons[(int)weapon.id];
            if (!weapon.enabled && unlocked)
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
                cards.Add(new Card
                {
                    title = $"{weapon.DisplayName} Lv {next}",
                    description = $"+{weapon.damagePerLevel} damage{bonus}",
                    apply = () => weapon.level++,
                });
            }
        }

        for (int i = 0; i < CardStats.Length; i++)
        {
            if (statRanks[i] >= MaxStatRank) continue;
            int index = i;
            cards.Add(new Card
            {
                title = $"{CardNames[i]} {statRanks[i] + 1}/{MaxStatRank}",
                description = Player.StatText(CardStats[i], CardAmounts[i]),
                apply = () =>
                {
                    statRanks[index]++;
                    player.AddStat(CardStats[index], CardAmounts[index]);
                },
            });
        }
        return cards;
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
