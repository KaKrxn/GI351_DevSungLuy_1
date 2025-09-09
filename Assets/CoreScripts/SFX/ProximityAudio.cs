using UnityEngine;

/// ปรับความดังของ AudioSource ตามระยะห่างจากผู้เล่น
/// ใช้ได้ทั้งเสียง 2D/3D (ตั้ง Spatial Blend ตามต้องการ)
[RequireComponent(typeof(AudioSource))]
public class ProximityAudio : MonoBehaviour
{
    [Header("Target (Player/Listener)")]
    [Tooltip("ถ้าเว้นว่าง ระบบจะหา Main Camera (AudioListener) หรือ GameObject แท็ก 'Player'")]
    public Transform listener;

    [Header("Mode")]
    [Tooltip("เปิด = ใช้ 3D rolloff ของ AudioSource (Spatial Blend=1) ปรับที่ Min/Max Distance เอง")]
    public bool useAudioSource3DAttenuation = true;

    [Header("Manual Volume (ใช้เมื่อปิด 3D rolloff)")]
    [Tooltip("ระยะที่ใกล้กว่านี้ = ดังสุด")]
    public float minDistance = 5f;
    [Tooltip("ระยะที่ไกลกว่านี้ = เงียบ")]
    public float maxDistance = 60f;
    [Tooltip("โค้งความดัง: t=0 (ใกล้) → 1 (ไกล)")]
    public AnimationCurve volumeByDistance = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("ค่อยๆ ไล่วอลุ่มให้นุ่ม")]
    public float smoothTime = 0.08f;

    [Header("Auto Play/Stop")]
    public bool autoPlayWhenAudible = true;
    public bool autoStopWhenOut = false;

    [Header("Optional: Occlusion (ทึบเสียงหลังสิ่งกีดขวาง)")]
    public bool occlusionByLinecast = false;
    public LayerMask occlusionMask = ~0;
    [Range(0f, 1f)] public float occludedVolumeMul = 0.6f;
    public float occlusionHeightOffset = 1.2f;

    AudioSource src;
    float volVel;

    void Awake()
    {
        src = GetComponent<AudioSource>();

        if (!listener)
        {
            // ลองเอา AudioListener บน Main Camera ก่อน
            if (Camera.main && Camera.main.GetComponent<AudioListener>())
                listener = Camera.main.transform;
            // เผื่อไม่มี ให้หา Player tag
            if (!listener)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) listener = p.transform;
            }
        }

        // ข้อแนะนำโหมด
        if (useAudioSource3DAttenuation)
        {
            // ให้ AudioSource ทำงาน 3D ปกติ
            src.spatialBlend = 1f; // 3D
            // การดัง/เบาตามระยะให้ไปตั้งที่ Min/Max Distance + Rolloff แทน
        }
        else
        {
            // เราจะคุมวอลุ่มเอง → ป้องกันการหายไปสองชั้น
            // คุณจะใช้ 2D หรือ 3D ก็ได้ แต่ถ้าอยากให้ “ชั้นเดียว”
            // แนะนำตั้ง Spatial Blend=0 (2D) เพื่อใช้กราฟในสคริปต์นี้อย่างเดียว
            // src.spatialBlend = 0f;
        }

        if (src.playOnAwake && !src.isPlaying) src.Play();
    }

    void Update()
    {
        if (!src || !listener) return;

        if (useAudioSource3DAttenuation)
        {
            // ปล่อยให้ AudioSource 3D จัดการความดังเอง
            AutoPlayStopByAudible();
            return;
        }

        // คุมวอลุ่มด้วยสคริปต์ (Manual)
        float d = Vector3.Distance(transform.position, listener.position);
        float t = Mathf.InverseLerp(minDistance, maxDistance, d);   // 0=ใกล้ → 1=ไกล
        float baseVol = Mathf.Clamp01(volumeByDistance.Evaluate(t));

        // Occlusion (ทางเลือก)
        if (occlusionByLinecast)
        {
            Vector3 a = transform.position + Vector3.up * occlusionHeightOffset;
            Vector3 b = listener.position + Vector3.up * occlusionHeightOffset;
            bool blocked = Physics.Linecast(a, b, occlusionMask, QueryTriggerInteraction.Ignore);
            if (blocked) baseVol *= occludedVolumeMul;
        }

        float newVol = Mathf.SmoothDamp(src.volume, baseVol, ref volVel, smoothTime);
        src.volume = newVol;

        AutoPlayStopByAudible();
    }

    void AutoPlayStopByAudible()
    {
        bool audible = src.volume > 0.001f || (useAudioSource3DAttenuation && src.isPlaying);
        if (audible && autoPlayWhenAudible && !src.isPlaying) src.Play();
        if (!audible && autoStopWhenOut && src.isPlaying) src.Stop();
    }
}
