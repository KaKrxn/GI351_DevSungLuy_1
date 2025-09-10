using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneScripts : MonoBehaviour
{
    // ฟังก์ชั่นสำหรับการโหลดซีนตามชื่อ
    public void LoadStartScene()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("Home");
    }
}
