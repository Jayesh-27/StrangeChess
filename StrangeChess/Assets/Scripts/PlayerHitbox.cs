using UnityEngine;

public class PlayerHitbox : MonoBehaviour
{
    // Drag the PlayerHealth script (on the parent object) into this slot in the Inspector
    public PlayerHealth playerHealth; 

    public void OnHit(int damage)
    {
        // We pass the hit up to the official health script
        if (playerHealth != null)
        {
            playerHealth.TakeDamageServerRpc(damage);
        }
    }
}