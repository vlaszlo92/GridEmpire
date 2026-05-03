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
        // HOST: Ő a szerver és egy kliens is
        hostBtn.onClick.AddListener(() => {
            if (NetworkManager.Singleton.StartHost())
            {
                // A NetworkSceneManager váltja át mindenkinél a scenét!
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        });

        // CLIENT: Ő csak csatlakozik
        clientBtn.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
        });
    }
}