using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class Target : MonoBehaviour
{
    // Broadcasts the point value whenever ANY target is knocked down
    public static event Action<int> OnTargetKnockedDown;

    [Header("Scoring & Detection")]
    [SerializeField] private int pointValue = 10;
    [Tooltip("1.0 = completely upright, 0.0 = lying flat on its side. 0.65 is roughly a 49-degree tilt.")]
    [SerializeField] private float tiltThreshold = 0.65f;
    [Tooltip("How far below its starting height the target must drop to count as fallen off a ledge.")]
    [SerializeField] private float fallThresholdY = 0.4f;

    [Header("Physics Tuning")]
    [Tooltip("Raises the center of mass slightly so projectiles tip the target instead of sliding it.")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, 0.25f, 0f);

    [Header("Visual Feedback")]
    [SerializeField] private Color knockedDownColor = Color.gray;

    private Rigidbody rb;
    private Renderer[] childRenderers; // Array to hold ALL shapes inside the target
    private float startY;
    private bool isKnockedDown = false;
    private float graceTimer = 0.5f;

    public bool IsKnockedDown => isKnockedDown;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Grab the Renderer on the parent AND all child shape objects
        childRenderers = GetComponentsInChildren<Renderer>();

        rb.centerOfMass += centerOfMassOffset;
    }

    void Start()
    {
        startY = transform.position.y;
    }

    void Update()
    {
        if (isKnockedDown) return;

        if (graceTimer > 0f)
        {
            graceTimer -= Time.deltaTime;
            startY = transform.position.y;
            return;
        }

        if (rb.isKinematic) startY = transform.position.y;

        bool isTilted = Vector3.Dot(transform.up, Vector3.up) < tiltThreshold;
        bool hasFallen = transform.position.y < (startY - fallThresholdY);

        if (isTilted || hasFallen)
        {
            MarkAsKnockedDown();
        }
    }

    private void MarkAsKnockedDown()
    {
        isKnockedDown = true;

        // Loop through every child shape and turn all of its materials grey
        foreach (Renderer rend in childRenderers)
        {
            foreach (Material mat in rend.materials)
            {
                mat.color = knockedDownColor;

                // Ensure URP Lit/Unlit shaders also update their base color
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", knockedDownColor);
                }
            }
        }

        OnTargetKnockedDown?.Invoke(pointValue);
    }
}