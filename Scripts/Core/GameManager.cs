using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

namespace LaserLab
{
    public enum GameState { Corridor, Lab, Dead, Won }

    /// <summary>
    /// Central game state: corridor -> lab -> escape.
    /// Handles death (full restart), detection (back to lab entrance), keycards and buffs.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Where the player is sent back to when detected inside the lab.")]
        public Transform labRespawnPoint;

        [Header("Rules")]
        public int keycardsRequired = 3;
        [Tooltip("Seconds of immunity after being caught, so you can't be caught twice in a row.")]
        public float caughtGraceTime = 2.5f;
        [Tooltip("Seconds the death screen is shown before the game restarts at the corridor start.")]
        public float deathRestartDelay = 3f;
        [Range(0.2f, 1f)] public float crouchSpeedMultiplier = 0.5f;

        public GameState State { get; private set; } = GameState.Corridor;
        public bool IsPlaying => State == GameState.Corridor || State == GameState.Lab;
        public int KeycardsCollected { get; private set; }
        public bool ExitUnlocked => KeycardsCollected >= keycardsRequired;
        public int TimesCaught { get; private set; }
        public float ElapsedTime { get; private set; }
        public bool InGrace => graceTimer > 0f;

        public string Message { get; private set; } = "";
        public float MessageTimer { get; private set; }
        public float CaughtFlash { get; private set; }
        public float SpeedBoostTimer { get; private set; }

        public Transform Player { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerCrouch Crouch { get; private set; }
        public FirstPersonController Controller { get; private set; }
        public StarterAssetsInputs Inputs { get; private set; }
        public int PlayerMask { get; private set; }

        CharacterController characterController;
        float baseMoveSpeed, baseSprintSpeed, speedMultiplier = 1f, graceTimer;

        public bool IsCrouching => Crouch != null && Crouch.IsCrouching;
        public bool IsSprinting => Inputs != null && Inputs.sprint && Inputs.move.sqrMagnitude > 0.01f && !IsCrouching;

        /// <summary>Centre of the player's capsule (follows crouching).</summary>
        public Vector3 PlayerCenter => Player.position + characterController.center;
        /// <summary>Approximate head position of the player (follows crouching).</summary>
        public Vector3 PlayerHead => Player.position + Vector3.up * (characterController.center.y + characterController.height * 0.5f - 0.15f);

        void Awake()
        {
            Instance = this;

            Controller = FindFirstObjectByType<FirstPersonController>();
            if (Controller == null)
            {
                Debug.LogError("[LaserLab] No FirstPersonController found in the scene. Rebuild the scene with Tools > Laser Lab > Build Scene.");
                enabled = false;
                return;
            }

            Player = Controller.transform;
            Health = Player.GetComponent<PlayerHealth>();
            Crouch = Player.GetComponent<PlayerCrouch>();
            Inputs = Player.GetComponent<StarterAssetsInputs>();
            characterController = Player.GetComponent<CharacterController>();

            PlayerMask = LayerMask.GetMask("Player");
            if (PlayerMask == 0)
                Debug.LogError("[LaserLab] The 'Player' layer does not exist. Run Tools > Laser Lab > Build Scene to create it.");

            baseMoveSpeed = Controller.MoveSpeed;
            baseSprintSpeed = Controller.SprintSpeed;
        }

        void Start()
        {
            ShowMessage("SECURITY CORRIDOR A-1\nReach the lab at the end of the corridor. Avoid the lasers.", 6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (MessageTimer > 0f) MessageTimer -= dt;
            if (graceTimer > 0f) graceTimer -= dt;
            if (CaughtFlash > 0f) CaughtFlash -= dt * 1.5f;
            if (IsPlaying) ElapsedTime += dt;

            // Movement speed = base x adrenaline buff x crouch
            if (SpeedBoostTimer > 0f)
            {
                SpeedBoostTimer -= dt;
                if (SpeedBoostTimer <= 0f) speedMultiplier = 1f;
            }
            if (Controller != null)
            {
                float crouchMul = IsCrouching ? crouchSpeedMultiplier : 1f;
                Controller.MoveSpeed = baseMoveSpeed * speedMultiplier * crouchMul;
                Controller.SprintSpeed = IsCrouching ? Controller.MoveSpeed : baseSprintSpeed * speedMultiplier;
            }

            if (State == GameState.Won && InputUtil.RestartPressed())
                RestartGame();
        }

        // ---------------- Messages ----------------
        public void ShowMessage(string text, float duration = 3f)
        {
            Message = text;
            MessageTimer = duration;
        }

        // ---------------- Corridor ----------------
        public void ApplySpeedBoost(float multiplier, float duration)
        {
            speedMultiplier = multiplier;
            SpeedBoostTimer = duration;
        }

        public void PlayerDied()
        {
            if (!IsPlaying) return;
            State = GameState.Dead;
            Controller.enabled = false;
            ShowMessage("", 0f);
            StartCoroutine(RestartAfter(deathRestartDelay));
        }

        IEnumerator RestartAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            RestartGame();
        }

        /// <summary>Reloads the scene: full health, back at the start of the corridor.</summary>
        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ---------------- Lab / stealth ----------------
        public void EnterLab()
        {
            if (State != GameState.Corridor) return;
            State = GameState.Lab;

            var spawner = FindFirstObjectByType<LaserSpawner>();
            if (spawner != null) spawner.ClearAll();

            ShowMessage("B1 BIOHAZARD LAB\nStay out of sight. Find 3 hidden keycards with your flashlight [F].", 7f);
        }

        /// <summary>Called by any detector or hazard when the player is spotted in the lab.</summary>
        public void Caught(string reason)
        {
            if (State != GameState.Lab || graceTimer > 0f) return;

            TimesCaught++;
            graceTimer = caughtGraceTime;
            CaughtFlash = 1f;

            if (Crouch != null) Crouch.ForceStand();
            Teleport(labRespawnPoint);
            Detector.ResetAll();

            ShowMessage($"DETECTED by {reason}\nReturned to the lab entrance.", 3.5f);
        }

        public void AddKeycard()
        {
            KeycardsCollected++;
            if (ExitUnlocked)
                ShowMessage("All keycards recovered.\nThe emergency exit is now UNLOCKED.", 5f);
            else
                ShowMessage($"Keycard recovered ({KeycardsCollected}/{keycardsRequired})", 3f);
        }

        public void Win()
        {
            if (State != GameState.Lab) return;
            State = GameState.Won;
            Controller.enabled = false;
            ShowMessage("", 0f);
        }

        void Teleport(Transform target)
        {
            if (target == null) return;
            characterController.enabled = false;
            Player.SetPositionAndRotation(target.position, target.rotation);
            characterController.enabled = true;
        }
    }
}
