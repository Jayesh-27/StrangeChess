using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; 

public class GunController : NetworkBehaviour
{
    [Header("Gun Stats")]
    public float fireRate = 0.2f;
    public int maxAmmo = 15;
    public float reloadTime = 1.5f;
    
    [Header("References")]
    public Transform bulletSpawnPoint;
    public InputActionProperty triggerAction; 

    private int currentAmmo;
    private float lastFireTime;
    private bool isReloading;

    void Start()
    {
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        if (isReloading) return;

        float triggerValue = triggerAction.action.ReadValue<float>();
        
        if (triggerValue > 0.5f && Time.time >= lastFireTime + fireRate)
        {
            if (currentAmmo > 0) Fire();
            else StartCoroutine(Reload());
        }
    }

    private void Fire()
    {
        lastFireTime = Time.time;
        currentAmmo--;

        // Pass our Client ID
        ShootServerRpc(bulletSpawnPoint.position, bulletSpawnPoint.rotation, NetworkManager.Singleton.LocalClientId);
    }

    // CRITICAL FIX: Allow the client to fire the ServerRpc!
    [ServerRpc(RequireOwnership = false)]
    private void ShootServerRpc(Vector3 position, Quaternion rotation, ulong shooterId)
    {
        ShootClientRpc(position, rotation, shooterId);
    }

    [ClientRpc]
    private void ShootClientRpc(Vector3 position, Quaternion rotation, ulong shooterId)
    {
        bool isLocalShooter = (NetworkManager.Singleton.LocalClientId == shooterId);
        SimpleBulletPool.Instance.GetBullet(position, rotation, isLocalShooter);
    }

    private System.Collections.IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        isReloading = false;
        Debug.Log("Reloaded!");
    }
}