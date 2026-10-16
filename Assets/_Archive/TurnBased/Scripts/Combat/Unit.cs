using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One combatant on screen. Holds its live stats for the fight and draws its own name, bars and highlight.
public class Unit : MonoBehaviour, IPointerClickHandler
{
    [Header("Scene references")]
    public Image body;
    public Text nameText;
    public Text statusText;
    public RectTransform hpFill;
    public RectTransform manaFill; // optional, hero only
    public RectTransform atbFill;
    public GameObject highlight;

    [Header("Live stats (copied from data at Setup, buffs/debuffs change these)")]
    public UnitData data;
    public int hp, maxHp, mana, maxMana, manaRegen, speed, accuracyMod, critChance, dodge;
    [Range(0f, 1f)] public float atb; // ATB gauge, acts when it reaches 1

    CombatManager manager;

    public bool IsDead => hp <= 0;
    public bool GaugeFull => atb >= 1f;
    public string DisplayName => data.displayName;

    public void Setup(UnitData unitData, CombatManager combatManager)
    {
        data = unitData;
        manager = combatManager;

        maxHp = data.maxHp;
        hp = maxHp;
        maxMana = data.maxMana;
        mana = maxMana;
        manaRegen = data.manaRegen;
        speed = data.speed;
        accuracyMod = data.accuracyMod;
        critChance = data.critChance;
        dodge = data.dodge;

        nameText.text = data.displayName;
        body.sprite = data.sprite;
        body.color = data.sprite != null ? Color.white : data.placeholderColor;

        gameObject.SetActive(true);
        SetHighlight(false);
        ResetGauge();
        Refresh();
    }

    public void FillGauge(float amount)
    {
        atb = Mathf.Min(1f, atb + amount);
        SetFill(atbFill, atb);
    }

    public void ResetGauge()
    {
        atb = 0f;
        SetFill(atbFill, 0f);
    }

    public void TakeDamage(int amount)
    {
        hp = Mathf.Max(0, hp - amount);
        Refresh();
    }

    public void Heal(int amount)
    {
        hp = Mathf.Min(maxHp, hp + amount);
        Refresh();
    }

    public void ChangeMana(int amount)
    {
        mana = Mathf.Clamp(mana + amount, 0, maxMana);
        Refresh();
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    void Refresh()
    {
        SetFill(hpFill, maxHp > 0 ? (float)hp / maxHp : 0f);
        SetFill(manaFill, maxMana > 0 ? (float)mana / maxMana : 0f);
        statusText.text = maxMana > 0 ? $"HP {hp}/{maxHp}   MP {mana}/{maxMana}" : $"HP {hp}/{maxHp}";
    }

    // Bars are stretched to their background; moving the right anchor shows how full they are.
    // Null-safe because the mana bar only exists on the hero.
    static void SetFill(RectTransform fill, float amount)
    {
        if (fill != null) fill.anchorMax = new Vector2(amount, 1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null) manager.OnUnitClicked(this);
    }
}
