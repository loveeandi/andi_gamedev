using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// A single laser beam travelling down the corridor toward the player.
    /// Uses Physics.CheckBox every frame to test for contact with the player.
    /// </summary>
    public class LaserBeam : MonoBehaviour
    {
        float speed, damage, destroyZ;
        Vector3 halfExtents;

        public void Init(float speed, float damage, float destroyZ, Vector3 size)
        {
            this.speed = speed;
            this.damage = damage;
            this.destroyZ = destroyZ;
            halfExtents = size * 0.5f + Vector3.one * 0.02f;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            transform.position += Vector3.back * speed * Time.deltaTime;

            if (Physics.CheckBox(transform.position, halfExtents, transform.rotation,
                                 gm.PlayerMask, QueryTriggerInteraction.Ignore))
            {
                gm.Health.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            if (transform.position.z < destroyZ) Destroy(gameObject);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, halfExtents * 2f);
        }
    }
}
