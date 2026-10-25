using UnityEngine;
using UnityEngine.Audio;
using System.IO;
using System;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    public GameSettings CurrentSettings;

    private string savePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        savePath = Path.Combine(Application.persistentDataPath, "settings.json");

        LoadSettings();
        ApplySettings();
    }

    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(CurrentSettings, true);
        File.WriteAllText(savePath, json);

        Debug.Log("Settings saved to: " + savePath);
    }

    public void LoadSettings()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            CurrentSettings = JsonUtility.FromJson<GameSettings>(json);
        }
        else
        {
            CurrentSettings = new GameSettings();
            SaveSettings();
        }
    }

    public void ApplySettings()
    {
        ApplyAudio();
        ApplyGraphics();
        ApplyControls();
    }
    
    public void RestoreDefaults()
    {
        CurrentSettings = new GameSettings();
        ApplySettings();
        SaveSettings();
    }

    private void ApplyAudio()
    {
        // TODO: Implement volume mixer
    }

    private void ApplyGraphics()
    {
        QualitySettings.SetQualityLevel(CurrentSettings.qualityLevel);

        Screen.SetResolution(
            CurrentSettings.resolutionWidth,
            CurrentSettings.resolutionHeight,
            CurrentSettings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
            new RefreshRate { numerator = CurrentSettings.refreshRate, denominator = 1 }
        );
    }
    
    private void ApplyControls()
    {
        // TODO: Implement controls
    }

    public void SetMasterVolume(float value)
    {
        CurrentSettings.masterVolume = value;
        ApplyAudio();
    }

    public void SetMusicVolume(float value)
    {
        CurrentSettings.musicVolume = value;
        ApplyAudio();
    }

    public void SetSFXVolume(float value)
    {
        CurrentSettings.sfxVolume = value;
        ApplyAudio();
    }

    public void SetQuality(int qualityIndex)
    {
        CurrentSettings.qualityLevel = qualityIndex;
        ApplyGraphics();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        CurrentSettings.fullscreen = isFullscreen;
        ApplyGraphics();
    }

    public void SetResolution(int width, int height, uint refreshRate)
    {
        CurrentSettings.resolutionWidth = width;
        CurrentSettings.resolutionHeight = height;
        CurrentSettings.refreshRate = refreshRate;

        ApplyGraphics();
    }

    internal void SetMouseSensitivity(float value)
    {
        CurrentSettings.mouseSensitivityX = value;
        CurrentSettings.mouseSensitivityY = value;
        ApplyControls();
    }
}