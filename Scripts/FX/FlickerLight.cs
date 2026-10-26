using UnityEngine;

namespace LaserLab
{
    /// <summary>Faulty fluorescent tube. Purely atmospheric.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float minInterval = 0.05f;
        public float maxInterval = 0.8f;
        [Range(0f, 1f)] public float offChance = 0.3f;

        Light lightSource;
        float baseIntensity, timer;

        void Awake()
        {
            lightSource = GetComponent<Light>();
            baseIntensity = lightSource.intensity;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            bool off = Random.value < offChance;
            lightSource.intensity = off ? baseIntensity * 0.05f : baseIntensity * Random.Range(0.8f, 1.1f);
            timer = off ? Random.Range(minInterval, 0.15f) : Random.Range(minInterval, maxInterval);
        }
    }
}
