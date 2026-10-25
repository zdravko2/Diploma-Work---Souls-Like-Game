using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUIManager : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button quitButton;

    [Header("Load Game UI")]
    [SerializeField] private GameObject loadGamePanel;
    [SerializeField] private Button closeLoadGamePanelButton;
    [SerializeField] private Transform saveListContent;
    [SerializeField] private SaveFileEntryUIManager saveFileEntryUIPrefab;
    [SerializeField] private ScrollRect saveListScrollRect;
    
    [Header("Settings")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button closeSettingsPanelButton;
    [SerializeField] private Button restoreSettingsPanelButton;
    [SerializeField] private TextMeshProUGUI masterVolumeLabel;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI sensitivityLabel;
    [SerializeField] private Slider sensitivitySlider;
    private bool isInitializing = false;

    private void Start()
    {
        RefreshMenuState();
        loadGamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        InitializeSettingsUI();
    }

    private void OnEnable()
    {
        RefreshMenuState();
    }
    
    private void InitializeSettingsUI()
    {
        //isInitializing = true;

        GameSettings settings = SettingsManager.Instance.CurrentSettings;

        masterVolumeSlider.value = settings.masterVolume;
        //musicVolumeSlider.value = settings.musicVolume;
        //sfxVolumeSlider.value = settings.sfxVolume;
        sensitivitySlider.value = settings.mouseSensitivityX;

        //isInitializing = false;
    }

    public void OnNewGamePressed()
    {
        GameManager.Instance.CreateNewGame();
    }

    public void OnContinuePressed()
    {
        GameManager.Instance.ContinueGame();
    }

    public void OnLoadGamePressed()
    {
        if (loadGamePanel != null)
            loadGamePanel.SetActive(true);

        RebuildSaveList();
    }

    public void OnCloseLoadGamePressed()
    {
        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);
    }

    public void OnSettingsPressed()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void OnCloseSettingsPressed()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        SettingsManager.Instance.SaveSettings();
    }

    public void OnRestoreSettingsPressed()
    {
        if (settingsPanel != null)
        {
            SettingsManager.Instance.RestoreDefaults();
            InitializeSettingsUI();
        }
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }

    public void LoadSelectedSave(string fileName)
    {
        GameManager.Instance.LoadGame(fileName);
    }

    public void DeleteSelectedSave(string fileName)
    {
        GameManager.Instance.DeleteSaveFile(fileName);
        RefreshMenuState();
        RebuildSaveList();
    }

    public void RefreshMenuState()
    {
        bool hasSaves = GameManager.Instance != null && GameManager.Instance.HasAnySaveFiles();

        if (continueButton != null)
            continueButton.interactable = hasSaves;

        if (loadGameButton != null)
            loadGameButton.interactable = hasSaves;

        if (loadGamePanel != null && loadGamePanel.activeSelf)
            RebuildSaveList();
    }

    private void RebuildSaveList()
    {
        if (saveListContent == null || saveFileEntryUIPrefab == null)
            return;

        for (int i = saveListContent.childCount - 1; i >= 0; i--)
            Destroy(saveListContent.GetChild(i).gameObject);

        List<SaveFileInfo> saves = GameManager.Instance.GetAllSaveFiles();

        for (int i = 0; i < saves.Count; i++)
        {
            SaveFileEntryUIManager entry = Instantiate(saveFileEntryUIPrefab, saveListContent);
            entry.Bind(saves[i], this);
        }

        StartCoroutine(ScrollToTopNextFrame());
    }

    private IEnumerator ScrollToTopNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (saveListScrollRect != null)
            saveListScrollRect.verticalNormalizedPosition = 1f;
    }
    
    public void OnMasterVolumeChanged()
    {
        if (isInitializing) return;
        float value = masterVolumeSlider.value;

        masterVolumeLabel.text = $"Volume: {value:F1}";
        SettingsManager.Instance.SetMasterVolume(value);
    }

    public void OnMusicVolumeChanged()
    {
        if (isInitializing) return;
        float value = musicVolumeSlider.value;

        SettingsManager.Instance.SetMusicVolume(value);
    }

    public void OnSFXVolumeChanged()
    {
        if (isInitializing) return;
        float value = sfxVolumeSlider.value;
        
        SettingsManager.Instance.SetSFXVolume(value);
    }

    public void OnSensitivityChanged()
    {
        if (isInitializing) return;
        float value = sensitivitySlider.value;
        
        sensitivityLabel.text = $"Sensitivity: {value:F1}";
        SettingsManager.Instance.SetMouseSensitivity(value);
    }
}