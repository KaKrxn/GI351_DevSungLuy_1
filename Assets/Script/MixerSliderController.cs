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
        // เพิ่ม listener ให้ Slider
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        vfxSlider.onValueChanged.AddListener(SetVFXVolume);

        // ตั้งค่า Slider เริ่มต้นให้ตรงกับ Mixer
        InitializeSlider(musicSlider, "MusicVolume");
        InitializeSlider(vfxSlider, "VFXVolume");
    }

    // ฟังก์ชันปรับค่า Slider เริ่มต้น
    void InitializeSlider(Slider slider, string parameter)
    {
        if (slider == null || mixer == null) return;

        float value;
        if (mixer.GetFloat(parameter, out value))
        {
            slider.value = Mathf.Pow(10, value / 20);
        }
    }

    // ปรับเสียง Music
    void SetMusicVolume(float value)
    {
        mixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20);
    }

    // ปรับเสียง VFX (ทุก AudioSource ที่อยู่ใน VFX Group)
    void SetVFXVolume(float value)
    {
        mixer.SetFloat("VFXVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20);
    }
}
