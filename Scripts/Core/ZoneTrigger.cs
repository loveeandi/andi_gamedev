using UnityEngine;

namespace LaserLab
{
    public enum ZoneType { LabEntrance, LockedDoorHint, Exit }

    /// <summary>
    /// Box-shaped area that reacts when the player stands in it.
    /// Uses Physics.CheckBox instead of trigger colliders.
    /// </summary>
    public class ZoneTrigger : MonoBehaviour
    {
        public ZoneType type;
        public Vector3 size = new Vector3(3f, 2.4f, 2f);

        float messageCooldown;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;
            if (messageCooldown > 0f) messageCooldown -= Time.deltaTime;

            bool inside = Physics.CheckBox(transform.position, size * 0.5f, transform.rotation,
                                           gm.PlayerMask, QueryTriggerInteraction.Ignore);
            if (!inside) return;

            switch (type)
            {
                case ZoneType.LabEntrance:
                    if (gm.State == GameState.Corridor) gm.EnterLab();
                    break;

                case ZoneType.LockedDoorHint:
                    if (!gm.ExitUnlocked && messageCooldown <= 0f)
                    {
                        gm.ShowMessage($"EMERGENCY EXIT SEALED\nKeycards: {gm.KeycardsCollected}/{gm.keycardsRequired}", 2.5f);
                        messageCooldown = 3f;
                    }
                    break;

                case ZoneType.Exit:
                    if (gm.ExitUnlocked) gm.Win();
                    break;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = type == ZoneType.Exit ? Color.green : type == ZoneType.LabEntrance ? Color.cyan : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
