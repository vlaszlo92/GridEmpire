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

        public void SubmitCommand(GameCommand cmd)
        {
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