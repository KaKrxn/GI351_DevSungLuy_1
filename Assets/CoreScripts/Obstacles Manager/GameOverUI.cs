using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button playAgainButton;
    public Button backButton;

    [Header("Scenes")]
    [Tooltip("ชื่อซีนหน้า Home ตาม Build Settings (เช่น \"HOME\")")]
    public string homeSceneName = "HOME";

    [Header("Behaviour")]
    [Tooltip("เปิดเมนูแล้วหยุดเวลา")]
    public bool freezeTimeOnShow = true;
    [Tooltip("ซ่อนเมนู/เปลี่ยนซีนแล้วปล่อยเวลา")]
    public bool unfreezeOnHideOrLoad = true;
    [Tooltip("โฟกัสปุ่มแรกอัตโนมัติ (สำหรับคีย์บอร์ด/จอย)")]
    public bool autoSelectFirstButton = true;
    public bool showCursor = true;

    void OnEnable()
    {
        //if (freezeTimeOnShow) Time.timeScale = 0f;

        if (showCursor) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }

        if (autoSelectFirstButton && playAgainButton && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(playAgainButton.gameObject);
    }

    void OnDisable()
    {
        if (unfreezeOnHideOrLoad) Time.timeScale = 1f;
    }

    // ===== Buttons =====
    public void OnPlayAgain()
    {
        if (unfreezeOnHideOrLoad) Time.timeScale = 1f;
        var cur = SceneManager.GetActiveScene();
        SceneManager.LoadScene(cur.buildIndex);
    }

    public void OnBackHome()
    {
        if (unfreezeOnHideOrLoad) Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(homeSceneName))
            SceneManager.LoadScene(homeSceneName);
        else
            Debug.LogError("[GameOverUI] homeSceneName is empty.");
    }
}
