using System.Collections;
using System.Collections.Generic;
using System.IO;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private SceneReference titleScene;
    [SerializeField] private SceneReference gameplayRootScene;
    [SerializeField] private SceneDefinition firstLevel;

    [Header("App Prefabs")]
    [SerializeField] private GameObject loadingScreenPrefab;
    [SerializeField] private GameObject eventSystemPrefab;
    [SerializeField] private GameDatabase gameDatabasePrefab;
    [SerializeField] private SettingsManager settingsManagerPrefab;

    [Header("World Layers")]
    public LayerMask characterLayer;
    public LayerMask environmentLayer;
    public LayerMask hitBoxLayer;
    public LayerMask hurtBoxLayer;
    public LayerMask interactableLayer;
    public LayerMask sceneTriggerLayer;

    public CharacterSaveData CurrentSaveData { get; private set; }
    public string CurrentSaveFileName { get; private set; }

    private GameObject loadingScreenInstance;
    private EventSystem eventSystemInstance;
    private GameDatabase gameDatabaseInstance;
    private SaveDataFileManager saveFileManager;
    private SettingsManager settingsManagerInstance;
    private bool isBusy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        CreatePersistentObjects();
        saveFileManager = new SaveDataFileManager(Path.Combine(Application.persistentDataPath, "Saves"));
        StartCoroutine(LoadSceneRoutine(titleScene));
    }

    public SceneDefinition GetLocationById(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            return null;

        if (gameDatabaseInstance != null && gameDatabaseInstance.TryGetLocation(sceneId, out SceneDefinition location))
            return location;

        if (firstLevel != null && firstLevel.sceneId == sceneId)
            return firstLevel;

        return null;
    }

    public bool HasAnySaveFiles()
    {
        return saveFileManager != null && saveFileManager.HasAnySaveFiles();
    }

    public List<SaveFileInfo> GetAllSaveFiles()
    {
        return saveFileManager != null ? saveFileManager.GetAllSaveFiles() : new List<SaveFileInfo>();
    }

    public void CreateNewGame()
    {
        if (isBusy || saveFileManager == null)
            return;

        CurrentSaveData = CreateNewSaveData("MyHero");
        CurrentSaveFileName = saveFileManager.CreateSaveFile(CurrentSaveData, false, GetLocationDisplayName(firstLevel));
        saveFileManager.SetLastPlayedSaveFile(CurrentSaveFileName);

        StartCoroutine(StartGameplaySessionRoutine(true, CurrentSaveData, firstLevel));
    }

    public void ContinueGame()
    {
        if (isBusy || saveFileManager == null)
            return;

        string fileName = saveFileManager.GetLastPlayedSaveFile();

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = saveFileManager.GetMostRecentSaveFile();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogWarning("No save files available to continue.");
            return;
        }

        LoadGame(fileName);
    }

    public void LoadGame()
    {
        ContinueGame();
    }

    public void LoadGame(string fileName)
    {
        if (isBusy || saveFileManager == null || string.IsNullOrWhiteSpace(fileName))
            return;

        CharacterSaveData loadedData = saveFileManager.LoadSaveFile(fileName);

        if (loadedData == null)
        {
            Debug.LogWarning($"Failed to load save file '{fileName}'.");
            return;
        }

        CurrentSaveData = loadedData;
        CurrentSaveFileName = fileName;
        saveFileManager.SetLastPlayedSaveFile(fileName);

        SceneDefinition location = GetLocationById(CurrentSaveData.currentSceneId);
        if (location == null)
            location = firstLevel;

        StartCoroutine(StartGameplaySessionRoutine(false, CurrentSaveData, location));
    }

    public void SaveGame()
    {
        CreateSave(false);
    }

    public void CreateManualSave()
    {
        CreateSave(false);
    }

    public void CreateAutoSave()
    {
        CreateSave(true);
    }

    public void DeleteSaveFile(string fileName)
    {
        if (saveFileManager == null || string.IsNullOrWhiteSpace(fileName))
            return;

        saveFileManager.DeleteSaveFile(fileName);

        if (CurrentSaveFileName == fileName)
        {
            CurrentSaveFileName = null;
            CurrentSaveData = null;
        }
    }

    public void ReturnToTitle()
    {
        if (isBusy)
            return;

        StartCoroutine(ReturnToTitleRoutine());
    }

    private void CreateSave(bool isAutoSave)
    {
        if (isBusy || CurrentSaveData == null || saveFileManager == null)
            return;

        GameSessionManager sessionManager = FindFirstObjectByType<GameSessionManager>();

        if (sessionManager == null)
        {
            Debug.LogWarning("GameSessionManager not found.");
            return;
        }

        sessionManager.CaptureCurrentGameState(CurrentSaveData);

        string locationName = GetLocationDisplayName(CurrentSaveData.currentSceneId);
        CurrentSaveFileName = saveFileManager.CreateSaveFile(CurrentSaveData, isAutoSave, locationName);
        saveFileManager.SetLastPlayedSaveFile(CurrentSaveFileName);
    }

    private IEnumerator StartGameplaySessionRoutine(bool isNewGame, CharacterSaveData saveData, SceneDefinition startLocation)
    {
        if (isBusy)
            yield break;

        isBusy = true;
        ShowLoadingScreen();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(gameplayRootScene.Path, LoadSceneMode.Single);

        while (!loadOperation.isDone)
            yield return null;

        yield return null;

        GameSessionManager sessionManager = FindFirstObjectByType<GameSessionManager>();

        if (sessionManager == null)
        {
            Debug.LogError("GameplayRoot loaded, but no GameSessionManager was found.");
            HideLoadingScreen();
            isBusy = false;
            yield break;
        }

        yield return sessionManager.InitializeSession(isNewGame, saveData, startLocation);

        HideLoadingScreen();
        isBusy = false;
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        if (isBusy)
            yield break;

        isBusy = true;
        ShowLoadingScreen();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(titleScene.Path, LoadSceneMode.Single);

        while (!loadOperation.isDone)
            yield return null;

        HideLoadingScreen();
        isBusy = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator LoadSceneRoutine(SceneReference sceneReference)
    {
        if (isBusy)
            yield break;

        isBusy = true;
        ShowLoadingScreen();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneReference.Path, LoadSceneMode.Single);

        while (!loadOperation.isDone)
            yield return null;

        HideLoadingScreen();
        isBusy = false;
    }

    private CharacterSaveData CreateNewSaveData(string characterName)
    {
        return new CharacterSaveData
        {
            characterName = characterName,
            currentSceneId = firstLevel != null ? firstLevel.sceneId : string.Empty,
            currentSpawnPointId = firstLevel != null ? firstLevel.defaultSpawnPointId : "Start",

            vitality = 10,
            endurance = 10,
            mind = 10,
            strength = 10,
            dexterity = 10,

            health = 10f,
            stamina = 10f,
            mana = 10f
        };
    }

    private string GetLocationDisplayName(SceneDefinition location)
    {
        if (location == null)
            return "Unknown";

        return string.IsNullOrWhiteSpace(location.name) ? location.sceneId : location.name;
    }

    private string GetLocationDisplayName(string sceneId)
    {
        SceneDefinition location = GetLocationById(sceneId);
        return GetLocationDisplayName(location);
    }

    private void CreatePersistentObjects()
    {
        if (loadingScreenInstance == null && loadingScreenPrefab != null)
        {
            loadingScreenInstance = Instantiate(loadingScreenPrefab);
            loadingScreenInstance.SetActive(false);
            DontDestroyOnLoad(loadingScreenInstance);
        }

        if (eventSystemInstance == null && eventSystemPrefab != null)
        {
            GameObject eventSystemObject = Instantiate(eventSystemPrefab);
            eventSystemInstance = eventSystemObject.GetComponent<EventSystem>();
            DontDestroyOnLoad(eventSystemObject);
        }

        if (gameDatabaseInstance == null && gameDatabasePrefab != null)
        {
            gameDatabaseInstance = Instantiate(gameDatabasePrefab);
            DontDestroyOnLoad(gameDatabaseInstance.gameObject);
        }
        
        if (settingsManagerInstance == null && settingsManagerPrefab != null)
        {
            settingsManagerInstance = Instantiate(settingsManagerPrefab);
            DontDestroyOnLoad(settingsManagerInstance.gameObject);
        }
    }

    private void ShowLoadingScreen()
    {
        if (loadingScreenInstance != null)
            loadingScreenInstance.SetActive(true);
    }

    private void HideLoadingScreen()
    {
        if (loadingScreenInstance != null)
            loadingScreenInstance.SetActive(false);
    }
}