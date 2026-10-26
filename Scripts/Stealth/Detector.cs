using System.Collections.Generic;
using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Base class for anything that can SEE the player (patrol droids, security cameras).
    ///
    /// Detection pipeline:
    ///   1. Physics.CheckSphere  - broad phase: is the player anywhere within view range?
    ///   2. Vector3.Angle        - is the player inside the vision cone?
    ///   3. Physics.Raycast      - line of sight to the player's head or chest (cover blocks it).
    /// While the player is seen, Suspicion fills up. At 1.0 the player is caught.
    /// </summary>
    public abstract class Detector : MonoBehaviour
    {
        public static readonly List<Detector> All = new List<Detector>();

        public string displayName = "Security";
        [Tooltip("Extra view range while the player's flashlight is on (light gives you away).")]
        public float flashlightRangeBonus = 5f;
        [Tooltip("View range multiplier while the player is crouching.")]
        public float crouchRangeMultiplier = 0.7f;
        [Tooltip("How fast suspicion drops when the player is out of sight (per second).")]
        public float suspicionDecay = 0.35f;

        public float Suspicion { get; protected set; }
        public bool SeesPlayer { get; protected set; }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public static void ResetAll()
        {
            foreach (var d in All) { d.Suspicion = 0f; d.SeesPlayer = false; }
        }

        public static float MaxSuspicion()
        {
            float max = 0f;
            foreach (var d in All) max = Mathf.Max(max, d.Suspicion);
            return max;
        }

        protected bool CanDetect()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.State == GameState.Lab && !gm.InGrace;
        }

        protected float EffectiveRange(float baseRange)
        {
            var gm = GameManager.Instance;
            float r = baseRange;
            if (Flashlight.Instance != null && Flashlight.Instance.IsOn) r += flashlightRangeBonus;
            if (gm != null && gm.IsCrouching) r *= crouchRangeMultiplier;
            return r;
        }

        protected bool CanSeePlayer(Vector3 eyePos, Vector3 forward, float range, float fov)
        {
            var gm = GameManager.Instance;

            // 1) Broad phase: CheckSphere around the eye
            if (!Physics.CheckSphere(eyePos, range, gm.PlayerMask, QueryTriggerInteraction.Ignore))
                return false;

            // 2 + 3) Cone + line of sight, tested against head and chest
            return RayHitsPlayer(eyePos, forward, gm.PlayerHead, range, fov, gm.PlayerMask) ||
                   RayHitsPlayer(eyePos, forward, gm.PlayerCenter, range, fov, gm.PlayerMask);
        }

        static bool RayHitsPlayer(Vector3 eyePos, Vector3 forward, Vector3 target, float range, float fov, int playerMask)
        {
            Vector3 toTarget = target - eyePos;
            if (toTarget.magnitude > range) return false;
            if (Vector3.Angle(forward, toTarget) > fov * 0.5f) return false;

            // DefaultRaycastLayers skips "Ignore Raycast", where the droids/cameras live,
            // so a detector never blocks its own view.
            if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit hit, range,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return ((1 << hit.collider.gameObject.layer) & playerMask) != 0;
            }
            return false;
        }

        protected void AddSuspicion(float amount)
        {
            Suspicion = Mathf.Clamp01(Suspicion + amount);
            if (Suspicion >= 1f) GameManager.Instance.Caught(displayName);
        }

        protected void DecaySuspicion()
        {
            Suspicion = Mathf.Max(0f, Suspicion - suspicionDecay * Time.deltaTime);
        }

        protected static Color AlertColor(Color calm, float suspicion)
        {
            if (suspicion < 0.5f) return Color.Lerp(calm, new Color(1f, 0.8f, 0.1f), suspicion * 2f);
            return Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(1f, 0.1f, 0.05f), (suspicion - 0.5f) * 2f);
        }
    }
}
