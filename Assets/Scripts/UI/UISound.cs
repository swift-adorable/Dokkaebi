using UnityEngine;

/// <summary>
/// 【화면 효과음 — 코드로 만든 짧은 음】 (결정 2-142) — 아직 소리 파일이 없어 사인파로 만든다.
/// 뒤지다 물건이 드러날 때 등급마다 다른 소리: 흔함은 짧은 「톡」, 위로 갈수록 음이 높고 겹친다(전설은 세 음 올림).
/// 덕코프 모드 「아이템 가치 희귀도 표시 · 검색 사운드」를 따랐다. 소리 파일이 생기면 Clip()만 바꾼다.
/// 멈춘 화면(GamePause)에서도 들려야 하므로 AudioSource는 시간 배율을 타지 않는다(ignoreListenerPause).
/// </summary>
public static class UISound
{
    private const int Rate = 22050;
    private static AudioSource source;
    private static readonly AudioClip[] gradeClips = new AudioClip[ItemGrade.Count];
    private static AudioClip tick;

    /// <summary>소리 크기 [임시값].</summary>
    public static float Volume = 0.35f;

    /// <summary>뒤지는 중 — 한 칸 끝날 때마다 아주 작은 「톡」.</summary>
    public static void PlayTick() => Play(tick ??= Tone("tick", new[] { 420f }, 0.045f, 0.5f));

    /// <summary>물건이 드러났다 — 등급 소리.</summary>
    public static void PlayGrade(int grade)
    {
        grade = Mathf.Clamp(grade, 0, ItemGrade.Count - 1);
        gradeClips[grade] ??= Tone($"grade{grade}", NotesOf(grade), grade >= 3 ? 0.11f : 0.08f, 0.9f);
        Play(gradeClips[grade]);
    }

    /// <summary>등급별 음 — 흔함 한 음, 귀함부터 화음이 차례로 오른다.</summary>
    private static float[] NotesOf(int grade) => grade switch
    {
        0 => new[] { 523f },
        1 => new[] { 587f },
        2 => new[] { 659f, 784f },
        3 => new[] { 698f, 880f, 1047f },
        4 => new[] { 784f, 988f, 1175f, 1568f },
        _ => new[] { 880f, 1109f, 1319f, 1760f, 2217f },
    };

    private static void Play(AudioClip clip)
    {
        if (clip == null || Volume <= 0f)
            return;

        if (source == null)
        {
            var go = new GameObject("UISound (Runtime)");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        source.PlayOneShot(clip, Volume);
    }

    /// <summary>음들을 차례로(아르페지오) 잇는다. 각 음은 짧게 사라진다.</summary>
    private static AudioClip Tone(string name, float[] notes, float noteSeconds, float gain)
    {
        int per = Mathf.CeilToInt(noteSeconds * Rate);
        int tail = Mathf.CeilToInt(0.12f * Rate);
        var data = new float[per * notes.Length + tail];

        for (int n = 0; n < notes.Length; n++)
        {
            int start = n * per;
            int length = per + tail;
            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float envelope = Mathf.Min(1f, i / (Rate * 0.004f)) * Mathf.Exp(-t * 18f);
                float wave = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * t);
                data[start + i] += wave * envelope * gain * 0.45f;
            }
        }

        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
