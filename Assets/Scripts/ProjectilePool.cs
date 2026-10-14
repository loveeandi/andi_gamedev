using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private int initialPoolSize = 15;

    private List<Projectile> pool = new List<Projectile>();

    void Awake()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewProjectile();
        }
    }

    private Projectile CreateNewProjectile()
    {
        Projectile newProj = Instantiate(projectilePrefab, transform);
        newProj.gameObject.SetActive(false);
        pool.Add(newProj);
        return newProj;
    }

    public Projectile GetProjectile()
    {
        // Find an available inactive projectile in the list
        foreach (Projectile proj in pool)
        {
            if (!proj.gameObject.activeInHierarchy)
            {
                return proj;
            }
        }

        // If all projectiles are currently flying in the air, expand the pool safely
        return CreateNewProjectile();
    }
}