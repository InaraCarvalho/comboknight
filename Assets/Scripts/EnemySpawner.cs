using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject slimePrefab;
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private float groundSpawnY = -3.95f;
    [SerializeField] private float minBatY = -2.0f;
    [SerializeField] private float maxBatY = 2.5f;
    [SerializeField] private float spawnDistanceX = 4.3f;

    private float slimeTimer = 1.8f;
    private float batTimer = 2.2f;

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) return;

        float diff = GameManager.Instance.DifficultyMultiplier;

        slimeTimer += Time.deltaTime;
        float slimeInterval = Mathf.Max(0.9f, 2.3f / diff);
        if (slimeTimer >= slimeInterval)
        {
            slimeTimer = 0f;
            if (slimePrefab != null)
            {
                float side = (Random.value < 0.5f) ? -spawnDistanceX : spawnDistanceX;
                Instantiate(slimePrefab, new Vector3(side, groundSpawnY, 0f), Quaternion.identity);
            }
        }

        batTimer += Time.deltaTime;
        float batInterval = Mathf.Max(1.4f, 3.4f / diff);
        if (batTimer >= batInterval)
        {
            batTimer = 0f;
            if (batPrefab != null)
            {
                float side = (Random.value < 0.5f) ? -spawnDistanceX : spawnDistanceX;
                float flightY = Random.Range(minBatY, maxBatY);
                Instantiate(batPrefab, new Vector3(side, flightY, 0f), Quaternion.identity);
            }
        }
    }
}
