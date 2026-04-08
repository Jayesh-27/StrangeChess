using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public NetworkVariable<int> health = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Resets health for the next fight
    public void ResetHealth()
    {
        if (IsServer) health.Value = 100;
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damageAmount)
    {
        if (health.Value <= 0) return;

        health.Value -= damageAmount;
        if (health.Value < 0) health.Value = 0;
        
        if (health.Value == 0)
        {
            Debug.Log($"[Combat] Player {OwnerClientId} HAS DIED! Ending Match.");
            // Tell the ChessManager who died so it can resolve the board!
            FindObjectOfType<ChessManager>().ResolveCombatServerRpc(OwnerClientId);
        }
    }
}