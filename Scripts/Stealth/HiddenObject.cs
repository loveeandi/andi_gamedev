using System.Collections.Generic;
using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Invisible until the flashlight is on AND this object is inside its light cone.
    /// Used for the keycards (collectible) and the UV wall hints (not collectible).
    /// Hidden objects have no colliders, so they can't be bumped into or seen any other way.
    /// </summary>
    public class HiddenObject : MonoBehaviour
    {
        public static readonly List<HiddenObject> All = new List<HiddenObject>();

        public bool collectible = true;
        public string displayName = "Keycard";
        [Tooltip("Seconds the object stays visible after leaving the light.")]
        public float lingerTime = 0.3f;
        public bool spin = true;

        public bool IsRevealed => visibleTimer > 0f;

        Renderer[] renderers;
        Light[] lights;
        float visibleTimer;
        bool visible = true;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            lights = GetComponentsInChildren<Light>(true);
            SetVisible(false);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            var fl = Flashlight.Instance;
            if (fl != null && fl.IsPointLit(transform.position)) visibleTimer = lingerTime;
            else visibleTimer -= Time.deltaTime;

            SetVisible(visibleTimer > 0f);

            if (spin && collectible) transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        }

        void SetVisible(bool v)
        {
            if (v == visible) return;
            visible = v;
            foreach (var r in renderers) r.enabled = v;
            foreach (var l in lights) l.enabled = v;
        }

        public void Collect()
        {
            if (!collectible) return;
            GameManager.Instance.AddKeycard();
            Destroy(gameObject);
        }
    }
}
