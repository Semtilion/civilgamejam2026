using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// In-dive leveling (on by default, resets every dive): XP gems -> level up -> pause and pick 1 of 3 random cards.
// Cards are deliberately small next to the town (the main source of power):
// stat cards, and +damage for weapons already unlocked in town. Cards never give new weapons.
public class LevelUp : MonoBehaviour
{
    // Stat cards: what each one raises and by how much per pick (units as in Player.AddStat).
    static readonly StatType[] CardStats = { StatType.MaxHp, StatType.MoveSpeed, StatType.Damage, StatType.AttackSpeed, StatType.Area, StatType.PickupRange };
    static readonly float[] CardAmounts = { 4f, 0.02f, 0.02f, 0.02f, 0.02f, 0.05f };
    static readonly string[] CardNames = { "Max HP", "Move Speed", "Damage", "Attack Speed", "Area", "Pickup Range" };
    const int MaxRank = 5;               // max picks per stat card and per weapon card
    const float WeaponCardPower = 0.2f;  // a weapon card gives 20% of a smith level's damage (rounded up)

    class Card
    {
        public string title;
        public string description;
        public System.Action apply;
    }

    [Tooltip("XP gems and level-up cards during dives. Off = all power from the town only.")]
    public bool levelingEnabled = true;

    [Header("XP needed for the next level = xpBase + xpPerLevel x current level")]
    public int xpBase = 5;
    public int xpPerLevel = 5;

    [Header("Scene references")]
    [Tooltip("Every weapon on the hero (the town decides which are owned).")]
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

    int level = 1;
    int xp;
    int pendingPicks;
    readonly int[] statRanks = new int[CardStats.Length];
    int[] weaponRanks = new int[0];

    int XpToNext => xpBase + xpPerLevel * level;

    // Level 1, no XP, no card bonuses. (Which weapons are owned is set by the town loadout.)
    public void ResetForNewRun()
    {
        level = 1;
        xp = 0;
        pendingPicks = 0;
        System.Array.Clear(statRanks, 0, statRanks.Length);
        weaponRanks = new int[weapons.Length];
        foreach (Weapon weapon in weapons) weapon.cardDamage = 0;
        cardPanel.SetActive(false);
    }

    void Update()
    {
        if (xpHud.activeSelf != levelingEnabled) xpHud.SetActive(levelingEnabled);
        xpFill.anchorMax = new Vector2(Mathf.Clamp01((float)xp / XpToNext), 1f);
        levelText.text = $"Lv {level}";
    }

    public void AddXp(int amount)
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

        Time.timeScale = 0f;
        cardPanel.SetActive(true);
        for (int i = 0; i < cardButtons.Length; i++)
        {
            bool show = i < cards.Count;
            cardButtons[i].gameObject.SetActive(show);
            if (!show) continue;

            Card card = cards[i];
            cardTitles[i].text = card.title;
            cardDescriptions[i].text = card.description;
            cardButtons[i].onClick.RemoveAllListeners();
            cardButtons[i].onClick.AddListener(() => Pick(card));
        }
    }

    void Pick(Card card)
    {
        card.apply();
        pendingPicks--;
        cardPanel.SetActive(false);
        Time.timeScale = 1f;
        TryShowCards(); // several level-ups at once (e.g. after clearing a depth)
    }

    List<Card> AvailableCards()
    {
        List<Card> cards = new List<Card>();

        // +damage for weapons the town has unlocked (owned = enabled).
        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon weapon = weapons[i];
            if (!weapon.enabled || weaponRanks[i] >= MaxRank) continue;
            int index = i;
            int bonus = Mathf.CeilToInt(weapon.damagePerLevel * WeaponCardPower);
            cards.Add(new Card
            {
                title = $"{weapon.DisplayName} {weaponRanks[i] + 1}/{MaxRank}",
                description = $"+{bonus} damage\n(this dive only)",
                apply = () =>
                {
                    weaponRanks[index]++;
                    weapon.cardDamage += bonus;
                },
            });
        }

        for (int i = 0; i < CardStats.Length; i++)
        {
            if (statRanks[i] >= MaxRank) continue;
            int index = i;
            cards.Add(new Card
            {
                title = $"{CardNames[i]} {statRanks[i] + 1}/{MaxRank}",
                description = Player.StatText(CardStats[i], CardAmounts[i]) + "\n(this dive only)",
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
