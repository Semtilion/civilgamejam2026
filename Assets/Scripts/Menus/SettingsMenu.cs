using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The settings panel (title screen and the dungeon's pause menu): resolution, fullscreen / windowed, music and effects
// volume. Changes apply right away; the volumes are saved when the panel closes. In a WebGL build the resolution row is
// hidden (the browser decides the size).
public class SettingsMenu : MonoBehaviour
{
    [Header("Scene references")]
    public GameObject panel;
    public GameObject resolutionRow;
    public Text resolutionText;
    public Button previousResolution;
    public Button nextResolution;
    public Button displayButton;
    public Slider musicSlider;
    public Slider effectsSlider;
    public Button closeButton;

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    int resolutionIndex;
    bool fullscreen;

    public bool IsOpen => panel.activeSelf;

    void Start()
    {
        previousResolution.onClick.AddListener(() => StepResolution(-1));
        nextResolution.onClick.AddListener(() => StepResolution(1));
        displayButton.onClick.AddListener(ToggleFullscreen);
        musicSlider.onValueChanged.AddListener(value => GameSettings.MusicVolume = value);
        effectsSlider.onValueChanged.AddListener(value => GameSettings.EffectsVolume = value);
        closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        LoadResolutions();
        fullscreen = Screen.fullScreen;
        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        effectsSlider.SetValueWithoutNotify(GameSettings.EffectsVolume);
        resolutionRow.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
        Refresh();
        panel.SetActive(true);
    }

    public void Close()
    {
        GameSettings.Save();
        panel.SetActive(false);
    }

    // Every distinct size the monitor supports, smallest first, plus the current one.
    void LoadResolutions()
    {
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }
        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        resolutionIndex = resolutions.IndexOf(current);
    }

    void StepResolution(int step)
    {
        resolutionIndex = Mathf.Clamp(resolutionIndex + step, 0, resolutions.Count - 1);
        Vector2Int size = resolutions[resolutionIndex];
        Screen.SetResolution(size.x, size.y, fullscreen); // applies next frame (not in the editor's Game view)
        Refresh();
    }

    void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        Screen.fullScreen = fullscreen;
        Refresh();
    }

    void Refresh()
    {
        Vector2Int size = resolutions[resolutionIndex];
        resolutionText.text = $"{size.x} x {size.y}";
        previousResolution.interactable = resolutionIndex > 0;
        nextResolution.interactable = resolutionIndex < resolutions.Count - 1;
        displayButton.GetComponentInChildren<Text>().text = fullscreen ? "Fullscreen" : "Windowed";
    }
}
