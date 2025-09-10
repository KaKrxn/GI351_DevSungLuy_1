using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    // เปิด Settings
    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // กลับไป MainMenu
    public void BackToMenu()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // ปิดเกม
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Game Quit"); // เอาไว้ทดสอบใน Editor
    }
}