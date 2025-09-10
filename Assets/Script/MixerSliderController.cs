using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class MixerSliderController : MonoBehaviour
{
    [Header("Audio Mixer")]
    public AudioMixer mixer;

    [Header("UI Sliders")]
    public Slider musicSlider;
    public Slider vfxSlider;

    void Start()
    {
        // ���� listener ��� Slider
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        vfxSlider.onValueChanged.AddListener(SetVFXVolume);

        // ��駤�� Slider ����������ç�Ѻ Mixer
        InitializeSlider(musicSlider, "MusicVolume");
        InitializeSlider(vfxSlider, "VFXVolume");
    }

    // �ѧ��ѹ��Ѻ��� Slider �������
    void InitializeSlider(Slider slider, string parameter)
    {
        if (slider == null || mixer == null) return;

        float value;
        if (mixer.GetFloat(parameter, out value))
        {
            slider.value = Mathf.Pow(10, value / 20);
        }
    }

    // ��Ѻ���§ Music
    void SetMusicVolume(float value)
    {
        mixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20);
    }

    // ��Ѻ���§ VFX (�ء AudioSource �������� VFX Group)
    void SetVFXVolume(float value)
    {
        mixer.SetFloat("VFXVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20);
    }
}
