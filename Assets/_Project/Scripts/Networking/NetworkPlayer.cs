using GridEmpire.Shared;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            //Debug.Log($"Én vagyok a helyi játékos! ID: {OwnerClientId}");
            // Itt majd össze kell kötnünk a helyi GameControllerrel
        }
    }

    // Ezt fogja hívni a CommandHub a kliensnél
    [ServerRpc]
    public void SendCommandServerRpc(int unitId, int targetCellId, ActionType type)
    {
        // A SZERVER oldalon fut le:
        //Debug.Log($"Szerver megkapta: Unit {unitId} -> Cell {targetCellId}");

        // Itt adjuk hozzá a TurnResolver listájához a parancsot
        // TurnResolver.Instance.EnqueueAction(...);
    }
}