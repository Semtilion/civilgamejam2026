using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The dungeon's Esc menu: Resume / How to play / Settings / Quit to title. Pauses the game while open (ArenaManager
// .IsPlaying is false). Esc closes whatever is on top. Quitting counts as dying (ArenaManager.QuitToTitle).
public class PauseMenu : MonoBehaviour
{
    [Header("Scene references")]
    public ArenaManager arena;
    public GameObject panel;
    public Button resumeButton;
    public Button howToPlayButton;
    public Button settingsButton;
    public Button quitButton;
    public HowToPlay howToPlay;
    public SettingsMenu settings;

    public bool IsOpen => panel.activeSelf;

    void Start()
    {
        resumeButton.onClick.AddListener(Resume);
        howToPlayButton.onClick.AddListener(howToPlay.Open);
        settingsButton.onClick.AddListener(settings.Open);
        quitButton.onClick.AddListener(arena.QuitToTitle);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (howToPlay.IsOpen) howToPlay.Close();
        else if (settings.IsOpen) settings.Close();
        else if (IsOpen) Resume();
        else if (arena.IsPlaying) Open(); // not over a level-up or another panel
    }

    void Open()
    {
        panel.SetActive(true);
        Time.timeScale = 0f;
        Sfx.Play(Sound.Click);
    }

    void Resume()
    {
        howToPlay.Close();
        settings.Close();
        panel.SetActive(false);
        Time.timeScale = 1f;
        Sfx.Play(Sound.Click); // (Esc; the Resume button clicks anyway)
    }
}
