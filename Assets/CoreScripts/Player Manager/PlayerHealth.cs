using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Player HP")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Events")]
    public UnityEvent onPlayerDamaged;
    public UnityEvent onPlayerDead;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        onPlayerDamaged?.Invoke();

        Debug.Log("Player HP == " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            onPlayerDead?.Invoke();
            Debug.Log("Player is captured eiei");

            #if UNITY_EDITOR
            EditorApplication.isPlaying = false;  //ปิด Play Mode naka
            #endif
        }
    }

    public int GetHealth()
    {
        return currentHealth;
    }
}
