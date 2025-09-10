using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    // �Դ Settings
    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // ��Ѻ� MainMenu
    public void BackToMenu()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // �Դ��
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Game Quit"); // �����鷴�ͺ� Editor
    }
}