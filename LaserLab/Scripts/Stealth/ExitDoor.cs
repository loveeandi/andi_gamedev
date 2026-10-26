using UnityEngine;

namespace LaserLab
{
    /// <summary>Emergency exit door: slides up once all keycards are collected. Red/green status light.</summary>
    public class ExitDoor : MonoBehaviour
    {
        public float openHeight = 2.6f;
        public float openSpeed = 1.5f;
        public Light statusLight;
        public Renderer statusRenderer;

        Vector3 closedPos;
        bool wasOpen;
        Material statusMat;

        static readonly Color Locked = new Color(1f, 0.08f, 0.05f);
        static readonly Color Unlocked = new Color(0.15f, 1f, 0.35f);

        void Start()
        {
            closedPos = transform.position;
            if (statusRenderer != null) statusMat = statusRenderer.material;
            SetStatus(false);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            bool open = gm.ExitUnlocked;
            if (open != wasOpen) { SetStatus(open); wasOpen = open; }

            Vector3 target = open ? closedPos + Vector3.up * openHeight : closedPos;
            transform.position = Vector3.MoveTowards(transform.position, target, openSpeed * Time.deltaTime);
        }

        void SetStatus(bool open)
        {
            Color c = open ? Unlocked : Locked;
            if (statusLight != null) statusLight.color = c;
            if (statusMat != null) statusMat.SetColor("_EmissionColor", c * 4f);
        }

        void OnDestroy()
        {
            if (statusMat != null) Destroy(statusMat);
        }
    }
}
