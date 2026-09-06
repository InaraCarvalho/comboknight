using UnityEngine;

public class SimpleAudio : MonoBehaviour
{
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource bgmSource;

    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip slashClip;
    [SerializeField] private AudioClip killClip;
    [SerializeField] private AudioClip coinClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip swapClip;
    [SerializeField] private AudioClip comboClip;
    [SerializeField] private AudioClip bgmClip;

    [SerializeField] private float loopStartTime = 0.0f;
    [SerializeField] private float loopEndTime = 164.53f;
    [SerializeField] private bool useLoopPoints = true;

    private void Awake()
    {
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = true;
        }

        GenerateProceduralClipsIfMissing();
    }

    private void Start()
    {
        if (bgmSource != null)
        {
            if (bgmClip != null) bgmSource.clip = bgmClip;
            if (bgmSource.clip != null && !bgmSource.isPlaying)
            {
                bgmSource.loop = true;
                bgmSource.volume = 0.35f;
                bgmSource.Play();
            }
        }
    }

    private void Update()
    {
        if (useLoopPoints && bgmSource != null && bgmSource.clip != null)
        {
            float targetEnd = (loopEndTime > 0f && loopEndTime <= bgmSource.clip.length) ? loopEndTime : bgmSource.clip.length;
            if (bgmSource.time >= targetEnd)
            {
                bgmSource.time = loopStartTime;
                if (!bgmSource.isPlaying) bgmSource.Play();
            }
        }
    }

    public void SetLoopPoints(float start, float end)
    {
        loopStartTime = Mathf.Max(0f, start);
        loopEndTime = end;
        useLoopPoints = true;
    }

    private void GenerateProceduralClipsIfMissing()
    {
        if (jumpClip == null) jumpClip = CreateProceduralTone(440f, 880f, 0.12f);
        if (slashClip == null) slashClip = CreateNoiseClip(0.1f);
        if (killClip == null) killClip = CreateProceduralTone(300f, 150f, 0.18f);
        if (coinClip == null) coinClip = CreateArpeggioClip(new float[] { 523.25f, 659.25f, 783.99f }, 0.15f);
        if (hurtClip == null) hurtClip = CreateProceduralTone(220f, 110f, 0.25f);
        if (swapClip == null) swapClip = CreateProceduralTone(600f, 900f, 0.08f);
        if (comboClip == null) comboClip = CreateArpeggioClip(new float[] { 440f, 554.37f, 659.25f, 880f }, 0.22f);

        if (bgmSource.clip == null)
        {
            bgmSource.clip = CreateBgmLoop();
            bgmSource.volume = 0.35f;
            bgmSource.Play();
        }
    }

    public void PlaySFX(GameManager.SoundType type) => PlaySFX(type, 1f);

    public void PlaySFX(GameManager.SoundType type, float pitch)
    {
        AudioClip clip = type switch
        {
            GameManager.SoundType.Jump => jumpClip,
            GameManager.SoundType.Slash => slashClip,
            GameManager.SoundType.Kill => killClip,
            GameManager.SoundType.Coin => coinClip,
            GameManager.SoundType.Hurt => hurtClip,
            GameManager.SoundType.Swap => swapClip,
            GameManager.SoundType.Combo => comboClip,
            _ => null
        };

        if (clip != null && sfxSource != null)
        {
            sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
            sfxSource.PlayOneShot(clip);
        }
    }

    private AudioClip CreateProceduralTone(float startFreq, float endFreq, float duration)
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            float phase = 2f * Mathf.PI * freq * (i / (float)sampleRate);
            float envelope = 1f - t;
            data[i] = Mathf.Sin(phase) * envelope * 0.4f;
        }
        AudioClip clip = AudioClip.Create("SFX_" + startFreq, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateNoiseClip(float duration)
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float envelope = 1f - t;
            data[i] = (Random.value * 2f - 1f) * envelope * 0.35f;
        }
        AudioClip clip = AudioClip.Create("SFX_Noise", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateArpeggioClip(float[] freqs, float duration)
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        int noteSamples = samples / freqs.Length;
        for (int i = 0; i < samples; i++)
        {
            int noteIndex = Mathf.Min(freqs.Length - 1, i / noteSamples);
            float freq = freqs[noteIndex];
            float phase = 2f * Mathf.PI * freq * (i / (float)sampleRate);
            float noteT = (float)(i % noteSamples) / noteSamples;
            float envelope = 1f - noteT * 0.5f;
            data[i] = Mathf.Sin(phase) * envelope * 0.35f;
        }
        AudioClip clip = AudioClip.Create("SFX_Arp", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateBgmLoop()
    {
        int sampleRate = 22050;
        float duration = 4.0f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        float[] notes = new float[] { 220f, 261.63f, 329.63f, 392f, 329.63f, 261.63f, 220f, 196f };
        int noteLen = samples / notes.Length;
        for (int i = 0; i < samples; i++)
        {
            int idx = (i / noteLen) % notes.Length;
            float freq = notes[idx];
            float phase = 2f * Mathf.PI * freq * (i / (float)sampleRate);
            float env = 0.8f + 0.2f * Mathf.Sin(i * 0.05f);
            data[i] = Mathf.Sin(phase) * env * 0.15f;
        }
        AudioClip clip = AudioClip.Create("BGM_Loop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
