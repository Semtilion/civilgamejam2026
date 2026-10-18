using UnityEngine;

// The background music: each scene's song (set by the scene builders: the title Crystal Cave, the town A New Town, the
// depths Hyper Ultra Racing; all by cynicmusic, CC0), looping at the settings' music volume. One per game like
// SceneTransition: the first scene's stays, a later scene's hands over its song (the same song keeps playing).
// Scene changes fade it out with the screen and the next song in as the next scene appears (SceneTransition calls
// FadeOut / FadeIn). At half volume while the dungeon is paused: level-up cards, the pause menu, the panels (Dimmed,
// set by the ArenaManager every frame). Unscaled time.
[RequireComponent(typeof(AudioSource))]
public class Music : MonoBehaviour
{
    const float DimVolume = 0.5f, DimTime = 0.3f;

    public static Music Instance { get; private set; }
    public static bool Dimmed; // (a new scene's song resets it)

    public AudioClip song; // this scene's

    AudioSource source;
    float fade = 1f, fadeTarget = 1f, fadeSpeed = 1f; // scene changes: 0 = silent
    float dim = 1f;                                   // 1, or DimVolume while Dimmed

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.Play(song);
            Destroy(gameObject); // the one from the first scene stays
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        Play(song);
    }

    // Switches to `clip` (the same one keeps playing). Faded out (a scene change), the new one starts as it fades in.
    void Play(AudioClip clip)
    {
        Dimmed = false;
        if (source.clip == clip) return;
        source.Stop();
        source.clip = clip;
        if (fadeTarget > 0f && clip != null) source.Play();
    }

    // A scene change starts: the song fades out over `duration` seconds.
    public static void FadeOut(float duration)
    {
        if (Instance != null) Instance.FadeTo(0f, duration);
    }

    // The next scene appears: its song (from the start, if it's a different one) fades in over `duration` seconds.
    public static void FadeIn(float duration)
    {
        if (Instance == null) return;
        AudioSource source = Instance.source;
        if (!source.isPlaying && source.clip != null) source.Play();
        Instance.FadeTo(1f, duration);
    }

    void FadeTo(float target, float duration)
    {
        fadeTarget = target;
        fadeSpeed = 1f / duration;
    }

    void Update()
    {
        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
        dim = Mathf.MoveTowards(dim, Dimmed ? DimVolume : 1f, (1f - DimVolume) / DimTime * Time.unscaledDeltaTime);
        source.volume = GameSettings.MusicVolume * fade * dim;
    }
}
