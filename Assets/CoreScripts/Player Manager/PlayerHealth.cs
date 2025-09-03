using UnityEngine;
using UnityEngine.Events;


public class PlayerHealth : MonoBehaviour
{
    [Header("Player HP")]
    public int maxHealth; //ghggg

    [SerializeField] private int currentHealth;

    [Header("Invincibility")]
    public float invincibleTime = 2f;
    [SerializeField]private bool isInvincible = false;
    [SerializeField] private float invTimer = 0f;

    [Header("Events")]
    public UnityEvent onPlayerDamaged;
    public UnityEvent onPlayerDead;
    public UnityEvent onInvincibilityStart;
    public UnityEvent onInvincibilityEnd;

    void Awake()
    {       
        currentHealth = maxHealth;
        isInvincible = false;
        invTimer = 0f;
        Debug.Log($"HP = {currentHealth}/{maxHealth}");
    }

    void Update()
    {
        if (!isInvincible) return;

        invTimer -= Time.deltaTime;
        if (invTimer <= 0f)
        {
            isInvincible = false;
            onInvincibilityEnd?.Invoke(); 
            Debug.Log("[PlayerHealth] อมตะอยู่จ้า");
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;             //กันไว้เฉยๆ
        if (currentHealth <= 0) return;      //ตายแล้วไม่รับดาเมจ
        if (isInvincible) return;            //อมตะอยู่ไม่รับดาเมจ

        currentHealth = Mathf.Max(0, currentHealth - damage);
        onPlayerDamaged?.Invoke();
        Debug.Log($"Player HP == {currentHealth}");

        if (currentHealth <= 0)
        {
            onPlayerDead?.Invoke();                   
            return;
        }

        // เข้าโหมดอมตะ
        isInvincible = true;
        invTimer = invincibleTime;
        onInvincibilityStart?.Invoke(); 
        Debug.Log("[PlayerHealth] ไม่อมตะเเล้ว");
    }

    public int GetHealth() => currentHealth;
    public bool IsInvincible() => isInvincible;
}

