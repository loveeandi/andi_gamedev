using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Player flashlight (Spot Light on the camera). Toggle with F or right mouse.
    /// Runs on a battery that recharges while it's off. Being lit makes you easier to spot.
    /// IsPointLit() tells hidden objects whether they are inside the light cone:
    ///   range check -> cone angle check -> Raycast for occlusion.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        public static Flashlight Instance { get; private set; }

        public float maxBattery = 100f;
        public float drainPerSecond = 4f;
        public float rechargePerSecond = 7f;

        public bool IsOn { get; private set; }
        public float Battery { get; private set; }

        Light spot;
        float baseIntensity;
        int occluderMask;

        void Awake()
        {
            Instance = this;
            spot = GetComponent<Light>();
            baseIntensity = spot.intensity;
            Battery = maxBattery;
            spot.enabled = false;
            occluderMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            if (InputUtil.FlashlightPressed())
            {
                if (!IsOn && Battery < 5f) gm?.ShowMessage("Battery depleted - let it recharge", 1.5f);
                else IsOn = !IsOn;
            }

            if (IsOn)
            {
                Battery -= drainPerSecond * Time.deltaTime;
                if (Battery <= 0f) { Battery = 0f; IsOn = false; }
            }
            else
            {
                Battery = Mathf.Min(maxBattery, Battery + rechargePerSecond * Time.deltaTime);
            }

            spot.enabled = IsOn;
            if (IsOn)
            {
                bool flicker = Battery < 20f && Random.value < 0.08f;
                spot.intensity = baseIntensity * (flicker ? 0.25f : 1f);
            }
        }

        public bool IsPointLit(Vector3 point)
        {
            if (!IsOn) return false;

            Vector3 origin = transform.position;
            Vector3 to = point - origin;
            float dist = to.magnitude;

            if (dist > spot.range || dist < 0.01f) return false;                    // range
            if (Vector3.Angle(transform.forward, to) > spot.spotAngle * 0.5f) return false; // cone

            // occlusion: anything solid between the flashlight and the point blocks the light
            return !Physics.Raycast(origin, to / dist, dist - 0.1f, occluderMask, QueryTriggerInteraction.Ignore);
        }
    }
}
