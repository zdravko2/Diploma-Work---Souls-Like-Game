using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SceneLocationTrigger : MonoBehaviour
{
    [SerializeField] private SceneDefinition sceneDefinition;
    [SerializeField] private string playerTag = "Player";

    private Collider triggerCollider;

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
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (sceneDefinition == null || SceneStreamingManager.Instance == null)
            return;

        if (SceneStreamingManager.Instance.CurrentLocation == sceneDefinition)
            return;

        if (!triggerCollider.bounds.Contains(other.transform.position))
            return;

        StartCoroutine(SceneStreamingManager.Instance.SwitchToLocation(sceneDefinition));
    }
}