// GameTimer.cs — with GameOver Panel + Restart button
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;        // << สำหรับ Button
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Header("References")]
    public DeliveryPointManager delivery;   // ลากมาหรือปล่อยให้ค้นหาเอง

    [Header("Timer")]
    public float startSeconds = 60f;
    public bool autoStart = true;
    public bool useUnscaledTime = false;

    [Header("Bonus Time (on each delivery)")]
    public bool randomBonus = true;             // ✅ เปิดสุ่มเวลา
    public float fixedBonus = 15f;              // ใช้เมื่อติด randomBonus = false
    public Vector2 bonusRange = new Vector2(10f, 20f); // ช่วงสุ่ม (วินาที)
    public bool roundBonusToInt = true;         // ปัดเป็นวินาทีเต็ม
    public float capTimeAt = -1f;               // >0 = เพดานเวลาสูงสุด, <=0 = ไม่จำกัด

    [Header("UI (TMP)")]
    public TMP_Text timeLabel;
    public string prefix = " ";
    public string suffix = " ";

    [Header("Game Over UI")]
    public GameObject gameOverPanel;      // ใส่ Panel ที่จะโชว์ตอน Game Over
    public Button restartButton;           // ปุ่ม "Play Again"
    public bool pauseOnGameOver = true;    // หยุดเวลาเมื่อจบ (Time.timeScale = 0)
    public bool showCursorOnGameOver = true; // โชว์เมาส์เมื่อ Game Over
    [Tooltip("เว้นว่าง = โหลดซีนปัจจุบันใหม่")]
    public string reloadSceneName = "";

    [Header("Events")]
    public UnityEvent onGameOver;          // ยังเรียกเหมือนเดิม

    float timeLeft;
    bool running;
    bool isOver;

    void Awake()
    {
        timeLeft = Mathf.Max(0f, startSeconds);
        if (!timeLabel) timeLabel = GetComponent<TMP_Text>();
        if (!delivery)
        {
            delivery = FindFirstObjectByType<DeliveryPointManager>();
            if (!delivery) delivery = FindObjectOfType<DeliveryPointManager>();
        }

        // เตรียม UI Game Over
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (restartButton)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(Restart);
        }

        Render();
    }

    void OnEnable() { if (delivery) delivery.onDelivered.AddListener(OnDelivered); }
    void OnDisable() { if (delivery) delivery.onDelivered.RemoveListener(OnDelivered); }

    void Start() { running = autoStart; }

    void Update()
    {
        if (!running || isOver) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        timeLeft -= dt;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            Render();
            HandleGameOver();    // << จบเกมที่นี่
            return;
        }

        Render();
    }

    void OnDelivered()
    {
        if (isOver) return;

        float bonus = randomBonus
            ? Random.Range(bonusRange.x, bonusRange.y)
            : fixedBonus;

        if (roundBonusToInt) bonus = Mathf.Round(bonus);

        timeLeft += bonus;
        if (capTimeAt > 0f) timeLeft = Mathf.Min(timeLeft, capTimeAt);

        Render();
        // Debug.Log($"[Timer] +{bonus:0}s -> {timeLeft:0.0}s");
    }

    void Render()
    {
        if (!timeLabel) return;
        int t = Mathf.Max(0, Mathf.FloorToInt(timeLeft));
        int m = t / 60;
        int s = t % 60;
        timeLabel.text = $"{prefix}{m:00}:{s:00}{suffix}";
    }

    // ===== Game Over handling =====
    void HandleGameOver()
    {
        if (isOver) return;
        isOver = true;
        running = false;

        // แจ้งระบบอื่น ๆ (เช่นหยุดนับสกอร์)
        onGameOver?.Invoke();
        delivery?.SetGameOver();

        // หยุดเวลา + เปิด UI
        //if (pauseOnGameOver) Time.timeScale = 0f;
        if (gameOverPanel) gameOverPanel.SetActive(true);
        if (showCursorOnGameOver)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void Restart()
    {
        // คืนเวลา แล้วโหลดซีนใหม่
        if (pauseOnGameOver) Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(reloadSceneName))
            SceneManager.LoadScene(reloadSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // API เสริม
    public void StartTimer() { running = true; }
    public void StopTimer() { running = false; }
    public void ResetTimer(float seconds = -1f)
    {
        timeLeft = seconds >= 0 ? seconds : startSeconds;
        isOver = false;
        Render();
    }
    public float GetTimeLeft() => timeLeft;
    public bool IsOver() => isOver;

    void OnValidate()
    {
        if (bonusRange.y < bonusRange.x) bonusRange.y = bonusRange.x;
        if (fixedBonus < 0f) fixedBonus = 0f;
        if (startSeconds < 0f) startSeconds = 0f;
    }
}
