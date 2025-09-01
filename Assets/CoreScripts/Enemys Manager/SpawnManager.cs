using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Police Settings")]
    public GameObject policePrefab;       // Prefab ของ Police
    public Transform[] spawnPoints;       // จุด Spawn ที่ตั้งไว้ใน Inspector

    [Header("Spawn Timing")]
    public float spawnInterval = 30f;     // ทุกๆ 30 วิ Spawn เพิ่ม
    private int initialPolice = 2;        // เริ่มเกม Spawn 2 ตัว

    public void Start()
    {
        SpawnPolice();

        StartCoroutine(SpawnPoliceByTime());
    }

    public void SpawnPolice()
    {
        if (policePrefab == null || spawnPoints.Length == 0)
        {
            Debug.Log("forgot prefab and spawn point naja jub jub");           
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Instantiate(policePrefab, spawnPoint.position, spawnPoint.rotation);
    }

    public IEnumerator SpawnPoliceByTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnPolice();
        }
    }
}