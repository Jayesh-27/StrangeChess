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

        // 1. INSTANT LOCAL SPAWN: Spawn the bullet instantly for the person shooting (No delay!)
        SimpleBulletPool.Instance.GetBullet(bulletSpawnPoint.position, bulletSpawnPoint.rotation, true);

        // 2. Tell the network to spawn it for everyone ELSE
        ShootServerRpc(bulletSpawnPoint.position, bulletSpawnPoint.rotation, NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShootServerRpc(Vector3 position, Quaternion rotation, ulong shooterId)
    {
        ShootClientRpc(position, rotation, shooterId);
    }

    [ClientRpc]
    private void ShootClientRpc(Vector3 position, Quaternion rotation, ulong shooterId)
    {
        // 3. Ignore this message if WE shot the bullet, because we already spawned it locally in Fire()!
        if (NetworkManager.Singleton.LocalClientId == shooterId) return;
        
        SimpleBulletPool.Instance.GetBullet(position, rotation, false);
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