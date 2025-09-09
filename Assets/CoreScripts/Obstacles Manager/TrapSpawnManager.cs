using System.Collections;
using UnityEngine;

public class TrapSpawnManager : MonoBehaviour
{
    [Header("ตั้งค่า Trap Prefabs")]
    public GameObject[] trapPrefabs; // Array ของ Prefab ให้กำหนดจาก Inspector

    [Header("ตำแหน่ง Spawn")]
    public Transform[] spawnPositions; // Array ของตำแหน่ง Spawn

    [Header("ตั้งค่าเวลา")]
    public float spawnInterval = 5f; // ทุกๆกี่วินาทีจะ Spawn ใหม่
    public float trapLifetime = 10f; // Spawn แล้วอยู่กี่วิจะหายไป

    [Header("Rotation ตอน Spawn")]
    public float rotationX = -90f;
    public float rotationY = 50f;
    public float rotationZ = 0f;

    private void Start()
    {
        StartCoroutine(SpawnTrapLoop());
    }

    IEnumerator SpawnTrapLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval); // รอจนถึงเวลาสุ่ม
            SpawnTrap();
        }
    }

    void SpawnTrap()
    {
        if (trapPrefabs.Length == 0 || spawnPositions.Length == 0) return;

        // สุ่มเลือก Trap
        int randomTrapIndex = Random.Range(0, trapPrefabs.Length);

        // สุ่มเลือกตำแหน่ง
        int randomPosIndex = Random.Range(0, spawnPositions.Length);

        // Rotation ที่กำหนดเอง
        Quaternion customRotation = Quaternion.Euler(rotationX, rotationY, rotationZ);

        // สร้าง Trap ที่ตำแหน่งที่สุ่มได้ พร้อม Rotation
        GameObject spawnedTrap = Instantiate(trapPrefabs[randomTrapIndex], spawnPositions[randomPosIndex].position, customRotation);

        // ทำลายหลังเวลาที่กำหนด
        Destroy(spawnedTrap, trapLifetime);
    }
}
