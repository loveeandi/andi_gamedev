using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private int currentScore = 0;
    private int totalTargets = 0;
    private int knockedDownCount = 0;

    void OnEnable()
    {
        Target.OnTargetKnockedDown += HandleTargetKnockedDown;
    }

    void OnDisable()
    {
        Target.OnTargetKnockedDown -= HandleTargetKnockedDown;
    }

    void Start()
    {
        // Automatically count how many targets exist in the level at start
        Target[] targetsInScene = FindObjectsByType<Target>(FindObjectsSortMode.None);
        totalTargets = targetsInScene.Length;
        Debug.Log($"[ScoreManager] Level started with {totalTargets} targets.");
    }

    private void HandleTargetKnockedDown(int points)
    {
        currentScore += points;
        knockedDownCount++;

        Debug.Log($"Target Knocked Down! +{points} pts | Total Score: {currentScore} | Progress: {knockedDownCount}/{totalTargets}");

        if (knockedDownCount >= totalTargets)
        {
            Debug.Log("ALL TARGETS KNOCKED DOWN! Level Complete!");
        }
    }
}