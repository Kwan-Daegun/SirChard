using UnityEngine;

public partial class TrapSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject trapPrefab;
    public int numberOfTraps = 10;

    [Header("Area Settings")]
    public Vector3 spawnAreaCenter;
    public Vector3 spawnAreaSize; // Defines the width/length of the area

    void Start()
    {
        SpawnTraps();
    }

    void SpawnTraps()
    {
        for (int i = 0; i < numberOfTraps; i++)
        {
            // Calculate a random position within the defined box
            Vector3 randomPos = new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                0, // Keep Y at 0 or your ground level
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            // Add the center offset
            Vector3 finalPosition = spawnAreaCenter + randomPos;

            // Instantiate the trap
            Instantiate(trapPrefab, finalPosition, Quaternion.identity);
        }
    }

    // This helps you see the spawn area in the Scene View
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
    }
}