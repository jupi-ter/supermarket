using Unity.Netcode;
using UnityEngine;

public class GroceryManager : NetworkBehaviour
{
    [SerializeField] private GameObject[] groceryPrefabs;
    [SerializeField] private int maxGroceries = 20;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnInterval = 5f;

    private float nextSpawnTime;
    private float spawnCount = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Initial spawn
        for (int i = 0; i < maxGroceries / 2; i++)
        {
            SpawnGrocery();
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time >= nextSpawnTime && spawnCount < maxGroceries)
        {
            SpawnGrocery();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void SpawnGrocery()
    {
        if (groceryPrefabs.Length == 0) return;

        // Select random prefab
        var prefab = groceryPrefabs[Random.Range(0, groceryPrefabs.Length)];

        // Get position in a circle around the manager
        Vector3 spawnPos = transform.position + Random.insideUnitSphere * spawnRadius;
        spawnPos.y = 0.5f; // Adjust height as needed

        // Spawn the network object
        var groceryObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        var networkObj = groceryObj.GetComponent<NetworkObject>();
        networkObj.Spawn(true); // Spawn with scene object persistence
        spawnCount++;
    }
}