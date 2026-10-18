using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Scene changes with a transition, on a screen overlay that survives the load (one per game: every scene has one,
// the first stays). Load(scene, style, title, subtitle):
//   Fade:    to black, a card ("Day 4" / "Quake on day 6"; none without a title), the next scene fades in.
//   Descent: the screen sinks out of view (a snapshot slides up) into dark rock with glowing crystal veins and the
//            card, then the rock slides up revealing the next scene, with a flash where the hero arrives (the middle).
// Any key or click skips the card. The next scene is frozen (timeScale 0) until it's revealed. The music fades out
// with the screen and the next scene's song in as it's revealed (Music). Unscaled time.
// Without one in the scene (e.g. pressing Play in a scene that wasn't rebuilt), Load just loads the scene.
public class SceneTransition : MonoBehaviour
{
    public enum Style { Fade, Descent }

    const float FadeTime = 0.4f, CardFadeTime = 0.25f, CardTime = 1f;
    const float SinkTime = 0.7f, RiseTime = 0.6f, FlashTime = 0.45f;

    public static SceneTransition Instance { get; private set; }
    public static bool Busy => Instance != null && Instance.busy; // a scene change is on screen

    [Header("Scene references (children, made by the scene builders)")]
    public CanvasGroup group;      // the whole overlay (blocks clicks while it's up)
    public Image black;
    public RawImage snapshot;      // the screen as it was, sinking away (Descent)
    public Image rock;             // the rock it sinks through (Descent)
    public Image flash;            // where the hero arrives (Descent)
    public Text title, subtitle;   // the card

    bool busy;

    // Changes scene with a transition (or straight away without the overlay). Ignored while one is running.
    public static void Load(string scene, Style style = Style.Fade, string title = null, string subtitle = null)
    {
        if (Instance == null)
        {
            SceneManager.LoadScene(scene);
            return;
        }
        if (Instance.busy) return;
        Instance.StartCoroutine(style == Style.Descent ? Instance.Descend(scene, title, subtitle) : Instance.Fade(scene, title, subtitle));
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // the one from the first scene stays
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Hide();
    }

    void Hide()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        snapshot.enabled = false;
        rock.enabled = false;
        SetAlpha(flash, 0f);
        SetAlpha(black, 0f);
        SetCard(0f);
    }

    IEnumerator Fade(string scene, string cardTitle, string cardSubtitle)
    {
        Begin(cardTitle, cardSubtitle);
        Music.FadeOut(FadeTime);
        yield return Animate(FadeTime, t => SetAlpha(black, t));
        yield return LoadAndHold(scene, cardTitle != null);
        Music.FadeIn(FadeTime);
        yield return Animate(FadeTime, t =>
        {
            SetAlpha(black, 1f - t);
            SetCard(Mathf.Min(CardAlpha, 1f - t));
        });
        End();
    }

    IEnumerator Descend(string scene, string cardTitle, string cardSubtitle)
    {
        yield return new WaitForEndOfFrame(); // the screen as it is now
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        Begin(cardTitle, cardSubtitle);
        snapshot.texture = shot;
        snapshot.enabled = true;
        rock.enabled = true;
        Music.FadeOut(SinkTime);
        float height = ((RectTransform)transform).rect.height;
        yield return Animate(SinkTime, t => // the view sinks: the screen slides up, the rock comes up from below
        {
            float k = Smooth(t);
            snapshot.rectTransform.anchoredPosition = new Vector2(0f, height * k);
            rock.rectTransform.anchoredPosition = new Vector2(0f, -height * (1f - k));
        });
        snapshot.enabled = false;
        Destroy(shot);
        yield return LoadAndHold(scene, cardTitle != null);
        Music.FadeIn(RiseTime);
        yield return Animate(RiseTime, t => // the rock slides on up: the new scene is underneath
        {
            rock.rectTransform.anchoredPosition = new Vector2(0f, height * Smooth(t));
            SetCard(Mathf.Min(CardAlpha, 1f - t * 2f));
        });
        rock.enabled = false;
        yield return Animate(FlashTime, t => // a flash where the hero arrives
        {
            SetAlpha(flash, 0.8f * Mathf.Sin(t * Mathf.PI));
            flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.3f, t);
        });
        End();
    }

    // Loads the scene behind the overlay, freezes it, shows the card (any key or click moves on), then unfreezes it.
    IEnumerator LoadAndHold(string scene, bool card)
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync(scene);
        while (!loading.isDone) yield return null;
        yield return null; // its Start has run (and set the time scale)
        Time.timeScale = 0f;
        if (card)
        {
            yield return Animate(CardFadeTime, SetCard);
            float held = 0f;
            while (held < CardTime && !Skipped())
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        Time.timeScale = 1f;
    }

    void Begin(string cardTitle, string cardSubtitle)
    {
        busy = true;
        group.alpha = 1f;
        group.blocksRaycasts = true; // nothing underneath can be clicked meanwhile
        title.text = cardTitle ?? "";
        subtitle.text = cardSubtitle ?? "";
        snapshot.rectTransform.anchoredPosition = Vector2.zero;
        rock.rectTransform.anchoredPosition = Vector2.zero;
    }

    void End()
    {
        Hide();
        busy = false;
    }

    static IEnumerator Animate(float duration, System.Action<float> step)
    {
        for (float age = 0f; age < duration; age += Time.unscaledDeltaTime)
        {
            step(age / duration);
            yield return null;
        }
        step(1f);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static bool Skipped()
    {
        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
    }

    float CardAlpha => title.color.a;

    void SetCard(float alpha)
    {
        SetAlpha(title, alpha);
        SetAlpha(subtitle, alpha);
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
