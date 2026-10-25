using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId = "Start";

    public string SpawnPointId => spawnPointId;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (playerManager != null)
            {
                //GameSessionManager.Instance.SetCurrentSpawnPoint(spawnPointId);
            }
        }
    }
}