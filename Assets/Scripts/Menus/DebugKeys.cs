using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Test keys, only in the editor and in Development builds (never in the build you submit):
//   F1  dungeon: a list of every relic; click one to get it (works right away, owned for good)
//   F2  dungeon: level up once
//   F3  town or dungeon: +200 gold, +100 materials, +10 crystals, +50 supplies (the town's stock)
//   F4  dungeon: the depth's kill goal is met (teleporter, or the boss on a boss depth); a raid: its timer ends
// A small hint in the corner lists the keys and the last one used.
public class DebugKeys : MonoBehaviour
{
    const int Gold = 200;
    const int Materials = 100;
    const int Crystals = 10;
    const int Supplies = 50;
    const string Keys = "DEBUG  F1 relics  F2 level up  F3 resources  F4 clear depth";

    [Header("Scene references (one of these)")]
    public ArenaManager arena;
    public TownManager town;

    [Header("UI")]
    public Text hintText;
    [Tooltip("Dungeon only: the relic list (buttons in RelicId order).")]
    public GameObject relicPanel;
    public Button[] relicButtons;
    public Button relicCloseButton;

    public bool IsOpen => relicPanel != null && relicPanel.activeSelf;

    void Start()
    {
        if (!Debug.isDebugBuild)
        {
            hintText.gameObject.SetActive(false);
            enabled = false;
            return;
        }
        hintText.text = Keys;
        if (relicPanel == null) return;

        for (int i = 0; i < relicButtons.Length; i++)
        {
            RelicId relic = (RelicId)i; // copy for the lambda
            relicButtons[i].onClick.AddListener(() => GiveRelic(relic));
        }
        relicCloseButton.onClick.AddListener(CloseRelics);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f3Key.wasPressedThisFrame) AddResources();
        if (arena == null) return;

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            if (IsOpen) CloseRelics();
            else if (arena.IsPlaying) OpenRelics();
        }
        if (!arena.IsPlaying) return;
        if (keyboard.f2Key.wasPressedThisFrame)
        {
            arena.leveling.DebugLevelUp();
            Report("level up");
        }
        if (keyboard.f4Key.wasPressedThisFrame)
        {
            arena.DebugClearDepth();
            Report("depth cleared");
        }
    }

    void AddResources()
    {
        Campaign campaign = Campaign.Current;
        if (campaign == null) return;
        campaign.gold += Gold;
        campaign.materials += Materials;
        campaign.crystals += Crystals;
        campaign.supplies += Supplies;
        if (town != null) town.Refresh();
        Report($"+{Gold} gold, +{Materials} materials, +{Crystals} crystals, +{Supplies} supplies (town stock)");
    }

    // Pauses the game while the list is open.
    void OpenRelics()
    {
        for (int i = 0; i < relicButtons.Length; i++)
        {
            bool owned = Campaign.Current.relicOwned[i] || arena.relics.Has((RelicId)i);
            relicButtons[i].GetComponentInChildren<Text>().text = owned ? $"{Relics.Names[i]} (have it)" : Relics.Names[i];
            relicButtons[i].interactable = !owned;
        }
        relicPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    void CloseRelics()
    {
        relicPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    void GiveRelic(RelicId relic)
    {
        arena.relics.DebugGive(relic);
        Campaign.Current.relicOwned[(int)relic] = true;
        Report("relic: " + Relics.Names[(int)relic]);
        CloseRelics();
    }

    void Report(string action)
    {
        hintText.text = $"{Keys}\nLast: {action}";
        Debug.Log("Debug key: " + action);
    }
}
