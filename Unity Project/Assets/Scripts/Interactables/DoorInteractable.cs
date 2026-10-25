using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle)),
RequireComponent(typeof(AudioSource))]
public class DoorInteractable : InteractableObject, ISavableObject
{
    [SerializeField] private string primaryText = "Open";
    [SerializeField] private string secondaryText = "Close";
    [SerializeField] private bool flipped;
    [SerializeField] private string saveId;
    [SerializeField] private bool isOpen = false;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    private Quaternion initialRotation;
    private NavMeshObstacle obstacle;
    private AudioSource audioSource;

    public string SaveId => saveId;

    private void Start()
    {
        GameSettings settings = SettingsManager.Instance.CurrentSettings;
        obstacle = GetComponent<NavMeshObstacle>();
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = settings.masterVolume * settings.sfxVolume;
        
        initialRotation = transform.rotation;
        if (GameSessionManager.Instance != null)
        {
            string sceneId = gameObject.scene.path;

            if (GameSessionManager.Instance.SaveState.TryGetObjectState(sceneId, SaveId, out string stateJson))
                RestoreState(stateJson);
            else
                GameSessionManager.Instance.SaveState.SetObjectState(sceneId, SaveId, CaptureState());
        }

        ApplyState();
    }

    void Reset()
    {
        if (string.IsNullOrWhiteSpace(saveId)) saveId = System.Guid.NewGuid().ToString();
        obstacle = GetComponent<NavMeshObstacle>();
        obstacle.carving = true;
    }

    public override void Interact(PlayerManager player)
    {
        base.Interact(player);

        if (!isOpen) audioSource.PlayOneShot(openSound);
        else audioSource.PlayOneShot(closeSound);

        isOpen = !isOpen;
        ApplyState();

        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.SaveState.SetObjectState(gameObject.scene.path, SaveId, CaptureState());
    }

    private void ApplyState()
    {
        transform.rotation = isOpen ? initialRotation * Quaternion.Euler(0f, flipped ? 90f : -90f, 0f) : initialRotation;
        interactionText = isOpen ? secondaryText : primaryText;
        obstacle.enabled = !isOpen;
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new ToggleableState { isOpen = isOpen });
    }

    public void RestoreState(string stateJson)
    {
        if (!string.IsNullOrWhiteSpace(stateJson))
            isOpen = JsonUtility.FromJson<ToggleableState>(stateJson).isOpen;

        ApplyState();
    }

    [System.Serializable]
    private class ToggleableState
    {
        public bool isOpen;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawLine(transform.position, transform.position + (flipped ? -transform.right : transform.right));
    }
}