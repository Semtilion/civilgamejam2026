using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Every sound effect in the game (which files, how loud: MenuBuilder.SoundPlayer).
public enum Sound
{
    CrateBreak, GlassBreak, CrystalPickup, MaterialsPickup, GoldPickup, Swing, Whoosh, Hit, Tremor, RockLand, Quake, HammerQuake,
    HammerSmash, BombBreak, Flame, Zap,
    HeroHurt, HeroDeath, Revive, SpitterShot, Explosion, BossArrive, BossDash, BossDeath, Freeze, Ricochet,
    XpPickup, LevelUp, CardPick, RelicFound, Mining, ShrineDone, TeleporterOpen, TeleporterEnter,
    Click, Descent, Build, TurnIn, LeaderSlide, Report, Evolution, Results, GameOver, FoodPickup
}

// The sound effects: Sfx.Play(sound) plays one of its clips (at random) at the settings' effects volume, with a
// slightly different pitch each time. A sound plays at most MaxCopies at once and not twice within MinGap seconds, so
// many at once (30 pickups, a sweep hitting 20 enemies) don't pile up. A cut sound stops after its `length`, fading
// out over its last `fade` seconds. Every button in every scene clicks (Sound.Click).
// One per game like Music: every scene has one (made by the scene builders), the first stays, so sounds play on
// through scene changes. Without one, Play does nothing.
public class Sfx : MonoBehaviour
{
    const int Voices = 16, MaxCopies = 3;
    const float MinGap = 0.05f, PitchSpread = 0.06f;

    [System.Serializable]
    public class Entry
    {
        public string name;       // (the Sound, for the inspector)
        public AudioClip[] clips; // one at random
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Above 0: stops after this many seconds (fading out over the last `fade`). 0 = plays whole.")]
        public float length;
        public float fade;
    }

    class Voice
    {
        public AudioSource source;
        public Sound sound;
        public float volume, stopAt, fade; // stopAt 0: plays to its end
    }

    public Entry[] sounds; // in Sound order

    static Sfx instance;
    Voice[] voices;
    float[] lastStart; // per Sound

    public static void Play(Sound sound)
    {
        if (instance != null) instance.Begin(sound);
    }

    // Plays `sound` once no scene transition covers the screen (a popup that opens as its scene loads).
    public static void PlayWhenShown(Sound sound)
    {
        if (instance != null) instance.StartCoroutine(instance.AfterTransition(sound));
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // the one from the first scene stays
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        voices = new Voice[Voices];
        for (int i = 0; i < Voices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            voices[i] = new Voice { source = source };
        }
        lastStart = new float[System.Enum.GetValues(typeof(Sound)).Length];
        System.Array.Fill(lastStart, -1f);
        HookButtons();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookButtons();

    // Every button in the loaded scenes (hidden ones too) clicks; hooking one twice is harmless.
    static void HookButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            button.onClick.RemoveListener(Click);
            button.onClick.AddListener(Click);
        }
    }

    static void Click() => Play(Sound.Click);

    IEnumerator AfterTransition(Sound sound)
    {
        while (SceneTransition.Busy) yield return null;
        Begin(sound);
    }

    void Begin(Sound sound)
    {
        int index = (int)sound;
        Entry entry = index < sounds.Length ? sounds[index] : null;
        float now = Time.unscaledTime;
        if (entry == null || entry.clips == null || entry.clips.Length == 0 || now - lastStart[index] < MinGap) return;

        Voice free = null;
        int copies = 0;
        foreach (Voice voice in voices)
        {
            if (!voice.source.isPlaying) free ??= voice;
            else if (voice.sound == sound) copies++;
        }
        if (free == null || copies >= MaxCopies) return;

        lastStart[index] = now;
        free.sound = sound;
        free.volume = entry.volume * GameSettings.EffectsVolume;
        free.stopAt = entry.length > 0f ? now + entry.length : 0f;
        free.fade = entry.fade;
        free.source.clip = entry.clips[Random.Range(0, entry.clips.Length)];
        free.source.pitch = 1f + Random.Range(-PitchSpread, PitchSpread);
        free.source.volume = free.volume;
        free.source.Play();
    }

    // Cut sounds: fade out, then stop.
    void Update()
    {
        float now = Time.unscaledTime;
        foreach (Voice voice in voices)
        {
            if (voice.stopAt <= 0f || !voice.source.isPlaying) continue;
            float left = voice.stopAt - now;
            if (left <= 0f)
            {
                voice.source.Stop();
                voice.stopAt = 0f;
            }
            else if (left < voice.fade) voice.source.volume = voice.volume * left / voice.fade;
        }
    }
}
