using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class AmmoScript : MonoBehaviour
{
    [Header("Projectile")]
    public float speed = 20f;

    [Tooltip("Seconds before the bullet auto-returns to the pool if it hits nothing.")]
    public float lifetime = 3f;

    private Rigidbody rb;
    private WeaponController pool;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Called by WeaponController right after spawning from the pool.
    public void Launch(Vector3 direction, WeaponController owner)
    {
        pool = owner;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * speed, ForceMode.VelocityChange);

        StopAllCoroutines();
        StartCoroutine(LifetimeCoroutine());
    }

    private IEnumerator LifetimeCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        Debug.Log("[AmmoScript] Lifetime expired, returning to pool.");
        ReturnSelf();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[AmmoScript] Hit: {collision.gameObject.name}");
        StopAllCoroutines();
        ReturnSelf();
    }

    private void ReturnSelf()
    {
        pool.ReturnToPool(gameObject);
    }
}
