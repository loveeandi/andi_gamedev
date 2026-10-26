using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Security droid that walks a loop of waypoints.
    /// SIGHT: CheckSphere + cone + Raycast (see Detector).
    /// HEARING: Physics.CheckSphere around the droid; sprinting makes the radius much bigger.
    /// When suspicious it stops and turns to face the player.
    /// </summary>
    public class PatrolGuard : Detector
    {
        [Header("Patrol")]
        public Vector3[] waypoints;
        public float moveSpeed = 1.6f;
        public float turnSpeed = 160f;
        public float waitAtPoint = 1.2f;

        [Header("Senses")]
        public Transform eye;
        public Light visionLight;
        public float viewDistance = 9f;
        public float viewAngle = 70f;
        [Tooltip("Seconds of continuous sight (at max range) before the player is caught.")]
        public float timeToSpot = 1.4f;
        public float hearingRadius = 2.2f;
        public float sprintHearingRadius = 6f;

        int index;
        float waitTimer;
        Color calmColor;

        void Start()
        {
            if (visionLight != null) calmColor = visionLight.color;
            if (waypoints != null && waypoints.Length > 1) index = 1;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            float range = EffectiveRange(viewDistance);
            bool seen = false, heard = false;

            if (CanDetect())
            {
                seen = CanSeePlayer(eye.position, eye.forward, range, viewAngle);
                float hearR = gm.IsSprinting ? sprintHearingRadius : hearingRadius;
                heard = Physics.CheckSphere(transform.position + Vector3.up, hearR, gm.PlayerMask,
                                            QueryTriggerInteraction.Ignore);
            }
            SeesPlayer = seen;

            if (seen)
            {
                float dist = Vector3.Distance(eye.position, gm.PlayerCenter);
                float closeness = 1f - Mathf.Clamp01(dist / range);
                AddSuspicion(Time.deltaTime / timeToSpot * (1f + 2f * closeness));
            }
            else if (heard)
            {
                AddSuspicion(Time.deltaTime * 0.6f);
            }
            else
            {
                DecaySuspicion();
            }

            if ((seen || heard) && Suspicion > 0.15f) FaceTowards(gm.Player.position);
            else Patrol();

            if (visionLight != null)
            {
                visionLight.color = AlertColor(calmColor, Suspicion);
                visionLight.range = range;
                visionLight.spotAngle = viewAngle;
            }
        }

        void Patrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            if (waitTimer > 0f) { waitTimer -= Time.deltaTime; return; }

            Vector3 target = waypoints[index];
            target.y = transform.position.y;
            Vector3 to = target - transform.position;

            if (to.magnitude < 0.1f)
            {
                index = (index + 1) % waypoints.Length;
                waitTimer = waitAtPoint;
                return;
            }

            Quaternion look = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, look) < 20f)
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }

        void FaceTowards(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to),
                                                          turnSpeed * 1.5f * Time.deltaTime);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, hearingRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, sprintHearingRadius);
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
                Gizmos.DrawLine(waypoints[i], waypoints[(i + 1) % waypoints.Length]);
        }
    }
}
