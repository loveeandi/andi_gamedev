using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Sits at the far end of the corridor and fires laser patterns toward the player.
    /// Patterns: low bar (jump), head-height bar (crouch), vertical posts (sidestep),
    /// half grid wall (go through the gap), and combos.
    /// </summary>
    public class LaserSpawner : MonoBehaviour
    {
        public Material laserMaterial;

        [Header("Corridor shape")]
        public float corridorHalfWidth = 2.9f;
        public float corridorHeight = 3.9f;
        [Tooltip("Lasers are destroyed once they pass this world Z (the corridor start).")]
        public float destroyZ = 0.3f;

        [Header("Difficulty")]
        public float laserSpeed = 7f;
        public float damage = 20f;
        public Vector2 spawnInterval = new Vector2(1.6f, 2.6f);
        [Tooltip("At the end of the corridor the interval is multiplied by this (gets harder).")]
        public float endIntervalMultiplier = 0.65f;
        [Tooltip("Stop spawning when the player is this close to the spawner (so lasers never spawn inside them).")]
        public float safeDistance = 5f;
        public float beamThickness = 0.12f;

        float timer = 1.5f;
        static readonly float[] Lanes = { -2f, 0f, 2f };

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Corridor) return;

            float playerZ = gm.Player.position.z;
            if (transform.position.z - playerZ < safeDistance) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            SpawnPattern(Random.Range(0, 6));

            float progress = Mathf.Clamp01(playerZ / Mathf.Max(1f, transform.position.z));
            float mul = Mathf.Lerp(1f, endIntervalMultiplier, progress);
            timer = Random.Range(spawnInterval.x, spawnInterval.y) * mul;
        }

        void SpawnPattern(int pattern)
        {
            float z = transform.position.z;
            float w = corridorHalfWidth * 2f;
            float h = corridorHeight;
            float t = beamThickness;

            switch (pattern)
            {
                case 0: // low bar -> JUMP
                    Beam(new Vector3(0f, 0.3f, z), new Vector3(w, t, t));
                    break;

                case 1: // head-height bar -> CROUCH
                    Beam(new Vector3(0f, 1.45f, z), new Vector3(w, t, t));
                    Beam(new Vector3(0f, 2.2f, z), new Vector3(w, t, t));
                    Beam(new Vector3(0f, 3.0f, z), new Vector3(w, t, t));
                    break;

                case 2: // one vertical post -> SIDESTEP
                    Beam(new Vector3(Lanes[Random.Range(0, 3)], h * 0.5f, z), new Vector3(t, h, t));
                    break;

                case 3: // two posts, one free lane
                {
                    int free = Random.Range(0, 3);
                    for (int i = 0; i < 3; i++)
                        if (i != free) Beam(new Vector3(Lanes[i], h * 0.5f, z), new Vector3(t, h, t));
                    break;
                }

                case 4: // half grid wall -> use the gap
                {
                    bool left = Random.value < 0.5f;
                    float gap = 2.2f;
                    float barW = w - gap;
                    float cx = left ? -corridorHalfWidth + barW * 0.5f : corridorHalfWidth - barW * 0.5f;
                    for (float y = 0.35f; y < h; y += 0.6f)
                        Beam(new Vector3(cx, y, z), new Vector3(barW, t, t));
                    break;
                }

                case 5: // low bar + a post -> jump in a free lane
                    Beam(new Vector3(0f, 0.3f, z), new Vector3(w, t, t));
                    Beam(new Vector3(Lanes[Random.Range(0, 3)], h * 0.5f, z), new Vector3(t, h, t));
                    break;
            }
        }

        void Beam(Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Laser";
            Destroy(go.GetComponent<Collider>()); // detection is done with CheckBox, not physics
            go.transform.SetParent(transform, true);
            go.transform.position = position;
            go.transform.localScale = size;

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = laserMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.AddComponent<LaserBeam>().Init(laserSpeed, damage, destroyZ, size);
        }

        public void ClearAll()
        {
            foreach (Transform child in transform)
                if (child.GetComponent<LaserBeam>() != null) Destroy(child.gameObject);
        }
    }
}
