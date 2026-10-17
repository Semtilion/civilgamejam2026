using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Buttons feel alive: they grow a little under the mouse, shrink while pressed and bounce when clicked.
// Only while the button can be used. Unscaled time (works on pause panels). Added to every button by the builders.
public class ButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    const float HoverScale = 1.06f;
    const float PressScale = 0.94f;
    const float BounceScale = 1.14f;
    const float Speed = 18f;

    public Button button;

    bool hovered, pressed;
    float scale = 1f;

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnPointerDown(PointerEventData eventData) => pressed = true;

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pressed && hovered && button.IsInteractable()) scale = BounceScale; // released on it = a click
        pressed = false;
    }

    void OnDisable()
    {
        hovered = pressed = false;
        scale = 1f;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        bool usable = button.IsInteractable();
        float target = !usable ? 1f : pressed ? PressScale : hovered ? HoverScale : 1f;
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
        transform.localScale = Vector3.one * scale;
    }
}
