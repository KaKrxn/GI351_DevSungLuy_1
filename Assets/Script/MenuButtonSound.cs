using UnityEngine;
using UnityEngine.UI;

public class MenuButtonSound : MonoBehaviour
{
    public AudioSource clickSound; // ใส่เสียงกดปุ่ม

    void Start()
    {
        // หา button ทั้งหมดในลูก object นี้
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

