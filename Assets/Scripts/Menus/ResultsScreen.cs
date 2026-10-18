using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// One line of the result screen: plain text, or text with "{0}" where a number rolls up from 0.
public struct ResultLine
{
    public string text;
    public int value;
    public bool rolls;

    public ResultLine(string text)
    {
        this.text = text;
        value = 0;
        rolls = false;
    }

    public ResultLine(string text, int value)
    {
        this.text = text;
        this.value = value;
        rolls = true;
    }
}

// The town's result screen after a dive: the lines appear one after another and their numbers roll up
// (unscaled time). Continue (or Esc, via TownManager) first shows everything at once, then closes and runs `then`.
// Lines not shown yet are already in the text, invisible, so the best-fit font size doesn't change as they appear.
public class ResultsScreen : MonoBehaviour
{
    const float LineTime = 0.15f; // a new line every this many seconds
    const float RollTime = 0.4f;  // a number takes this long to roll up

    [Header("Scene references")]
    public GameObject panel;
    public Text heading;
    public Text body;
    public Button continueButton;

    readonly List<ResultLine> lines = new List<ResultLine>();
    readonly StringBuilder text = new StringBuilder();
    System.Action then;
    float time;
    bool finished;

    public bool IsOpen => panel.activeSelf;

    void Start()
    {
        continueButton.onClick.AddListener(Continue);
    }

    public void Show(string title, List<ResultLine> resultLines, System.Action onClosed)
    {
        heading.text = title;
        lines.Clear();
        lines.AddRange(resultLines);
        then = onClosed;
        time = 0f;
        finished = false;
        panel.SetActive(true);
        Draw();
    }

    // Shows everything if it's still rolling; otherwise closes.
    public void Continue()
    {
        if (!finished)
        {
            time = float.MaxValue / 2f;
            Draw();
            return;
        }
        panel.SetActive(false);
        then?.Invoke();
    }

    void Update()
    {
        if (!IsOpen || finished) return;
        time += Time.unscaledDeltaTime;
        Draw();
    }

    void Draw()
    {
        text.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            ResultLine line = lines[i];
            float shownFor = time - i * LineTime;
            int value = line.rolls ? Mathf.RoundToInt(line.value * Mathf.Clamp01(shownFor / RollTime)) : 0;
            string row = line.rolls ? line.text.Replace("{0}", value.ToString()) : line.text;
            if (shownFor < 0f) row = $"<color=#00000000>{line.text.Replace("{0}", line.value.ToString())}</color>";
            text.AppendLine(row);
        }
        body.text = text.ToString();
        finished = time >= (lines.Count - 1) * LineTime + RollTime;
    }
}
