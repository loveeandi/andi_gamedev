using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Wall-mounted camera that sweeps left and right.
    /// Uses the Detector pipeline (CheckSphere + cone + Raycast). Freezes and locks on while it sees you.
    /// </summary>
    public class SecurityCamera : Detector
    {
        public Transform pivot;
        public Transform lens;
        public Light visionLight;

        public float sweepAngle = 45f;
        public float sweepSpeed = 0.6f;
        public float viewDistance = 14f;
        public float viewAngle = 38f;
        public float timeToSpot = 1.1f;

        Vector3 baseEuler;
        float phase;
        Color calmColor;

        void Start()
        {
            baseEuler = pivot.localEulerAngles;
            if (visionLight != null) calmColor = visionLight.color;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            float range = EffectiveRange(viewDistance);
            bool seen = CanDetect() && CanSeePlayer(lens.position, lens.forward, range, viewAngle);
            SeesPlayer = seen;

            if (seen)
            {
                AddSuspicion(Time.deltaTime / timeToSpot);
            }
            else
            {
                DecaySuspicion();
                phase += Time.deltaTime * sweepSpeed;
                pivot.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y + Mathf.Sin(phase) * sweepAngle, 0f);
            }

            if (visionLight != null)
            {
                visionLight.color = AlertColor(calmColor, Suspicion);
                visionLight.range = range;
                visionLight.spotAngle = viewAngle;
            }
        }
    }
}
