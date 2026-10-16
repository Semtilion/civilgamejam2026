using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class ItemStack
{
    public SkillData item;
    public int count;
}

// Runs one fight: one hero vs up to 4 enemies standing in ranks 1-4 (Darkest Dungeon layout).
// Turns use an FF7-style ATB gauge in Wait mode: gauges fill by speed, and time freezes while anyone acts or the hero chooses.
// Optional QTE mode (backup system): hero skills play a quick-time event, enemy attacks can be dodged or parried.
public class CombatManager : MonoBehaviour
{
    [Header("Fight setup")]
    public UnitData heroData;
    [Tooltip("Element 0 = rank 1 (closest to the hero). Max 4.")]
    public List<UnitData> enemyGroup = new List<UnitData>();
    public List<ItemStack> items = new List<ItemStack>();

    [Header("Timing")]
    [Tooltip("Seconds a speed-1 unit needs to fill its gauge. Fill time = this / speed (speed 10 = 2s).")]
    [Min(0.1f)] public float gaugeFillTime = 20f;
    [Tooltip("Pause after each action so the player can follow the fight.")]
    public float actionDelay = 0.8f;

    [Header("QTE mode (backup system)")]
    [Tooltip("On: hero skills play their QTE (damage multiplier instead of the accuracy roll), enemy attacks can be dodged (Space) or parried (E).")]
    public bool useQtes;
    public QteController qte;
    [Tooltip("The automatic counterattack after a parry.")]
    public SkillData counterSkill;

    [Header("Scene references")]
    public Unit hero;
    public Unit[] enemySlots;     // left to right = rank 1 to 4
    public Button[] skillButtons; // 5
    public Button[] itemButtons;  // 3
    public Text logText;
    public GameObject resultPanel;
    public Text resultText;
    public Button restartButton;

    const float CritMultiplier = 1.5f;
    const int MaxLogLines = 7;

    readonly List<Unit> enemies = new List<Unit>();
    readonly Queue<string> logLines = new Queue<string>();
    SkillData selectedSkill;
    ItemStack selectedItem;
    Unit selectedTarget;
    bool waitingForHero;

    void Start()
    {
        if (useQtes && qte == null)
        {
            Debug.LogError("Use Qtes is on but no QteController is assigned. Using normal combat.");
            useQtes = false;
        }

        resultPanel.SetActive(false);
        hero.Setup(heroData, this);

        for (int i = 0; i < enemySlots.Length; i++)
        {
            if (i < enemyGroup.Count && enemyGroup[i] != null)
            {
                enemySlots[i].Setup(enemyGroup[i], this);
                enemies.Add(enemySlots[i]);
            }
            else
            {
                enemySlots[i].gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < skillButtons.Length; i++)
        {
            int index = i; // copy for the lambda
            skillButtons[i].onClick.AddListener(() => OnSkillClicked(index));
        }
        for (int i = 0; i < itemButtons.Length; i++)
        {
            int index = i;
            itemButtons[i].onClick.AddListener(() => OnItemClicked(index));
        }
        restartButton.onClick.AddListener(Restart);

        RefreshButtons(false);
        StartCoroutine(BattleLoop());
    }

    // ---------- Turn flow ----------

    IEnumerator BattleLoop()
    {
        Log("Battle start!");
        while (true)
        {
            // Fill every gauge until someone is ready. This only runs between actions, which is what makes it Wait mode.
            Unit actor = null;
            while (actor == null)
            {
                yield return null;
                List<Unit> units = LivingUnits();
                foreach (Unit unit in units) unit.FillGauge(unit.speed * Time.deltaTime / gaugeFillTime);
                actor = units.FirstOrDefault(u => u.GaugeFull); // hero is first in the list, so the hero wins ties
            }

            actor.ResetGauge();
            if (actor == hero) yield return HeroTurn();
            else yield return EnemyTurn(actor);

            if (hero.IsDead || LivingEnemies().Count == 0)
            {
                EndBattle();
                yield break;
            }
            yield return new WaitForSeconds(actionDelay);
        }
    }

    IEnumerator HeroTurn()
    {
        if (hero.manaRegen > 0) hero.ChangeMana(hero.manaRegen);

        selectedSkill = null;
        selectedItem = null;
        selectedTarget = null;
        waitingForHero = true;
        RefreshButtons(true);
        Log("Your turn.");

        if (!AnyActionUsable())
        {
            Log("Nothing usable. Turn skipped.");
            waitingForHero = false;
        }

        yield return new WaitUntil(() => !waitingForHero);
        if (selectedSkill == null) yield break; // turn was skipped

        float? qteMultiplier = null; // null = normal accuracy roll
        if (useQtes && selectedSkill.qte != QteType.None)
        {
            Unit focus = selectedTarget != null ? selectedTarget : hero;
            yield return qte.PlayAttack(selectedSkill.qte, focus.body.rectTransform.position);
            qteMultiplier = qte.AttackMultiplier;
        }
        UseSkill(hero, selectedSkill, selectedTarget, qteMultiplier);
    }

    IEnumerator EnemyTurn(Unit enemy)
    {
        List<SkillData> skills = enemy.data.skills;
        if (skills.Count == 0) yield break;
        SkillData skill = skills[Random.Range(0, skills.Count)];
        if (skill == null) yield break;

        if (!useQtes || skill.target == SkillTarget.Self)
        {
            UseSkill(enemy, skill, hero, null);
            yield break;
        }

        // QTE mode: the hero's dodge/parry replaces the enemy's accuracy roll.
        yield return qte.PlayDefense(enemy, hero);
        if (qte.Defense == DefenseResult.Parry)
        {
            Log($"{hero.DisplayName} parries {enemy.DisplayName}'s {skill.displayName}!");
            if (counterSkill != null) Attack(hero, enemy, counterSkill, 1f);
        }
        else if (qte.Defense == DefenseResult.Dodge)
        {
            Log($"{hero.DisplayName} dodges {enemy.DisplayName}'s {skill.displayName}!");
        }
        else
        {
            UseSkill(enemy, skill, hero, 1f); // a failed dodge always hits
        }
    }

    void EndBattle()
    {
        RefreshButtons(false);
        resultText.text = hero.IsDead ? "DEFEAT" : "VICTORY";
        resultPanel.SetActive(true);
    }

    // Test scene only. The dungeon will take over what happens after a fight.
    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------- Player input ----------

    void OnSkillClicked(int index)
    {
        Select(hero.data.skills[index], null);
    }

    void OnItemClicked(int index)
    {
        Select(items[index].item, items[index]);
    }

    // Single-target picks wait for a click on a highlighted enemy. All/Self act right away.
    void Select(SkillData skill, ItemStack item)
    {
        if (!waitingForHero) return;
        selectedSkill = skill;
        selectedItem = item;
        ClearHighlights();

        if (skill.target == SkillTarget.Single)
        {
            foreach (Unit enemy in ReachableEnemies(skill)) enemy.SetHighlight(true);
        }
        else
        {
            HeroAct(null);
        }
    }

    public void OnUnitClicked(Unit unit)
    {
        if (!waitingForHero || selectedSkill == null || selectedSkill.target != SkillTarget.Single) return;
        if (!ReachableEnemies(selectedSkill).Contains(unit)) return;
        HeroAct(unit);
    }

    // Locks in the choice. HeroTurn then plays the QTE (if any) and resolves it.
    void HeroAct(Unit target)
    {
        selectedTarget = target;
        waitingForHero = false;
        ClearHighlights();
        if (selectedItem != null) selectedItem.count--;
        RefreshButtons(false);
        // Drop the clicked button's focus so Space/arrow keys during QTEs can't press or move between buttons.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ---------- Resolving skills ----------

    // qteMultiplier: null = roll accuracy as normal. Otherwise the skill always lands and its damage/heal is multiplied.
    void UseSkill(Unit user, SkillData skill, Unit target, float? qteMultiplier)
    {
        user.ChangeMana(-skill.manaCost);

        switch (skill.target)
        {
            case SkillTarget.Self:
                int hpBefore = user.hp;
                int manaBefore = user.mana;
                int heal = Random.Range(skill.minAmount, skill.maxAmount + 1);
                if (qteMultiplier != null) heal = Mathf.RoundToInt(heal * qteMultiplier.Value);
                if (heal > 0) user.Heal(heal);
                if (skill.manaRestore > 0) user.ChangeMana(skill.manaRestore);

                string gains = "";
                if (user.hp > hpBefore) gains += $" +{user.hp - hpBefore} HP";
                if (user.mana > manaBefore) gains += $" +{user.mana - manaBefore} MP";
                Log($"{user.DisplayName} uses {skill.displayName}:{(gains == "" ? " no effect" : gains)}.");
                break;

            case SkillTarget.Single:
                Attack(user, target, skill, qteMultiplier);
                break;

            case SkillTarget.All:
                foreach (Unit t in TargetsInReach(user, skill)) Attack(user, t, skill, qteMultiplier);
                break;
        }
    }

    void Attack(Unit attacker, Unit target, SkillData skill, float? qteMultiplier)
    {
        string action = $"{attacker.DisplayName}'s {skill.displayName}";

        if (qteMultiplier == null)
        {
            int hitChance = skill.baseAccuracy + attacker.accuracyMod - target.dodge;
            if (Random.Range(0, 100) >= hitChance)
            {
                Log($"{action} misses {target.DisplayName}.");
                return;
            }
        }

        int damage = Random.Range(skill.minAmount, skill.maxAmount + 1);
        if (qteMultiplier != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * qteMultiplier.Value));
        bool crit = Random.Range(0, 100) < attacker.critChance + skill.critBonus;
        if (crit) damage = Mathf.RoundToInt(damage * CritMultiplier);

        target.TakeDamage(damage);
        Log(crit ? $"{action} CRITS {target.DisplayName} for {damage}!" : $"{action} hits {target.DisplayName} for {damage}.");

        if (target.IsDead)
        {
            Log($"{target.DisplayName} falls!");
            if (target != hero) target.gameObject.SetActive(false); // the layout moves the others forward
        }
    }

    // ---------- Helpers ----------

    List<Unit> LivingEnemies()
    {
        return enemies.Where(e => !e.IsDead).ToList();
    }

    // Hero first, then enemies by rank.
    List<Unit> LivingUnits()
    {
        List<Unit> units = LivingEnemies();
        units.Insert(0, hero);
        return units;
    }

    // Rank = position among living enemies, 1 = closest to the hero.
    List<Unit> ReachableEnemies(SkillData skill)
    {
        List<Unit> living = LivingEnemies();
        List<Unit> result = new List<Unit>();
        for (int i = 0; i < living.Count; i++)
        {
            int rank = i + 1;
            if (rank >= skill.minRank && rank <= skill.maxRank) result.Add(living[i]);
        }
        return result;
    }

    // Enemies only ever face the hero, so their reach doesn't matter.
    List<Unit> TargetsInReach(Unit user, SkillData skill)
    {
        return user == hero ? ReachableEnemies(skill) : new List<Unit> { hero };
    }

    bool CanUse(SkillData skill)
    {
        if (skill == null || hero.mana < skill.manaCost) return false;
        return skill.target == SkillTarget.Self || ReachableEnemies(skill).Count > 0;
    }

    bool AnyActionUsable()
    {
        return hero.data.skills.Any(CanUse) || items.Any(stack => stack.count > 0 && CanUse(stack.item));
    }

    void RefreshButtons(bool heroTurn)
    {
        List<SkillData> skills = hero.data.skills;
        for (int i = 0; i < skillButtons.Length; i++)
        {
            bool exists = i < skills.Count && skills[i] != null;
            skillButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;

            SkillData skill = skills[i];
            SetLabel(skillButtons[i], skill.manaCost > 0 ? $"{skill.displayName}\n{skill.manaCost} MP" : skill.displayName);
            skillButtons[i].interactable = heroTurn && CanUse(skill);
        }

        for (int i = 0; i < itemButtons.Length; i++)
        {
            bool exists = i < items.Count && items[i].item != null;
            itemButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;

            ItemStack stack = items[i];
            SetLabel(itemButtons[i], $"{stack.item.displayName} x{stack.count}");
            itemButtons[i].interactable = heroTurn && stack.count > 0 && CanUse(stack.item);
        }
    }

    static void SetLabel(Button button, string text)
    {
        button.GetComponentInChildren<Text>().text = text;
    }

    void ClearHighlights()
    {
        foreach (Unit enemy in enemies) enemy.SetHighlight(false);
    }

    void Log(string message)
    {
        logLines.Enqueue(message);
        while (logLines.Count > MaxLogLines) logLines.Dequeue();
        logText.text = string.Join("\n", logLines);
    }
}
