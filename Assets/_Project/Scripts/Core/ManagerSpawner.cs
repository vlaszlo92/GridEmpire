using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ManagerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject gameControllerPrefab;
    [SerializeField] private GameObject readySystemPrefab;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening);

        if (NetworkManager.Singleton.IsServer)
        {
            GameObject gc = Instantiate(gameControllerPrefab);
            gc.GetComponent<NetworkObject>().Spawn();

            GameObject rs = Instantiate(readySystemPrefab);
            rs.GetComponent<NetworkObject>().Spawn();
        }

        Destroy(gameObject);
    }
}