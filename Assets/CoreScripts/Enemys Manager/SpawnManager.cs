using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameObject policePrefab;         // Prefab ของ Police
    public Transform[] spawnPoints;         // จุดที่ spawn ได้
    public Transform[] patrolPoints;        // จุด Patrol
    public Transform target;                // Player หรือเป้าหมาย

    public int startPoliceCount = 2;        // เริ่มต้นสร้างกี่ตัว
    public float spawnInterval = 30f;       // ทุกกี่วิ spawn เพิ่ม
    private int currentPoliceCount;

    void Start()
    {
        SpawnPolice();

        currentPoliceCount = startPoliceCount;

        // เริ่ม Coroutine สำหรับ Spawn เพิ่มทุก 30 วิ
        StartCoroutine(SpawnRoutine());
    }

    void SpawnPolice()
    {
        // สุ่มตำแหน่ง spawn
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        
        GameObject police = Instantiate(policePrefab, spawnPoint.position, spawnPoint.rotation);

        
        PoliceController pc = police.GetComponent<PoliceController>();
        if (pc != null)
        {
            pc.patrolPoints = patrolPoints;
            pc.target = target;
        }
        else
        {
            Debug.LogError("Prefab ไม่มี PoliceController ");
        }
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
          
            SpawnPolice();
            currentPoliceCount++;
        }
    }
}
