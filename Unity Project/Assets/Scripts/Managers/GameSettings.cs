using UnityEngine;

[System.Serializable]
public class GameSettings
{
    [Header("AUDIO")]
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;

    [Header("GRAPHICS")]
    public int qualityLevel = 2;
    public bool fullscreen = true;

    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public uint refreshRate = 60;

    [Header("CONTROLS")]
    public float mouseSensitivityX = 1f;
    public float mouseSensitivityY = 1f;
    public bool invertX = false;
    public bool invertY = false;
}
