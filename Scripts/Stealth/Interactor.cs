using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// On the camera. Picks up a REVEALED hidden object you are looking at, with E.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        public float range = 2.6f;
        public float maxAngle = 30f;

        public HiddenObject Target { get; private set; }

        void Update()
        {
            Target = null;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            float best = maxAngle;
            foreach (var h in HiddenObject.All)
            {
                if (!h.collectible || !h.IsRevealed) continue;
                Vector3 to = h.transform.position - transform.position;
                if (to.magnitude > range) continue;
                float angle = Vector3.Angle(transform.forward, to);
                if (angle < best) { best = angle; Target = h; }
            }

            if (Target != null && InputUtil.InteractPressed())
            {
                Target.Collect();
                Target = null;
            }
        }
    }
}
