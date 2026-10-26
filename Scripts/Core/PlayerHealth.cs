using UnityEngine;

namespace LaserLab
{
    /// <summary>Player health, plus the temporary shield granted by the blue floor panels.</summary>
    public class PlayerHealth : MonoBehaviour
    {
        public float maxHealth = 100f;

        public float Current { get; private set; }
        public float ShieldTimer { get; private set; }
        public float DamageFlash { get; private set; }
        public bool HasShield => ShieldTimer > 0f;

        void Awake() => Current = maxHealth;

        void Update()
        {
            if (ShieldTimer > 0f) ShieldTimer -= Time.deltaTime;
            if (DamageFlash > 0f) DamageFlash -= Time.deltaTime * 2f;
        }

        /// <returns>True if damage was actually applied.</returns>
        public bool TakeDamage(float amount)
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return false;

            if (HasShield)
            {
                gm?.ShowMessage("SHIELD ABSORBED THE HIT", 1.2f);
                return false;
            }

            Current = Mathf.Max(0f, Current - amount);
            DamageFlash = 1f;

            if (Current <= 0f) gm?.PlayerDied();
            return true;
        }

        /// <returns>True if any health was restored.</returns>
        public bool Heal(float amount)
        {
            if (Current >= maxHealth) return false;
            Current = Mathf.Min(maxHealth, Current + amount);
            return true;
        }

        public void AddShield(float duration) => ShieldTimer = Mathf.Max(ShieldTimer, duration);
    }
}
