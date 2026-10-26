using UnityEngine;

namespace LaserLab
{
    public enum BonusType { Heal, Shield, Speed }

    /// <summary>
    /// Glowing floor panel. Step on it (detected with Physics.CheckBox) to get a bonus.
    /// Green = heal, Blue = laser shield, Yellow = adrenaline (speed).
    /// The panel goes dark and recharges after a cooldown.
    /// </summary>
    public class BonusPanel : MonoBehaviour
    {
        public BonusType type;
        public float healAmount = 35f;
        public float shieldDuration = 6f;
        public float speedMultiplier = 1.6f;
        public float speedDuration = 6f;
        public float cooldown = 12f;

        Renderer rend;
        Light glow;
        Material instanceMat;
        Color baseEmission;
        float cooldownTimer;

        void Start()
        {
            rend = GetComponent<Renderer>();
            glow = GetComponentInChildren<Light>();
            instanceMat = rend.material; // per-panel copy so dimming one doesn't dim all
            baseEmission = instanceMat.GetColor("_EmissionColor");
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f) SetLit(true);
                return;
            }

            Vector3 half = new Vector3(transform.lossyScale.x * 0.5f, 0.5f, transform.lossyScale.z * 0.5f);
            if (Physics.CheckBox(transform.position + Vector3.up * 0.5f, half, transform.rotation,
                                 gm.PlayerMask, QueryTriggerInteraction.Ignore))
            {
                Apply(gm);
            }
        }

        void Apply(GameManager gm)
        {
            switch (type)
            {
                case BonusType.Heal:
                    if (!gm.Health.Heal(healAmount)) return; // full health: don't waste it
                    gm.ShowMessage($"FIRST AID  +{healAmount:0} HEALTH", 2f);
                    break;
                case BonusType.Shield:
                    gm.Health.AddShield(shieldDuration);
                    gm.ShowMessage($"LASER SHIELD  {shieldDuration:0}s", 2f);
                    break;
                case BonusType.Speed:
                    gm.ApplySpeedBoost(speedMultiplier, speedDuration);
                    gm.ShowMessage($"ADRENALINE  {speedDuration:0}s", 2f);
                    break;
            }

            cooldownTimer = cooldown;
            SetLit(false);
        }

        void SetLit(bool lit)
        {
            instanceMat.SetColor("_EmissionColor", lit ? baseEmission : baseEmission * 0.05f);
            if (glow != null) glow.enabled = lit;
        }

        void OnDestroy()
        {
            if (instanceMat != null) Destroy(instanceMat);
        }
    }
}
