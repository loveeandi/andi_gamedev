using System.Collections;
using UnityEngine;

public class LaserSpawner : MonoBehaviour
{
    [Header("Custom Patterns")]
    [SerializeField] private GameObject[] laserPatternPrefabs; 
    [SerializeField] private float spawnInterval = 2.0f;
    [SerializeField] private float laserSpeed = 8.0f;
    [SerializeField] private float spawnXPosition = 5.0f;

    private void Start()
    {
        if (laserPatternPrefabs != null && laserPatternPrefabs.Length > 0)
        {
            StartCoroutine(SpawnPatternWaves());
        }
        else
        {
            Debug.LogWarning("No laser pattern prefabs assigned to the spawner!");
        }
    }

    private IEnumerator SpawnPatternWaves()
    {
        while (true)
        {
            SpawnRandomPattern();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnRandomPattern()
    {
        int randomIndex = Random.Range(0, laserPatternPrefabs.Length);
        GameObject selectedPatternPrefab = laserPatternPrefabs[randomIndex];

        Vector3 spawnPos = new Vector3(spawnXPosition, 0f, 0f);
        GameObject spawnedPattern = Instantiate(selectedPatternPrefab, spawnPos, Quaternion.identity);

        MovingPattern mover = spawnedPattern.GetComponent<MovingPattern>();
        if (mover == null)
        {
            mover = spawnedPattern.AddComponent<MovingPattern>();
        }
        mover.SetSpeed(laserSpeed);
    }
}