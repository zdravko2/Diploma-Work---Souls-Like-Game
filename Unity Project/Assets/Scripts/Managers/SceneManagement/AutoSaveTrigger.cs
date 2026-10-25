using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AutoSaveTrigger : MonoBehaviour, ISavableObject
{
    [SerializeField] private string playerTag = "Player";
    private Collider triggerCollider;
    private bool triggered = false;
    public string saveId = "";

    public string SaveId => saveId;

    private void Awake()
    {
        EnsureTriggerSetup();
    }

    private void Reset()
    {
        EnsureTriggerSetup();
    }

    private void EnsureTriggerSetup()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
        gameObject.layer = LayerMask.NameToLayer("SceneTrigger");

        if (string.IsNullOrWhiteSpace(saveId))
            Debug.LogWarning($"Save ID is not set for {gameObject.name} in {gameObject.scene.path}.", this);

        if (GameSessionManager.Instance != null && !string.IsNullOrWhiteSpace(saveId))
        {
            string sceneId = gameObject.scene.path;

            if (GameSessionManager.Instance.SaveState.TryGetObjectState(sceneId, saveId, out string stateJson))
            {
                RestoreState(stateJson);
            }
            else
            {
                GameSessionManager.Instance.SaveState.SetObjectState(sceneId, saveId, CaptureState());
            }
        }
    }
    
    void ApplyState()
    {
        if (triggered) Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag) || triggered)
            return;

        triggered = true;
        GameManager.Instance.CreateAutoSave();
        ApplyState();
    }
    
    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveData
        {
            isTriggered = triggered
        });
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            ApplyState();
            return;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(stateJson);
        triggered = data.isTriggered;

        ApplyState();
    }

    [System.Serializable]
    private struct SaveData
    {
        public bool isTriggered;
    }
}
