using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameObject policePrefab;     // Prefab ของตำรวจ
    public Transform[] spawnPoints;     // จุดเกิดตำรวจ
    public Transform[] patrolPoints;    // จุดเดินตรวจ
    public Transform target;            // Player

    private int startPoliceCount = 2;   // จำนวนเริ่มต้น
    public float spawnInterval = 30f;   // spawn ทุกๆ

    void Start()
    {
        for (int i = 0; i < startPoliceCount; i++)
        {
            SpawnPolice();
        }

        StartCoroutine(SpawnRoutine());
    }

    void SpawnPolice()
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject police = Instantiate(policePrefab, spawnPoint.position, spawnPoint.rotation);

        PoliceController pc = police.GetComponent<PoliceController>();
        if (pc != null)
        {
            pc.SetPatrolPoints(patrolPoints);
            pc.target = target;  
        }
        else
        {
            Debug.Log("Prefab ไม่มี PoliceController");
        }
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnPolice();
        }
    }
}
