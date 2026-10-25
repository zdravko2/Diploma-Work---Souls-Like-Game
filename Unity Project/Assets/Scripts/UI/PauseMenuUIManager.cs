using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private GameObject loadGamePanel;

    [Header("Main Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button quitButton;

    [Header("Load Game UI")]
    [SerializeField] private Button closeLoadGamePanelButton;
    [SerializeField] private Transform saveListContent;
    [SerializeField] private SaveFileEntryUIManager saveFileEntryUIPrefab;
    [SerializeField] private ScrollRect saveListScrollRect;

    private GameSessionManager sessionManager;
    private bool isOpen;

    private void Awake()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);

        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void Initialize(GameSessionManager sessionManager)
    {
        this.sessionManager = sessionManager;
        CloseImmediate();
    }

    public void TogglePause()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (isOpen)
            return;

        isOpen = true;

        if (rootPanel != null)
            rootPanel.SetActive(true);

        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);

        if (sessionManager != null)
            sessionManager.SetPaused(true);

        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.playerControls.Disable();
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (rootPanel != null)
            rootPanel.SetActive(false);

        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);

        if (sessionManager != null)
            sessionManager.SetPaused(false);

        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.playerControls.Enable();
    }

    public void CloseImmediate()
    {
        isOpen = false;

        if (rootPanel != null)
            rootPanel.SetActive(false);

        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.playerControls.Enable();
    }

    public void OnContinuePressed()
    {
        Close();
    }

    public void OnSaveGamePressed()
    {
        if (GameSessionManager.Instance.Player.statusManager.isDead) return;

        GameManager.Instance.CreateManualSave();
        Close();
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

    public void OnQuitPressed()
    {
        CloseImmediate();
        GameManager.Instance.ReturnToTitle();
    }

    public void LoadSelectedSave(string fileName)
    {
        CloseImmediate();
        GameManager.Instance.LoadGame(fileName);
    }

    public void DeleteSelectedSave(string fileName)
    {
        GameManager.Instance.DeleteSaveFile(fileName);
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
            entry.BindForPauseMenu(saves[i], this);
        }

        StartCoroutine(ScrollToTopNextFrame());
    }
    
    private IEnumerator ScrollToTopNextFrame()
    {
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (saveListScrollRect != null)
            saveListScrollRect.verticalNormalizedPosition = 1f;
    }
}