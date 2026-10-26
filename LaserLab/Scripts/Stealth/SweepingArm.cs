using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Rotating sterilizer arm at ankle height. Its hit area is a capsule along the arm,
    /// tested with Physics.CheckCapsule. Jump over it or time your approach.
    /// </summary>
    public class SweepingArm : MonoBehaviour
    {
        public float rotationSpeed = 50f;
        public float armLength = 4.5f;
        public float armHeight = 0.45f;
        public float armRadius = 0.2f;
        public float innerRadius = 0.5f;
        public string reason = "the sterilizer arm";

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            if (gm.State != GameState.Lab) return;

            GetCapsule(out Vector3 p0, out Vector3 p1);
            if (Physics.CheckCapsule(p0, p1, armRadius, gm.PlayerMask, QueryTriggerInteraction.Ignore))
                gm.Caught(reason);
        }

        void GetCapsule(out Vector3 p0, out Vector3 p1)
        {
            Vector3 basePos = transform.position + Vector3.up * armHeight;
            p0 = basePos + transform.forward * innerRadius;
            p1 = basePos + transform.forward * armLength;
        }

        void OnDrawGizmos()
        {
            GetCapsule(out Vector3 p0, out Vector3 p1);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(p0, armRadius);
            Gizmos.DrawWireSphere(p1, armRadius);
            Gizmos.DrawLine(p0, p1);
        }
    }
}
