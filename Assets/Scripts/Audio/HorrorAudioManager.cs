using UnityEngine;
using CyclesHell.AI;
using CyclesHell.Survival;

namespace CyclesHell.Audio
{
    /// <summary>
    /// Gestor de música de suspenso y efectos de terror con crossfade suave entre 3 capas:
    /// 1) Ambiente tenso de laboratorio al explorar.
    /// 2) Suspenso / latido acelerado cuando Sujeto_04 está cerca o investigando.
    /// 3) Persecución intensa cuando Sujeto_04 está en CHASE/ATTACK.
    /// Incluye AudioClips procedurales de respaldo y SFX de Flash UV, alarido y jumpscare (cero NullReferenceException).
    /// </summary>
    public class HorrorAudioManager : MonoBehaviour
    {
        public static HorrorAudioManager Instance { get; private set; }

        [Header("Referencias")]
        [SerializeField] private EnemyAI enemyAI;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Clips Opcionales (si son null se generan proceduralmente)")]
        [SerializeField] private AudioClip ambientClip;
        [SerializeField] private AudioClip suspenseClip;
        [SerializeField] private AudioClip chaseClip;

        [Header("Mezcla y Distancias")]
        [SerializeField] private float suspenseDistance = 14f;
        [SerializeField] private float crossfadeSpeed = 2.4f;
        [SerializeField] private float maxAmbientVolume = 0.24f;
        [SerializeField] private float maxSuspenseVolume = 0.44f;
        [SerializeField] private float maxChaseVolume = 0.56f;

        private AudioSource ambientSource;
        private AudioSource suspenseSource;
        private AudioSource chaseSource;
        private AudioSource sfxSource;

        private AudioClip genAmbient;
        private AudioClip genSuspense;
        private AudioClip genChase;
        private AudioClip sfxUvFlash;
        private AudioClip sfxEnemyScreech;
        private AudioClip sfxJumpscare;
        private AudioClip sfxClueFound;

        private void Awake()
        {
            Instance = this;

            if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>();
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }
            if (playerHealth == null && playerTransform != null)
            {
                playerHealth = playerTransform.GetComponent<PlayerHealth>();
            }

            ambientSource = CreateLoopSource("Layer_Ambient", ambientClip ?? (genAmbient = CreateAmbientClip()));
            suspenseSource = CreateLoopSource("Layer_Suspense", suspenseClip ?? (genSuspense = CreateSuspenseClip()));
            chaseSource = CreateLoopSource("Layer_Chase", chaseClip ?? (genChase = CreateChaseClip()));

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;

            sfxUvFlash = CreateUvFlashClip();
            sfxEnemyScreech = CreateScreechClip();
            sfxJumpscare = CreateJumpscareClip();
            sfxClueFound = CreateClueClip();
        }

        private void Start()
        {
            ambientSource.volume = maxAmbientVolume;
            suspenseSource.volume = 0f;
            chaseSource.volume = 0f;

            ambientSource.Play();
            suspenseSource.Play();
            chaseSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyIfValid(genAmbient);
            DestroyIfValid(genSuspense);
            DestroyIfValid(genChase);
            DestroyIfValid(sfxUvFlash);
            DestroyIfValid(sfxEnemyScreech);
            DestroyIfValid(sfxJumpscare);
            DestroyIfValid(sfxClueFound);
        }

        private void Update()
        {
            bool paused = Time.timeScale <= 0.001f;
            bool dead = playerHealth != null && playerHealth.IsDead;

            float targetAmb = (paused || dead) ? 0.05f : maxAmbientVolume;
            float targetSus = 0f;
            float targetChs = 0f;

            if (!paused && !dead && enemyAI != null && playerTransform != null && enemyAI.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(playerTransform.position, enemyAI.transform.position);
                EnemyState st = enemyAI.CurrentState;

                if (st == EnemyState.Chase || st == EnemyState.Attack)
                {
                    targetChs = maxChaseVolume;
                    targetSus = maxSuspenseVolume * 0.45f;
                    targetAmb = maxAmbientVolume * 0.45f;
                }
                else if (st == EnemyState.Flee)
                {
                    // Al huir del flash morado, la tensión baja gradualmente
                    targetSus = maxSuspenseVolume * 0.25f;
                    targetChs = 0f;
                }
                else
                {
                    float prox = 1f - Mathf.InverseLerp(2.5f, suspenseDistance, dist);
                    if (st == EnemyState.Investigate) prox = Mathf.Max(prox, 0.55f);
                    targetSus = Mathf.Clamp01(prox) * maxSuspenseVolume;
                }
            }

            float dt = Time.unscaledDeltaTime;
            float rate = 1f - Mathf.Exp(-crossfadeSpeed * dt);
            ambientSource.volume = Mathf.Lerp(ambientSource.volume, targetAmb, rate);
            suspenseSource.volume = Mathf.Lerp(suspenseSource.volume, targetSus, rate);
            chaseSource.volume = Mathf.Lerp(chaseSource.volume, targetChs, rate);
        }

        public void PlayUvFlashSound()
        {
            if (sfxSource != null && sfxUvFlash != null) sfxSource.PlayOneShot(sfxUvFlash, 0.72f);
        }

        public void PlayEnemyFleeScreech()
        {
            if (sfxSource != null && sfxEnemyScreech != null) sfxSource.PlayOneShot(sfxEnemyScreech, 0.75f);
        }

        public void PlayJumpscareStinger()
        {
            if (sfxSource != null && sfxJumpscare != null) sfxSource.PlayOneShot(sfxJumpscare, 0.90f);
        }

        public void PlayClueCollectedSound()
        {
            if (sfxSource != null && sfxClueFound != null) sfxSource.PlayOneShot(sfxClueFound, 0.55f);
        }

        private AudioSource CreateLoopSource(string childName, AudioClip clip)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private static void DestroyIfValid(AudioClip clip)
        {
            if (clip != null) Destroy(clip);
        }

        private static AudioClip CreateAmbientClip()
        {
            const int sr = 22050;
            const float dur = 6f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 123456789u;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float mod = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                float drone = Mathf.Sin(2f * Mathf.PI * 46f * t) * 0.28f +
                              Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.16f * mod +
                              Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.06f;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.94f + w * 0.06f;
                data[i] = Mathf.Clamp(drone + lp * 0.22f, -0.8f, 0.8f);
            }
            AudioClip c = AudioClip.Create("Procedural_LabAmbient", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateSuspenseClip()
        {
            const int sr = 22050;
            const float dur = 4f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float beat = t % 0.8f; // 75 BPM tense pulse
                float lub = Mathf.Exp(-Mathf.Pow((beat - 0.08f) / 0.04f, 2f)) * Mathf.Sin(2f * Mathf.PI * 54f * t) * 0.65f;
                float dub = Mathf.Exp(-Mathf.Pow((beat - 0.24f) / 0.05f, 2f)) * Mathf.Sin(2f * Mathf.PI * 43f * t) * 0.45f;
                float tensionPad = (Mathf.Sin(2f * Mathf.PI * 116.5f * t) + Mathf.Sin(2f * Mathf.PI * 123.5f * t)) * 0.12f *
                                   (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
                data[i] = Mathf.Clamp(lub + dub + tensionPad, -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("Procedural_Suspense", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateChaseClip()
        {
            const int sr = 22050;
            const float dur = 2f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 987654321u;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float beat = t % 0.4f; // 150 BPM pounding chase rhythm
                float kick = Mathf.Exp(-beat * 14f) * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 38f, beat / 0.4f) * beat) * 0.72f;
                float screech = (Mathf.Sin(2f * Mathf.PI * 233f * t) * 0.15f + Mathf.Sin(2f * Mathf.PI * 247f * t) * 0.15f) *
                                (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.5f * t));
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.72f + w * 0.28f;
                float perc = Mathf.Exp(-((t % 0.2f) * 28f)) * lp * 0.45f;
                data[i] = Mathf.Clamp(kick + screech + perc, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("Procedural_Chase", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateUvFlashClip()
        {
            const int sr = 22050;
            const float dur = 0.65f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 777123u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float pop = Mathf.Exp(-t * 36f) * w * 0.85f;
                float whineFreq = Mathf.Lerp(1600f, 3400f, t / dur);
                float whine = Mathf.Sin(2f * Mathf.PI * whineFreq * t) * Mathf.Exp(-t * 4.2f) * 0.30f;
                data[i] = Mathf.Clamp(pop + whine, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("SFX_UvFlash", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateScreechClip()
        {
            const int sr = 22050;
            const float dur = 0.75f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 445566u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                float f = Mathf.Lerp(620f, 210f, t / dur) + Mathf.Sin(2f * Mathf.PI * 38f * t) * 45f;
                float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.38f;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                data[i] = Mathf.Clamp((tone + w * 0.35f) * env, -0.92f, 0.92f);
            }
            AudioClip c = AudioClip.Create("SFX_EnemyScreech", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateJumpscareClip()
        {
            const int sr = 22050;
            const float dur = 1.15f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 999333u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 2.6f);
                float slam = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(130f, 34f, Mathf.Min(1f, t * 4f)) * t) * 0.75f;
                float dissonant = (Mathf.Sin(2f * Mathf.PI * 415f * t) + Mathf.Sin(2f * Mathf.PI * 440f * t) +
                                   Mathf.Sin(2f * Mathf.PI * 622f * t)) * 0.25f;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                data[i] = Mathf.Clamp((slam + dissonant + w * 0.45f) * env, -0.98f, 0.98f);
            }
            AudioClip c = AudioClip.Create("SFX_JumpscareStinger", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateClueClip()
        {
            const int sr = 22050;
            const float dur = 0.45f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 6.5f);
                float tone = (Mathf.Sin(2f * Mathf.PI * 520f * t) + Mathf.Sin(2f * Mathf.PI * 780f * t) * 0.5f) * 0.35f;
                data[i] = Mathf.Clamp(tone * env, -0.8f, 0.8f);
            }
            AudioClip c = AudioClip.Create("SFX_ClueFound", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
