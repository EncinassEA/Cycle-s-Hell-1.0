using UnityEngine;
using CyclesHell.UI;

namespace CyclesHell.Audio
{
    /// <summary>
    /// Director de sustos sonoros 3D para la cabaña nocturna (lloros, gritos lejanos, crujidos de madera,
    /// susurros y golpes secos).
    /// Arrastra tus propios AudioClips desde Assets/Audio/Terror a las listas públicas en el Inspector;
    /// si alguna lista está vacía, genera efectos procedurales inquietantes de respaldo (cero NullReferenceException).
    /// </summary>
    public class CreepySoundDirector : MonoBehaviour
    {
        [Header("Audios Personalizados (Arrastra tus clips desde Assets/Audio/Terror)")]
        public AudioClip[] childCryingClips;
        public AudioClip[] distantScreamClips;
        public AudioClip[] woodCreakClips;
        public AudioClip[] whisperClips;
        public AudioClip[] knockingClips;

        [Header("Puntos de Emisión 3D (Segundo piso, baño, escaleras, bosque)")]
        [SerializeField] private AudioSource[] spatialSoundPoints;

        [Header("Ritmo y Volumen")]
        [SerializeField] private float minIntervalSeconds = 18f;
        [SerializeField] private float maxIntervalSeconds = 35f;
        [SerializeField] private float minDistanceFromPlayer = 4.5f;
        [SerializeField] private float eventVolume = 0.78f;

        private Transform playerTransform;
        private float nextEventTimer;

        // Clips procedurales de respaldo si las listas del Inspector están vacías
        private AudioClip fallbackCreak;
        private AudioClip fallbackKnock;
        private AudioClip fallbackWhisper;
        private AudioClip fallbackDistantWail;

        private void Awake()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;

            if (spatialSoundPoints == null || spatialSoundPoints.Length == 0)
            {
                spatialSoundPoints = GetComponentsInChildren<AudioSource>(true);
            }

            if (spatialSoundPoints != null)
            {
                for (int i = 0; i < spatialSoundPoints.Length; i++)
                {
                    if (spatialSoundPoints[i] == null) continue;
                    spatialSoundPoints[i].spatialBlend = 1f;
                    spatialSoundPoints[i].rolloffMode = AudioRolloffMode.Logarithmic;
                    spatialSoundPoints[i].minDistance = 2.5f;
                    spatialSoundPoints[i].maxDistance = 30f;
                    spatialSoundPoints[i].playOnAwake = false;
                    spatialSoundPoints[i].loop = false;
                }
            }

            fallbackCreak = CreateWoodCreakClip();
            fallbackKnock = CreateDoorKnockClip();
            fallbackWhisper = CreateEerieWhisperClip();
            fallbackDistantWail = CreateDistantEchoWailClip();

            // Primer evento un poco antes (~14-20s tras terminar la intro) para enganchar rápido
            nextEventTimer = Random.Range(14f, 22f);
        }

        private void OnDestroy()
        {
            if (fallbackCreak != null) Destroy(fallbackCreak);
            if (fallbackKnock != null) Destroy(fallbackKnock);
            if (fallbackWhisper != null) Destroy(fallbackWhisper);
            if (fallbackDistantWail != null) Destroy(fallbackDistantWail);
        }

        private void Update()
        {
            if (Time.timeScale <= 0.001f) return;

            if (StoryObjectiveManager.Instance != null &&
                (StoryObjectiveManager.Instance.IsIntroActive || StoryObjectiveManager.Instance.IsVictoryActive))
            {
                return;
            }

            nextEventTimer -= Time.deltaTime;
            if (nextEventTimer <= 0f)
            {
                TriggerRandomSpatialEvent();
                nextEventTimer = Random.Range(minIntervalSeconds, maxIntervalSeconds);
            }
        }

        public void TriggerRandomSpatialEvent()
        {
            if (spatialSoundPoints == null || spatialSoundPoints.Length == 0) return;

            AudioSource chosenSource = PickSourceInOtherRoom();
            if (chosenSource == null) return;

            AudioClip clip = PickClipOrFallback();
            if (clip == null) return;

            chosenSource.pitch = Random.Range(0.90f, 1.08f);
            chosenSource.PlayOneShot(clip, eventVolume);
        }

        private AudioSource PickSourceInOtherRoom()
        {
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            // Preferir emisores en otro piso o a cierta distancia del jugador
            AudioSource fallbackSrc = null;
            int count = spatialSoundPoints.Length;
            int startIdx = Random.Range(0, count);

            for (int i = 0; i < count; i++)
            {
                AudioSource src = spatialSoundPoints[(startIdx + i) % count];
                if (src == null) continue;
                fallbackSrc = src;

                if (playerTransform == null) return src;

                float dist = Vector3.Distance(playerTransform.position, src.transform.position);
                bool differentFloor = Mathf.Abs(playerTransform.position.y - src.transform.position.y) > 2.0f;
                if (dist >= minDistanceFromPlayer && (differentFloor || dist >= 7.5f))
                {
                    return src;
                }
            }

            return fallbackSrc;
        }

        private AudioClip PickClipOrFallback()
        {
            int category = Random.Range(0, 5);
            AudioClip custom = category switch
            {
                0 => PickRandomValid(childCryingClips),
                1 => PickRandomValid(distantScreamClips),
                2 => PickRandomValid(woodCreakClips),
                3 => PickRandomValid(whisperClips),
                _ => PickRandomValid(knockingClips)
            };

            if (custom != null) return custom;

            // Respaldo procedural inquietante según la categoría
            return category switch
            {
                0 => fallbackDistantWail,
                1 => fallbackDistantWail,
                2 => fallbackCreak,
                3 => fallbackWhisper,
                _ => fallbackKnock
            };
        }

        private static AudioClip PickRandomValid(AudioClip[] array)
        {
            if (array == null || array.Length == 0) return null;
            int start = Random.Range(0, array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                AudioClip c = array[(start + i) % array.Length];
                if (c != null) return c;
            }
            return null;
        }

        private static AudioClip CreateWoodCreakClip()
        {
            const int sr = 22050;
            const float dur = 1.35f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                float freq = Mathf.Lerp(155f, 88f, t / dur) + Mathf.Sin(2f * Mathf.PI * 19f * t) * 24f;
                phase += (2f * Mathf.PI * freq) / sr;
                float creak = (Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2.7f) * 0.22f) * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 11f * t));
                data[i] = Mathf.Clamp(creak * env * 0.85f, -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("Procedural_WoodCreak", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateDoorKnockClip()
        {
            const int sr = 22050;
            const float dur = 1.15f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 616263u;
            float[] knockTimes = { 0.05f, 0.34f, 0.64f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float val = 0f;
                for (int k = 0; k < knockTimes.Length; k++)
                {
                    float dt = t - knockTimes[k];
                    if (dt >= 0f && dt < 0.25f)
                    {
                        float env = Mathf.Exp(-dt * 26f);
                        val += (Mathf.Sin(2f * Mathf.PI * 115f * dt) * 0.72f + w * 0.38f) * env;
                    }
                }
                data[i] = Mathf.Clamp(val, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("Procedural_DoorKnocks", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateEerieWhisperClip()
        {
            const int sr = 22050;
            const float dur = 2.1f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 424242u;
            float lp = 0f;
            float hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.75f + w * 0.25f;
                hp = lp - hp * 0.55f;
                float syllables = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2.2f * t)), 1.5f);
                float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                float breath = Mathf.Sin(2f * Mathf.PI * 390f * t) * 0.08f;
                data[i] = Mathf.Clamp((hp * 0.55f + breath) * (0.35f + 0.65f * syllables) * env, -0.85f, 0.85f);
            }
            AudioClip c = AudioClip.Create("Procedural_GhostWhisper", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateDistantEchoWailClip()
        {
            const int sr = 22050;
            const float dur = 2.4f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 808080u;
            float phase = 0f;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float norm = t / dur;
                float env = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * 0.9f);
                float f = Mathf.Lerp(540f, 340f, norm) + Mathf.Sin(2f * Mathf.PI * 5.8f * t) * 18f;
                phase += (2f * Mathf.PI * f) / sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.88f + w * 0.12f;
                float tone = (Mathf.Sin(phase) * 0.34f + Mathf.Sin(phase * 1.5f) * 0.14f + lp * 0.22f) * env;
                data[i] = Mathf.Clamp(tone, -0.85f, 0.85f);
            }
            AudioClip c = AudioClip.Create("Procedural_DistantEchoCry", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
