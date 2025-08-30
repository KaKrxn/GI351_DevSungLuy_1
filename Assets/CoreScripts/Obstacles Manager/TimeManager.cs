using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    //[Header("Text UI")]
    //[SerializeField] TMP_Text TimeCountText;


    //[Header("Format")]

    //float timeCount;
    //float timeCountDownPoint;

    //public void TimeCount()
    //{

    //}
    //private void Start()
    //{
    //    TimeCountText.text = "";
    //}


    //private void Update()
    //{
    //    timeCount = Time.time;

    //    TimeCountText.text = timeCount.ToString();
    //}

    [SerializeField] TMP_Text TimeCountText;   
    [SerializeField] TMP_Text TimeCountDownText;
    [Header("Behavior")]
    public bool autoStart = true;                 
    public bool pauseWhenTimeScaleZero = true;    
    public bool useUnscaledTime = false;          

    [Header("Format")]
    public string prefix = " ";                   
    public string suffix = " ";                   
    public bool showHoursIfNeeded = true;         

    

    float elapsed;                                
    bool running;

    void Awake()
    {
        if (!TimeCountText) TimeCountText = GetComponent<TMP_Text>();
        if (autoStart) StartTimer(); 
        else RenderTime();
    }

    void Update()
    {
        if (!running) return;
        if (pauseWhenTimeScaleZero && Time.timeScale == 0f && !useUnscaledTime) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += dt;
        RenderTime();
    }

    void TimeCountDown()
    {

    }

    void RenderTime()
    {
        if (!TimeCountText) return;
        int total = Mathf.Max(0, Mathf.FloorToInt(elapsed));
        int h = total / 3600;
        int m = (total / 60) % 60;
        int s = total % 60;

        if (showHoursIfNeeded && h > 0)
        { TimeCountText.text = $"{prefix}{h:00}:{m:00}:{s:00}{suffix}"; }
        else
        { TimeCountText.text = $"{prefix}{h:00}:{m:00}:{s:00}{suffix}"; }
    }

    // --- Public API ---
    public void StartTimer() => running = true;
    public void PauseTimer() => running = false;
    public void ResumeTimer() => running = true;
    public void ResetTimer(float startAt = 0f) { elapsed = Mathf.Max(0f, startAt); RenderTime(); }
    public void SetElapsed(float sec) { elapsed = Mathf.Max(0f, sec); RenderTime(); }
    public float GetElapsed() => elapsed;
}
