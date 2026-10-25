using UnityEngine;

public class FlagEventTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameSessionManager.Instance.SaveState.SetFlag("RAH");
        }
    }
}
