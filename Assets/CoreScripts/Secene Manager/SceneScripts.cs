using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneScripts : MonoBehaviour
{
    // �ѧ��������Ѻ�����Ŵ�չ�������
    public void LoadStartScene()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("Home");
    }
}
