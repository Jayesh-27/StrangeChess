using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class WeaponController : MonoBehaviour
{
    [Header("Shooting")]
    public float fireRate = 5f;
    public bool autoFire = false;
    public Transform firePoint;

    [Header("Ammo")]
    public GameObject ammoPrefab;
    public int magazineSize = 12;
    public int maxAmmo = 60;
    public int PoolSize = 10;

    [Header("Reload")]
    public XRNode reloadHand = XRNode.RightHand;
    [Range(0.1f, 0.99f)]
    public float gripThreshold = 0.8f;
    public float reloadTime = 2f;

    [Header("Input")]
    public XRNode hand = XRNode.RightHand;
    [Range(0.1f, 0.99f)]
    public float triggerThreshold = 0.8f;

    [Header("Haptics")]
    [Range(0f, 1f)]
    public float hapticAmplitude = 0.35f;
    public float hapticDuration = 0.08f;

    [Header("Debug")]
    public bool debugLogs = false;

    private InputDevice controller;
    private InputDevice reloadController;
    private float nextFireTime;
    private bool triggerWasPressed;
    private bool reloadWasPressed;

    private int currentMagazine;
    private int currentTotalAmmo;
    private bool isReloading;
    private Queue<GameObject> ammoPool = new Queue<GameObject>();

    private void OnEnable()
    {
        InputDevices.deviceConnected += OnDeviceChanged;
        InputDevices.deviceDisconnected += OnDeviceChanged;
        AcquireController();
        AcquireReloadController();
    }

    private void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceChanged;
        InputDevices.deviceDisconnected -= OnDeviceChanged;
    }

    private void Start()
    {
        currentMagazine = magazineSize;
        // maxAmmo is total ammo capacity (magazine + reserve)
        currentTotalAmmo = maxAmmo - magazineSize;
        if (currentTotalAmmo < 0)
            currentTotalAmmo = 0;

        // Start pool at exactly magazineSize — grows automatically if needed
        PoolSize = magazineSize;
        ammoPool = new Queue<GameObject>();
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject obj = Instantiate(ammoPrefab, transform.position, transform.rotation);
            obj.SetActive(false);
            ammoPool.Enqueue(obj);
        }
    }

    private void Update()
    {
        if (!controller.isValid)
            AcquireController();

        if (!reloadController.isValid)
            AcquireReloadController();

        bool triggerPressed = ReadTriggerPressed();

        bool reloadPressed = ReadReloadPressed();
        if (reloadPressed && !reloadWasPressed && !isReloading)
            StartCoroutine(ReloadCoroutine());

        if (!isReloading)
        {
            if (autoFire)
            {
                if (triggerPressed && Time.time >= nextFireTime)
                    FireOnce();
            }
            else
            {
                if (triggerPressed && !triggerWasPressed && Time.time >= nextFireTime)
                    FireOnce();
            }
        }

        triggerWasPressed = triggerPressed;
        reloadWasPressed = reloadPressed;
    }

    private void FireOnce()
    {
        if (currentMagazine <= 0)
        {
            if (debugLogs) Debug.Log("[WeaponController] Magazine empty!");
            return;
        }

        currentMagazine--;
        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

        // Spawn ammo from pool
        GameObject ammo = GetFromPool();
        ammo.transform.position = firePoint.position;
        ammo.transform.rotation = firePoint.rotation;
        ammo.SetActive(true);
        ammo.GetComponent<AmmoScript>().Launch(firePoint.forward, this);

        // Vibrate every shot (handles both auto and semi-auto)
        Vibrate();

        if (debugLogs)
            Debug.Log($"[WeaponController] Fired. Magazine: {currentMagazine}/{magazineSize}  Total ammo: {currentTotalAmmo}");
    }

    private IEnumerator ReloadCoroutine()
    {
        int need = magazineSize - currentMagazine;
        if (need <= 0 || currentTotalAmmo <= 0)
        {
            if (debugLogs) Debug.Log("[WeaponController] Nothing to reload");
            yield break;
        }

        isReloading = true;
        if (debugLogs) Debug.Log($"[WeaponController] Reloading... ({reloadTime}s)");

        yield return new WaitForSeconds(reloadTime);

        int take = Mathf.Min(need, currentTotalAmmo);
        currentMagazine += take;
        currentTotalAmmo -= take;
        isReloading = false;

        if (debugLogs)
            Debug.Log($"[WeaponController] Reload done. Magazine: {currentMagazine}/{magazineSize}  Reserve: {currentTotalAmmo}");
    }

    private GameObject GetFromPool()
    {
        if (ammoPool.Count == 0)
        {
            // Pool exhausted — grow it by one, matching the GunController pattern
            GameObject extra = Instantiate(ammoPrefab, transform.position, transform.rotation);
            PoolSize++;
            return extra;
        }

        return ammoPool.Dequeue();
    }

    public void ReturnToPool(GameObject ammo)
    {
        ammo.SetActive(false);
        ammo.transform.position = transform.position;
        ammoPool.Enqueue(ammo);
    }

    private void Vibrate()
    {
        if (controller.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
            controller.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
    }

    private bool ReadTriggerPressed()
    {
        if (controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton))
            return triggerButton;

        if (controller.TryGetFeatureValue(CommonUsages.trigger, out float triggerAxis))
            return triggerAxis >= triggerThreshold;

        return false;
    }

    private bool ReadReloadPressed()
    {
        // "Palm trigger" on Quest/Touch controllers is typically the grip button.
        if (reloadController.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton))
            return gripButton;

        if (reloadController.TryGetFeatureValue(CommonUsages.grip, out float gripAxis))
            return gripAxis >= gripThreshold;

        return false;
    }

    private void AcquireController()
    {
        controller = InputDevices.GetDeviceAtXRNode(hand);
        if (controller.isValid)
            return;

        var devices = new List<InputDevice>();
        InputDeviceCharacteristics handedness = hand == XRNode.LeftHand
            ? InputDeviceCharacteristics.Left
            : InputDeviceCharacteristics.Right;

        InputDevices.GetDevicesWithCharacteristics(
            handedness | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
            devices);

        if (devices.Count > 0)
            controller = devices[0];
    }

    private void AcquireReloadController()
    {
        reloadController = InputDevices.GetDeviceAtXRNode(reloadHand);
        if (reloadController.isValid)
            return;

        var devices = new List<InputDevice>();
        InputDeviceCharacteristics handedness = reloadHand == XRNode.LeftHand
            ? InputDeviceCharacteristics.Left
            : InputDeviceCharacteristics.Right;

        InputDevices.GetDevicesWithCharacteristics(
            handedness | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
            devices);

        if (devices.Count > 0)
            reloadController = devices[0];
    }

    private void OnDeviceChanged(InputDevice _)
    {
        AcquireController();
        AcquireReloadController();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(firePoint.position, firePoint.forward * 20f);
    }
}