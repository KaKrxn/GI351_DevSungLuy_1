using UnityEngine;
using UnityEngine.UI;

public class MenuButtonSound : MonoBehaviour
{
    public AudioSource clickSound; // ������§������

    void Start()
    {
        // �� button ��������١ object ���
        Button[] buttons = GetComponentsInChildren<Button>();

        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(PlayClickSound);
        }
    }

    void PlayClickSound()
    {
        clickSound.Play();
    }
}

