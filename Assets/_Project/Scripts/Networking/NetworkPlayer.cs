using GridEmpire.Shared;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            //Debug.Log($"en vagyok a helyi jatekos! ID: {OwnerClientId}");
            // Itt majd ossze kell kotnunk a helyi GameControllerrel
        }
    }

    // Ezt fogja hivni a CommandHub a kliensnel
    [ServerRpc]
    public void SendCommandServerRpc(int unitId, int targetCellId, ActionType type)
    {
        // A SZERVER oldalon fut le:
        //Debug.Log($"Szerver megkapta: Unit {unitId} -> Cell {targetCellId}");

        // Itt adjuk hozza a TurnResolver listajahoz a parancsot
        // TurnResolver.Instance.EnqueueAction(...);
    }
}