using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Decontamination scanner: a sheet of light sliding back and forth across a doorway.
    /// Touching it (Physics.CheckBox) triggers detection instantly. Time your crossing.
    /// </summary>
    public class ScannerGate : MonoBehaviour
    {
        public Vector3 pointA;
        public Vector3 pointB;
        public float cycleTime = 4.5f;
        public Vector3 halfExtents = new Vector3(1.3f, 1.5f, 0.2f);
        public string reason = "the decontamination scanner";

        void Update()
        {
            float s = (Mathf.Sin(Time.time * Mathf.PI * 2f / cycleTime) + 1f) * 0.5f;
            transform.position = Vector3.Lerp(pointA, pointB, s);

            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Lab) return;

            if (Physics.CheckBox(transform.position, halfExtents, transform.rotation,
                                 gm.PlayerMask, QueryTriggerInteraction.Ignore))
            {
                gm.Caught(reason);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}
