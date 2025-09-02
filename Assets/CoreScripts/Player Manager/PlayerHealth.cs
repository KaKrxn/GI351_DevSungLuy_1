using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Player HP")]
    public int maxHealth = 3;
    [SerializeField]private int currentHealth;

    [Header("Invincibility")]
    public float invincibleTime = 2f;   
    [SerializeField]private bool isInvincible = false;
    [SerializeField]private float invincibleTimer = 0f;

    [Header("Events")]
    public UnityEvent onPlayerDamaged;
    public UnityEvent onPlayerDead;
    public UnityEvent onInvincibilityStart;
    public UnityEvent onInvincibilityEnd;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isInvincible)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0f)
            {
                isInvincible = false;
                onInvincibilityEnd?.Invoke();
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isInvincible) return; //ถ้าอมตะอยู่ return
        currentHealth -= damage;
        onPlayerDamaged?.Invoke();

        if (!isInvincible) 
        {
            //เข้าโหมดอมตะ
            isInvincible = true;
            invincibleTimer = invincibleTime;
            onInvincibilityStart?.Invoke();
        }
            

        Debug.Log("Player HP == " + currentHealth);

        if (currentHealth <= 0)
        {   
            currentHealth = 0;
            onPlayerDead?.Invoke();
            Debug.Log("Player is captured eiei");
            /*
            #if UNITY_EDITOR
            EditorApplication.isPlaying = false;  //ปิด Play Mode naka
            #endif
            */
        }
        /*
        else
        {
            //เข้าโหมดอมตะ
            isInvincible = true;
            invincibleTimer = invincibleTime;
            onInvincibilityStart?.Invoke();
        }
        */
    }
    public void TextStart() //test event
    {
       Debug.Log($"อมตะอยู่จ้า");
    }

    public void TextEnd() //test 
    {
       Debug.Log($"ไม่อมตะละจ้า");
    }

    public int GetHealth()
    {
        return currentHealth;
    }
}
