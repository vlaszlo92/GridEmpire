using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneCleanup : MonoBehaviour
{
    private void Start() { if (!NetworkManager.Singleton.IsHost) { SceneManager.UnloadSceneAsync("MainMenuScene"); } }
}