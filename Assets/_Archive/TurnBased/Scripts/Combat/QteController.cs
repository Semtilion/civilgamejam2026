using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum DefenseResult { Hit, Dodge, Parry }

// Backup combat feel (Mario & Luigi / Clair Obscur): timed button presses on top of the ATB fight.
// CombatManager yields PlayAttack / PlayDefense, then reads AttackMultiplier / Defense.
// Controls: Space = timing + dodge, E = parry, arrow keys = key sequences.
public class QteController : MonoBehaviour
{
    enum Grade { Miss, Good, Perfect }

    static readonly Key[] Arrows = { Key.LeftArrow, Key.UpArrow, Key.RightArrow, Key.DownArrow };
    static readonly string[] ArrowSymbols = { "←", "↑", "→", "↓" };
    const float RingStartScale = 3f;
    const float FeedbackTime = 0.7f;

    [Header("Timing windows (seconds, ± around the target moment)")]
    [Tooltip("Attack: Perfect. Defense: parry window.")]
    public float perfectWindow = 0.06f;
    [Tooltip("Attack: Good. Defense: dodge window.")]
    public float goodWindow = 0.15f;

    [Header("QTE lengths (seconds)")]
    public float ringTime = 1f;
    public float barTime = 1.2f;
    public float sequenceTime = 2.5f;
    public int sequenceLength = 4;
    [Tooltip("Enemy attacks: time from the start of the lunge to the hit.")]
    public float defenseWindup = 1f;
    public float lungeDistance = 80f;

    [Header("Attack damage multipliers")]
    public float perfectMultiplier = 1.5f;
    public float goodMultiplier = 1f;
    public float missMultiplier = 0.5f;

    [Header("Scene references")]
    public Text promptText;
    public Text feedbackText;
    public RectTransform ring;          // moved over the target
    public RectTransform ringApproach;  // the shrinking circle
    public RectTransform bar;
    public RectTransform barCursor;
    public RectTransform barGoodZone;
    public RectTransform barPerfectZone;
    public GameObject sequence;
    public Text sequenceText;
    public RectTransform sequenceTimerFill;

    public float AttackMultiplier { get; private set; } = 1f;
    public DefenseResult Defense { get; private set; }

    int pressedKey;    // index into the keys being listened for, -1 = no press
    float pressError;  // seconds between the press and the target moment
    Grade sequenceGrade;
    float hideFeedbackAt;

    void Awake()
    {
        ring.gameObject.SetActive(false);
        bar.gameObject.SetActive(false);
        sequence.SetActive(false);
        feedbackText.gameObject.SetActive(false);
        promptText.text = "";
    }

    void Update()
    {
        if (feedbackText.gameObject.activeSelf && Time.time >= hideFeedbackAt)
            feedbackText.gameObject.SetActive(false);
    }

    // ---------- Called by CombatManager ----------

    public IEnumerator PlayAttack(QteType type, Vector3 targetPosition)
    {
        Grade grade;
        switch (type)
        {
            case QteType.TimingRing:
                promptText.text = "Press SPACE when the circles meet!";
                yield return Ring(targetPosition, ringTime, Key.Space);
                grade = GradePress();
                break;

            case QteType.MovingBar:
                promptText.text = "Press SPACE in the middle!";
                yield return Bar();
                grade = GradePress();
                break;

            default: // KeySequence
                promptText.text = "Type the arrows!";
                yield return Sequence();
                grade = sequenceGrade;
                break;
        }
        promptText.text = "";

        AttackMultiplier = grade == Grade.Perfect ? perfectMultiplier : grade == Grade.Good ? goodMultiplier : missMultiplier;
        ShowFeedback(grade == Grade.Perfect ? "PERFECT!" : grade == Grade.Good ? "GOOD" : "MISS");
    }

    // The attacker lunges while a ring shrinks around the hero. Dodge (Space) or parry (E) as it lands.
    public IEnumerator PlayDefense(Unit attacker, Unit hero)
    {
        promptText.text = "SPACE = Dodge      E = Parry";
        Coroutine lunge = StartCoroutine(Lunge(attacker.body.rectTransform));
        yield return Ring(hero.body.rectTransform.position, defenseWindup, Key.Space, Key.E);
        yield return lunge; // the hit lands when the lunge ends, even after an early press
        promptText.text = "";

        if (pressedKey == 1 && pressError <= perfectWindow) Defense = DefenseResult.Parry;
        else if (pressedKey == 0 && pressError <= goodWindow) Defense = DefenseResult.Dodge;
        else Defense = DefenseResult.Hit;

        if (Defense == DefenseResult.Parry) ShowFeedback("PARRY!");
        else if (Defense == DefenseResult.Dodge) ShowFeedback("DODGE!");
    }

    // ---------- QTE routines ----------

    // Shrinks the approach circle onto the target circle over `duration`. Only the first key press counts.
    IEnumerator Ring(Vector3 position, float duration, params Key[] keys)
    {
        ring.position = position;
        ring.gameObject.SetActive(true);
        pressedKey = -1;

        for (float t = 0f; t <= duration + goodWindow; t += Time.deltaTime)
        {
            ringApproach.localScale = Vector3.one * Mathf.LerpUnclamped(RingStartScale, 1f, t / duration);
            pressedKey = FirstPressed(keys);
            if (pressedKey >= 0)
            {
                pressError = Mathf.Abs(t - duration);
                break;
            }
            yield return null;
        }

        ring.gameObject.SetActive(false);
    }

    // One sweep of a cursor across the bar. The middle of the sweep is the target moment.
    IEnumerator Bar()
    {
        bar.gameObject.SetActive(true);
        float width = bar.rect.width;
        SetWidth(barPerfectZone, width * 2f * perfectWindow / barTime);
        SetWidth(barGoodZone, width * 2f * goodWindow / barTime);
        pressedKey = -1;

        for (float t = 0f; t <= barTime; t += Time.deltaTime)
        {
            barCursor.anchoredPosition = new Vector2((t / barTime - 0.5f) * width, 0f);
            pressedKey = FirstPressed(Key.Space);
            if (pressedKey >= 0)
            {
                pressError = Mathf.Abs(t - barTime / 2f);
                break;
            }
            yield return null;
        }

        bar.gameObject.SetActive(false);
    }

    // All keys right = Perfect, at least half = Good, otherwise Miss. A wrong key ends it.
    IEnumerator Sequence()
    {
        int[] keys = new int[sequenceLength];
        for (int i = 0; i < keys.Length; i++) keys[i] = Random.Range(0, Arrows.Length);
        int done = 0;
        bool wrong = false;
        sequence.SetActive(true);

        for (float t = 0f; t < sequenceTime && done < keys.Length && !wrong; t += Time.deltaTime)
        {
            int pressed = FirstPressed(Arrows);
            if (pressed >= 0)
            {
                if (pressed == keys[done]) done++;
                else wrong = true;
            }
            DrawSequence(keys, done, wrong);
            sequenceTimerFill.anchorMax = new Vector2(1f - t / sequenceTime, 1f);
            yield return null;
        }

        sequence.SetActive(false);
        sequenceGrade = done == keys.Length ? Grade.Perfect : done * 2 >= keys.Length ? Grade.Good : Grade.Miss;
    }

    IEnumerator Lunge(RectTransform body)
    {
        Vector2 start = body.anchoredPosition;
        for (float t = 0f; t < defenseWindup; t += Time.deltaTime)
        {
            body.anchoredPosition = start + Vector2.left * (lungeDistance * t / defenseWindup);
            yield return null;
        }
        body.anchoredPosition = start;
    }

    // ---------- Helpers ----------

    Grade GradePress()
    {
        if (pressedKey < 0) return Grade.Miss;
        if (pressError <= perfectWindow) return Grade.Perfect;
        return pressError <= goodWindow ? Grade.Good : Grade.Miss;
    }

    static int FirstPressed(params Key[] keys)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return -1;
        for (int i = 0; i < keys.Length; i++)
            if (keyboard[keys[i]].wasPressedThisFrame) return i;
        return -1;
    }

    void DrawSequence(int[] keys, int done, bool wrong)
    {
        string text = "";
        for (int i = 0; i < keys.Length; i++)
        {
            string color = i < done ? "#55ff55" : (wrong && i == done) ? "#ff5555" : "#ffffff";
            text += $"<color={color}>{ArrowSymbols[keys[i]]}</color>  ";
        }
        sequenceText.text = text;
    }

    void ShowFeedback(string message)
    {
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);
        hideFeedbackAt = Time.time + FeedbackTime;
    }

    static void SetWidth(RectTransform rect, float width)
    {
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
    }
}
