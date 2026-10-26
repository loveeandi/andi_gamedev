using UnityEngine;

namespace LaserLab
{
    /// <summary>
    /// Adds crouching to the Starter Assets First Person Controller.
    /// Crouching shrinks the CharacterController, lowers the camera, slows you down
    /// and makes you harder to see (so lab benches become cover).
    /// Standing up uses Physics.CheckCapsule to make sure there is headroom.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCrouch : MonoBehaviour
    {
        public Transform cameraRoot;
        public float crouchHeight = 1.0f;
        public float crouchCameraHeight = 0.75f;
        public float transitionSpeed = 6f;

        public bool IsCrouching { get; private set; }

        CharacterController cc;
        float standHeight, standCameraHeight, currentHeight;
        Vector3 standCenter;
        int obstacleMask;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            standHeight = cc.height;
            standCenter = cc.center;
            currentHeight = standHeight;
            if (cameraRoot != null) standCameraHeight = cameraRoot.localPosition.y;
            obstacleMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            if (InputUtil.CrouchPressed())
            {
                if (!IsCrouching) IsCrouching = true;
                else if (CanStand()) IsCrouching = false;
                else gm?.ShowMessage("Not enough room to stand up", 1.2f);
            }

            float target = IsCrouching ? crouchHeight : standHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, target, transitionSpeed * Time.deltaTime);
            ApplyHeight();
        }

        void ApplyHeight()
        {
            cc.height = currentHeight;
            cc.center = new Vector3(standCenter.x, standCenter.y - (standHeight - currentHeight) * 0.5f, standCenter.z);

            if (cameraRoot != null)
            {
                float t = Mathf.InverseLerp(crouchHeight, standHeight, currentHeight);
                Vector3 p = cameraRoot.localPosition;
                p.y = Mathf.Lerp(crouchCameraHeight, standCameraHeight, t);
                cameraRoot.localPosition = p;
            }
        }

        /// <summary>CheckCapsule: is the space above our head free?</summary>
        bool CanStand()
        {
            float r = cc.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (crouchHeight + r * 0.5f);
            Vector3 top = transform.position + Vector3.up * (standHeight - r);
            return !Physics.CheckCapsule(bottom, top, r, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Used when the player is teleported back to a checkpoint.</summary>
        public void ForceStand()
        {
            IsCrouching = false;
            currentHeight = standHeight;
            ApplyHeight();
        }
    }
}
