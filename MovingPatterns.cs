using UnityEngine;

public class MovingPattern : MonoBehaviour
{
    private float moveSpeed = 8.0f;

    public void SetSpeed(float speed)
    {
        moveSpeed = speed;
    }

    private void Update()
    {
        transform.Translate(Vector3.right * moveSpeed * Time.deltaTime, Space.World);

        if (transform.position.x > 1.2f)
        {
            Destroy(gameObject);
        }
    }
}