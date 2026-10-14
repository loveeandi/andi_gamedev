using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovingTarget : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Direction of movement (e.g., X=1 for left/right, Y=1 for up/down, Z=1 for forward/back).")]
    [SerializeField] private Vector3 moveDirection = Vector3.right;
    [Tooltip("How far from the starting point the target travels in each direction.")]
    [SerializeField] private float moveDistance = 3f;
    [Tooltip("How fast the target oscillates back and forth.")]
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("Offset the starting point in the cycle so multiple targets don't move in exact sync.")]
    [SerializeField] private float timeOffset = 0f;

    private Rigidbody rb;
    private Vector3 startPos;
    private bool wasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Keep the Rigidbody kinematic while patrolling so it doesn't tip over from movement
        rb.isKinematic = true;
        startPos = transform.position;
    }

    void FixedUpdate()
    {
        // Stop patrolling once struck by a projectile
        if (wasHit) return;

        // Calculate smooth sine-wave oscillation
        float offset = Mathf.Sin((Time.fixedTime + timeOffset) * moveSpeed) * moveDistance;
        Vector3 targetPosition = startPos + (moveDirection.normalized * offset);

        rb.MovePosition(targetPosition);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Unlock physics as soon as a projectile hits the target (or another falling target hits it)
        if (!wasHit && (collision.gameObject.CompareTag("Projectile") || collision.gameObject.GetComponent<Target>() != null))
        {
            wasHit = true;
            rb.isKinematic = false;

            // Because switching from Kinematic to Dynamic absorbs the initial collision frame,
            // we manually transfer the projectile's momentum at the exact point of impact.
            Rigidbody incomingRb = collision.rigidbody;
            if (incomingRb != null && collision.contactCount > 0)
            {
                Vector3 impactPoint = collision.GetContact(0).point;
                // Note: Swap linearVelocity for velocity if on Unity 2022 LTS or older
                Vector3 impactForce = incomingRb.linearVelocity * incomingRb.mass;
                rb.AddForceAtPosition(impactForce, impactPoint, ForceMode.Impulse);
            }
        }
    }
}