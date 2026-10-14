using UnityEngine;
using UnityEngine.InputSystem; // Required for the New Input System

public class ShooterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform firePoint;
    [SerializeField] private ProjectilePool projectilePool;

    [Header("Firing Settings")]
    [SerializeField] private float shootForce = 35f;
    [SerializeField] private float fireCooldown = 0.2f;
    [SerializeField] private float defaultAimDistance = 50f;

    private float nextFireTime = 0f;

    void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        // Make sure a mouse device is present, then check for left click
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireCooldown;
            ShootAtCursor();
        }
    }

    private void ShootAtCursor()
    {
        // Read cursor coordinates using the New Input System
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // 1. Cast an invisible ray from the camera through the mouse cursor
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
        Vector3 targetPoint;

        // 2. Check if the cursor is hovering over an actual collider (target, wall, floor)
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 100f))
        {
            targetPoint = hitInfo.point;
        }
        else
        {
            // If aiming at the empty sky, pick a point far along the ray
            targetPoint = ray.GetPoint(defaultAimDistance);
        }

        // 3. Calculate the normalized direction from the FirePoint to the target point
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        // 4. Pull a projectile from the pool, place it at FirePoint, and launch
        Projectile proj = projectilePool.GetProjectile();
        proj.transform.position = firePoint.position;
        proj.transform.rotation = Quaternion.LookRotation(shootDirection);
        proj.gameObject.SetActive(true);

        proj.Launch(shootDirection, shootForce);
    }
}