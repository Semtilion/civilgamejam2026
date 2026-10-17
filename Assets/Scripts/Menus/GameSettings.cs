using UnityEngine;

// Settings that last between sessions (PlayerPrefs): music and effects volume, 0-1. The screen resolution and
// fullscreen / windowed are remembered by Unity itself. Sounds should multiply their volume by these.
public static class GameSettings
{
    const string MusicKey = "Music Volume";
    const string EffectsKey = "Effects Volume";
    const float DefaultVolume = 0.8f;

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, DefaultVolume);
        set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
    }

    public static float EffectsVolume
    {
        get => PlayerPrefs.GetFloat(EffectsKey, DefaultVolume);
        set => PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value));
    }

    // Writes the volumes to disk (called when the settings panel closes, not on every slider move).
    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
