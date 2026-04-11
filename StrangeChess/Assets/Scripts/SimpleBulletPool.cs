using System.Collections.Generic;
using UnityEngine;

public class SimpleBulletPool : MonoBehaviour
{
    public static SimpleBulletPool Instance;
    public GameObject bulletPrefab;
    public int initialPoolSize = 30;

    private Queue<GameObject> bulletQueue = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        for (int i = 0; i < initialPoolSize; i++) CreateNewBullet();
    }

    private GameObject CreateNewBullet()
    {
        GameObject bullet = Instantiate(bulletPrefab);
        bullet.SetActive(false);
        bulletQueue.Enqueue(bullet);
        return bullet;
    }

    public GameObject GetBullet(Vector3 position, Quaternion rotation, bool isLocalShooter)
    {
        if (bulletQueue.Count == 0) CreateNewBullet();

        GameObject bullet = bulletQueue.Dequeue();
        bullet.transform.position = position;
        bullet.transform.rotation = rotation;
        bullet.SetActive(true);
        
        // Tell the bullet if we are the ones who shot it
        bullet.GetComponent<Bullet>().Initialize(isLocalShooter);
        
        return bullet;
    }

    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        bulletQueue.Enqueue(bullet);
    }
}