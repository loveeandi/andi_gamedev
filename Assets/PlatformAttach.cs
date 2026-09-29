using UnityEngine;

public class PlatformAttach : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Check if the object is tagged as Player
        if (collision.gameObject.CompareTag("Player"))
        {
            // Verify collision normal is pointing downward (meaning player is standing on top)
            if (collision.contacts[0].normal.y < -0.5f)
            {
                collision.transform.SetParent(transform);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.SetParent(null);
        }
    }
}