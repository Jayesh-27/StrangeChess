using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 40f;
    public float lifeTime = 3f;
    public int damage = 25; // You can change this per weapon later
    
    private float currentLifeTime;
    private bool isLocalShooter;

    // The GunController calls this when pulling a bullet from the pool
    public void Initialize(bool isLocal)
    {
        isLocalShooter = isLocal;
        currentLifeTime = 0f;
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
        
        currentLifeTime += Time.deltaTime;
        if (currentLifeTime >= lifeTime)
        {
            SimpleBulletPool.Instance.ReturnBullet(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerHitbox>(out PlayerHitbox enemy))
        {
            // CRITICAL: Only the person who fired the gun is allowed to calculate damage!
            // This prevents "double damage" over the network.
            if (isLocalShooter)
            {
                enemy.OnHit(damage);
            }
            
            SimpleBulletPool.Instance.ReturnBullet(gameObject);
            return;
        }
        
        // Optional: Return bullet if it hits a wall/floor here
    }
}