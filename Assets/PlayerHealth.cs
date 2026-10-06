using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;

    [Header("Respawn Settings")]
    private Vector3 spawnPosition;

    private void Start()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        Debug.Log("Player Health Initialized: " + currentHealth);
    }

    public void TakeDamage(int damageAmount)
    {
        currentHealth -= damageAmount;
        Debug.Log("Player took damage! Current Health: " + currentHealth);

        if (currentHealth <= 0)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        Debug.Log("Player died! Respawning at start.");

        // Disable CharacterController temporarily to avoid position-reset bugs
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // Reset position to start
        transform.position = spawnPosition;

        if (controller != null) controller.enabled = true;

        // Reset health
        currentHealth = maxHealth;
    }
}