using UnityEngine;
using UnityEngine.UI;

// Greys out a button's label (and its frame) while the button can't be used (can't afford it, last page, ...). The
// button's own color states only tint its background. Added to every button by the scene builders.
public class ButtonLabelDim : MonoBehaviour
{
    static readonly Color DimColor = new Color(1f, 1f, 1f, 0.35f);

    public Button button;
    public Text label;
    public Color labelColor = Color.white;
    [Tooltip("The button's frame (optional), shown in frameColor, faded while the button can't be used.")]
    public Image frame;
    public Color frameColor = Color.white;

    void Update()
    {
        bool usable = button.IsInteractable();
        label.color = usable ? labelColor : DimColor; // only redraws when it changes
        if (frame != null) frame.color = usable ? frameColor : new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f);
    }
}
