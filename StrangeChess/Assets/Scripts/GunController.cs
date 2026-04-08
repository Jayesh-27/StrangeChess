using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; 

public class GunController : NetworkBehaviour
{
    [Header("Gun Stats")]
    public float fireRate = 0.2f;
    public int maxAmmo = 15;
    public float reloadTime = 1.5f;
    public bool canFire = false; // NEW: Locks the gun during countdown!
    
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
        // If the gun is locked by the countdown, or reloading, do nothing.
        if (!canFire || isReloading) return;

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
        SimpleBulletPool.Instance.GetBullet(bulletSpawnPoint.position, bulletSpawnPoint.rotation, true);
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
        if (NetworkManager.Singleton.LocalClientId == shooterId) return;
        SimpleBulletPool.Instance.GetBullet(position, rotation, false);
    }

    private System.Collections.IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        isReloading = false;
    }
}