using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Types a Text out letter by letter (unscaled time); clicking the text shows the rest at once. The letters not typed
// yet are already there but invisible, so words don't jump to the next line while they're being typed.
// The text needs Raycast Target on for the click.
[RequireComponent(typeof(Text))]
public class Typewriter : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Letters per second.")]
    public float speed = 120f;

    Text text;
    string full = "";
    float typed;

    // Starts typing `line` from the first letter.
    public void Play(string line)
    {
        full = line;
        typed = 0f;
        Draw();
    }

    // Swaps in new text (e.g. the numbers changed) without starting over.
    public void Set(string line)
    {
        full = line;
        Draw();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        typed = full.Length;
        Draw();
    }

    void Update()
    {
        if (typed >= full.Length) return;
        typed += speed * Time.unscaledDeltaTime;
        Draw();
    }

    void Draw()
    {
        if (text == null) text = GetComponent<Text>(); // here, not in Awake: Play can come before the window is first shown
        int count = Mathf.Min((int)typed, full.Length);
        text.text = count >= full.Length ? full : full.Substring(0, count) + "<color=#00000000>" + full.Substring(count) + "</color>";
    }
}
