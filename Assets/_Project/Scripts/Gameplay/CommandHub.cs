using UnityEngine;
using System.Collections.Generic;
using GridEmpire.Commands;
using GridEmpire.Core;

namespace GridEmpire.Gameplay
{
    public class CommandHub : MonoBehaviour
    {
        public static CommandHub Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Ezt hívja az UI és az Input
        public void SubmitCommand(GameCommand cmd)
        {
            // EGYELÕRE: Azonnal végrehajtjuk lokálisan
            // KÉSÕBB: Itt küldjük el a szervernek: NetworkSend(cmd);
            ExecuteLocally(cmd);
        }

        private void ExecuteLocally(GameCommand cmd)
        {
            if (GameController.Instance != null)
            {
                cmd.Execute(GameController.Instance);
            }
        }
    }
}