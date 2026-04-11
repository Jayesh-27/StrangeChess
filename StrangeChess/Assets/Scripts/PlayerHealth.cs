using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public NetworkVariable<int> health = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += (oldVal, newVal) => {
            Debug.Log($"[Combat] Player {OwnerClientId}'s health is now {newVal} HP!");
        };
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damageAmount)
    {
        // Ignore damage if the player is already dead
        if (health.Value <= 0) return;

        // Apply damage and clamp to 0 so it never goes negative
        health.Value -= damageAmount;
        if (health.Value < 0) health.Value = 0;
        
        // Trigger death ONLY once when it exactly hits 0
        if (health.Value == 0)
        {
            Debug.Log($"[Combat] Player {OwnerClientId} HAS DIED!");
            // We will add the "Return to Chess Board" logic here later!
        }
    }
}