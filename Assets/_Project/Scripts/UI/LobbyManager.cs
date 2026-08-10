using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;

    private void Start()
    {
        // HOST: o a szerver es egy kliens is
        hostBtn.onClick.AddListener(() => {
            if (NetworkManager.Singleton.StartHost())
            {
                // A NetworkSceneManager valtja at mindenkinel a scenet!
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        });

        // CLIENT: o csak csatlakozik
        clientBtn.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
        });
    }
}