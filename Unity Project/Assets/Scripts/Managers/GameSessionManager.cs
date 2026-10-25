using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Header("Runtime Root")]
    [SerializeField] private Transform sessionRuntimeRoot;

    [Header("Session Prefabs")]
    [SerializeField] private PlayerInputManager playerInputManagerPrefab;
    [SerializeField] private PlayerCamera playerCameraPrefab;
    [SerializeField] private PlayerUIManager playerUIPrefab;
    [SerializeField] private PlayerManager playerPrefab;
    [SerializeField] private SceneStreamingManager sceneStreamingManagerPrefab;
    [SerializeField] private PauseMenuUIManager pauseMenuUIPrefab;
    public SaveStateService SaveState { get; } = new SaveStateService();

    private PlayerInputManager playerInputManager;
    private PlayerCamera playerCamera;
    private PlayerManager playerManager;
    private PlayerUIManager playerUIManager;
    private SceneStreamingManager sceneStreamingManager;
    private PauseMenuUIManager pauseMenuUIManager;

    private bool isPaused;
    private bool isInitializing;
    private string currentSpawnPointId;

    public PlayerManager Player => playerManager;
    public string CurrentSpawnPointId => currentSpawnPointId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (playerInputManager != null && playerInputManager.ToggleMenuPressed)
        {
            if (pauseMenuUIManager != null)
                pauseMenuUIManager.TogglePause();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;

        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;

        if (playerCamera != null)
        {
            playerCamera.controlsEnabled = !paused;
            playerCamera.followPlayer = !paused;
            playerCamera.avoidCollision = !paused;
            playerCamera.SetCursorState(paused ? CursorLockMode.None : CursorLockMode.Locked);
        }

        Cursor.visible = paused;

        if (playerManager != null)
            playerManager.enabled = !paused;
    }

    public IEnumerator InitializeSession(bool isNewGame, CharacterSaveData saveData, SceneDefinition startLocation)
    {
        if (isInitializing) yield break;

        isInitializing = true;

        SpawnSessionSystemsIfNeeded();
        SaveState.LoadFromSave(saveData);

        DisableCamera();

        if (playerManager != null)
        {
            playerManager.enabled = false;
        }

        SceneDefinition locationToLoad = ResolveLocationToLoad(isNewGame, saveData, startLocation);

        if (locationToLoad != null && sceneStreamingManager != null)
        {
            yield return sceneStreamingManager.SwitchToLocation(locationToLoad);
        }

        yield return null;

        // string spawnPointId = ResolveSpawnPointId(isNewGame, saveData, locationToLoad);

        // if (!TryPlacePlayerAtSpawn(spawnPointId) && saveData != null)
        // {
        //     ApplyPlayerTransform(saveData);
        // }

        ApplyPlayerTransform(saveData);

        if (saveData != null && playerManager != null)
        {
            playerManager.LoadCharacterData(ref saveData);
        }

        SetupCamera();

        if (playerManager != null)
            playerManager.enabled = true;

        isInitializing = false;
    }

    private void SpawnSessionSystemsIfNeeded()
    {
        if (playerInputManager == null && playerInputManagerPrefab != null)
        {
            playerInputManager = Instantiate(playerInputManagerPrefab, sessionRuntimeRoot);
        }

        if (playerCamera == null && playerCameraPrefab != null)
        {
            playerCamera = Instantiate(playerCameraPrefab, sessionRuntimeRoot);
        }

        if (playerUIManager == null && playerUIPrefab != null)
        {
            playerUIManager = Instantiate(playerUIPrefab, sessionRuntimeRoot);
        }
        
        if (playerManager == null && playerPrefab != null)
        {
            playerManager = Instantiate(playerPrefab, sessionRuntimeRoot);
        }

        if (sceneStreamingManager == null && sceneStreamingManagerPrefab != null)
        {
            sceneStreamingManager = Instantiate(sceneStreamingManagerPrefab, sessionRuntimeRoot);
        }
        
        if (pauseMenuUIManager == null && pauseMenuUIPrefab != null)
        {
            pauseMenuUIManager = Instantiate(pauseMenuUIPrefab, sessionRuntimeRoot);
            pauseMenuUIManager.Initialize(this);
        }
    }

    private SceneDefinition ResolveLocationToLoad(bool isNewGame, CharacterSaveData saveData, SceneDefinition fallbackLocation)
    {
        if (isNewGame) 
            return fallbackLocation;
        if (saveData == null || string.IsNullOrWhiteSpace(saveData.currentSceneId)) 
            return fallbackLocation;

        SceneDefinition savedLocation = GameManager.Instance.GetLocationById(saveData.currentSceneId);
        return savedLocation != null ? savedLocation : fallbackLocation;
    }

    private string ResolveSpawnPointId(bool isNewGame, CharacterSaveData saveData, SceneDefinition locationToLoad)
    {
        if (!isNewGame && saveData != null && !string.IsNullOrWhiteSpace(saveData.currentSpawnPointId))
            return saveData.currentSpawnPointId;

        if (locationToLoad != null && !string.IsNullOrWhiteSpace(locationToLoad.defaultSpawnPointId))
            return locationToLoad.defaultSpawnPointId;

        return "Start";
    }

    private bool TryPlacePlayerAtSpawn(string spawnPointId)
    {
        if (playerManager == null || string.IsNullOrWhiteSpace(spawnPointId))
            return false;

        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].SpawnPointId != spawnPointId)
                continue;

            PlacePlayer(spawnPoints[i].transform.position, spawnPoints[i].transform.rotation);
            currentSpawnPointId = spawnPointId;
            return true;
        }

        return false;
    }

    public void SetCurrentSpawnPoint(string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(spawnPointId)) return;

        currentSpawnPointId = spawnPointId;
    }

    private void ApplyPlayerTransform(CharacterSaveData saveData)
    {
        if (playerManager == null || saveData == null)
            return;

        Vector3 position = new Vector3(
            saveData.xPosition,
            saveData.yPosition,
            saveData.zPosition
        );

        Quaternion rotation = Quaternion.Euler(
            saveData.xRotation,
            saveData.yRotation,
            saveData.zRotation
        );

        PlacePlayer(position, rotation);
    }

    private void PlacePlayer(Vector3 position, Quaternion rotation)
    {
        CharacterController controller = playerManager.characterController;

        if (controller != null)
            controller.enabled = false;

        playerManager.transform.SetPositionAndRotation(position + Vector3.up * 0.05f, rotation);

        ResetPlayerMotionState();

        if (controller != null)
            controller.enabled = true;
    }

    private void ResetPlayerMotionState()
    {
        if (playerManager == null)
            return;

        // reset movement / gravity / jump variables
    }

    private void SetupCamera()
    {
        if (playerCamera == null || playerManager == null) return;

        playerCamera.player = playerManager;
        playerCamera.SetCursorState(CursorLockMode.Locked);
        playerCamera.SetPosition(playerManager.transform.position, playerManager.transform.rotation);
        playerCamera.controlsEnabled = true;
        playerCamera.followPlayer = true;
        playerCamera.avoidCollision = true;
    }

    private void DisableCamera()
    {
        if (playerCamera == null) return;

        playerCamera.player = null;
        playerCamera.SetCursorState(CursorLockMode.None);
        playerCamera.SetPosition(Vector3.zero, Quaternion.identity);
        playerCamera.controlsEnabled = false;
        playerCamera.followPlayer = false;
        playerCamera.avoidCollision = false;
    }

    public void CaptureCurrentGameState(CharacterSaveData saveData)
    {
        if (playerManager == null || saveData == null)
            return;

        Vector3 position = playerManager.transform.position;
        Vector3 rotation = playerManager.transform.eulerAngles;

        saveData.xPosition = position.x;
        saveData.yPosition = position.y;
        saveData.zPosition = position.z;

        saveData.xRotation = rotation.x;
        saveData.yRotation = rotation.y;
        saveData.zRotation = rotation.z;

        if (playerManager.statusManager is PlayerStatusManager playerStatus)
        {
            saveData.health = playerStatus.currentHealth;
            saveData.stamina = playerStatus.currentStamina;
            saveData.mana = playerStatus.currentMana;

            saveData.vitality = playerStatus.vitality;
            saveData.endurance = playerStatus.endurance;
            saveData.mind = playerStatus.mind;
            saveData.strength = playerStatus.strength;
            saveData.dexterity = playerStatus.dexterity;
        }

        if (sceneStreamingManager != null && sceneStreamingManager.CurrentLocation != null)
            saveData.currentSceneId = sceneStreamingManager.CurrentLocation.sceneId;

        saveData.currentSpawnPointId = CurrentSpawnPointId;

        SaveState.WriteToSave(saveData);
    }
}