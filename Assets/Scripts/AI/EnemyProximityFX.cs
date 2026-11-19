using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CyclesHell.Survival;

namespace CyclesHell.AI
{
    /// <summary>
    /// Efectos de tensión found-footage por proximidad a "Sujeto_04" (< 12m):
    /// - Aumenta progresivamente Film Grain y Chromatic Aberration en los URP Global Volumes (usando instancias de runtime).
    /// - Parpadeo sutil de interferencia VHS en el HUD de la cámara.
    /// - Audio procedural de latido + estática generado por código si no hay AudioClip asignado.
    /// </summary>
    public class EnemyProximityFX : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private EnemyAI enemyAI;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Volume baseVolume;
        [SerializeField] private Volume nightVisionVolume;
        [SerializeField] private CanvasGroup viewfinderCanvasGroup;
        [SerializeField] private UnityEngine.UI.Image vhsGlitchBarImage;
        [SerializeField] private AudioSource proximityAudioSource;

        [Header("Distancia y Post-Procesado URP")]
        [SerializeField] private float maxTensionDistance = 12f;
        [SerializeField] private float minTensionDistance = 2.0f;
        [SerializeField] private float maxExtraFilmGrain = 0.75f;
        [SerializeField] private float maxChromaticAberration = 0.85f;
        [SerializeField] private float smoothSpeed = 6f;

        [Header("Audio Procedural")]
        [SerializeField] private float maxAudioVolume = 0.42f;
        [SerializeField] private float minPitch = 0.90f;
        [SerializeField] private float maxPitch = 1.32f;

        private FilmGrain baseFilmGrain;
        private ChromaticAberration baseChromatic;
        private float initialBaseGrain = 0.22f;

        private FilmGrain nvFilmGrain;
        private ChromaticAberration nvChromatic;
        private float initialNvGrain = 0.72f;

        private float currentTension;
        private AudioClip generatedClip;

        private void Awake()
        {
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

            if (proximityAudioSource == null)
            {
                proximityAudioSource = GetComponent<AudioSource>();
                if (proximityAudioSource == null)
                {
                    proximityAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            SetupAudio();
        }

        private void Start()
        {
            SetupVolumeOverrides();
        }

        private void OnDestroy()
        {
            if (generatedClip != null)
            {
                Destroy(generatedClip);
            }
        }

        private void SetupVolumeOverrides()
        {
            // Usa .profile (clon de runtime) según urp-postprocessing para no alterar los assets en disco.
            if (baseVolume != null && baseVolume.profile != null)
            {
                if (!baseVolume.profile.TryGet(out baseFilmGrain))
                {
                    baseFilmGrain = baseVolume.profile.Add<FilmGrain>(true);
                }
                baseFilmGrain.intensity.overrideState = true;
                initialBaseGrain = baseFilmGrain.intensity.value;

                if (!baseVolume.profile.TryGet(out baseChromatic))
                {
                    baseChromatic = baseVolume.profile.Add<ChromaticAberration>(true);
                }
                baseChromatic.intensity.overrideState = true;
                baseChromatic.intensity.value = 0f;
            }

            if (nightVisionVolume != null && nightVisionVolume.profile != null)
            {
                if (!nightVisionVolume.profile.TryGet(out nvFilmGrain))
                {
                    nvFilmGrain = nightVisionVolume.profile.Add<FilmGrain>(true);
                }
                nvFilmGrain.intensity.overrideState = true;
                initialNvGrain = nvFilmGrain.intensity.value;

                if (!nightVisionVolume.profile.TryGet(out nvChromatic))
                {
                    nvChromatic = nightVisionVolume.profile.Add<ChromaticAberration>(true);
                }
                nvChromatic.intensity.overrideState = true;
                nvChromatic.intensity.value = 0f;
            }
        }

        private void SetupAudio()
        {
            proximityAudioSource.loop = true;
            proximityAudioSource.playOnAwake = false;
            proximityAudioSource.spatialBlend = 0f;
            proximityAudioSource.volume = 0f;

            if (proximityAudioSource.clip == null)
            {
                generatedClip = CreateProceduralTensionClip();
                proximityAudioSource.clip = generatedClip;
            }
        }

        private void Update()
        {
            bool isPausedOrDead = Time.timeScale <= 0.001f || (playerHealth != null && playerHealth.IsDead);
            float targetTension = 0f;

            if (!isPausedOrDead && enemyAI != null && playerTransform != null && enemyAI.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(playerTransform.position, enemyAI.transform.position);
                if (dist < maxTensionDistance)
                {
                    float linear = 1f - Mathf.InverseLerp(minTensionDistance, maxTensionDistance, dist);
                    targetTension = Mathf.Clamp01(linear * linear * 0.45f + linear * 0.55f);
                    if (enemyAI.IsChasing)
                    {
                        targetTension = Mathf.Clamp01(targetTension + 0.18f);
                    }
                }
            }

            float dt = Time.deltaTime;
            currentTension = Mathf.Lerp(currentTension, targetTension, 1f - Mathf.Exp(-smoothSpeed * dt));
            if (currentTension < 0.005f && targetTension <= 0f)
            {
                currentTension = 0f;
            }

            ApplyPostProcessing(currentTension);
            ApplyHUDInterference(currentTension, isPausedOrDead);
            ApplyProximityAudio(currentTension, isPausedOrDead);
        }

        private void ApplyPostProcessing(float t)
        {
            float glitchSpike = t > 0.35f && Mathf.PerlinNoise(Time.time * 26f, 0.41f) > 0.72f
                ? 0.16f * t
                : 0f;

            float chroma = Mathf.Clamp01(t * maxChromaticAberration + glitchSpike);

            if (baseFilmGrain != null)
            {
                baseFilmGrain.intensity.value = Mathf.Clamp01(initialBaseGrain + t * maxExtraFilmGrain);
            }
            if (baseChromatic != null)
            {
                baseChromatic.intensity.value = chroma;
            }

            if (nvFilmGrain != null)
            {
                nvFilmGrain.intensity.value = Mathf.Clamp01(initialNvGrain + t * 0.28f);
            }
            if (nvChromatic != null)
            {
                nvChromatic.intensity.value = chroma;
            }
        }

        private void ApplyHUDInterference(float t, bool isPausedOrDead)
        {
            if (viewfinderCanvasGroup != null)
            {
                if (isPausedOrDead || t <= 0.02f)
                {
                    viewfinderCanvasGroup.alpha = 1f;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(Time.time * 22f, 2.7f);
                    float drop = noise > 0.65f ? (noise - 0.65f) * 0.85f * t : 0f;
                    viewfinderCanvasGroup.alpha = Mathf.Clamp(1f - drop, 0.58f, 1f);
                }
            }

            if (vhsGlitchBarImage != null)
            {
                bool showBar = !isPausedOrDead && t > 0.18f && Mathf.PerlinNoise(Time.time * 19f, 7.3f) > (0.78f - t * 0.35f);
                if (vhsGlitchBarImage.enabled != showBar)
                {
                    vhsGlitchBarImage.enabled = showBar;
                }

                if (showBar)
                {
                    RectTransform rt = vhsGlitchBarImage.rectTransform;
                    float yNorm = Mathf.PerlinNoise(Time.time * 11f, 0.9f);
                    rt.anchorMin = new Vector2(0f, yNorm);
                    rt.anchorMax = new Vector2(1f, yNorm);
                    rt.sizeDelta = new Vector2(0f, Mathf.Lerp(4f, 18f, t));
                    Color c = vhsGlitchBarImage.color;
                    c.a = Mathf.Lerp(0.06f, 0.24f, t);
                    vhsGlitchBarImage.color = c;
                }
            }
        }

        private void ApplyProximityAudio(float t, bool isPausedOrDead)
        {
            if (proximityAudioSource == null) return;

            if (isPausedOrDead || t <= 0.01f)
            {
                proximityAudioSource.volume = 0f;
                if (proximityAudioSource.isPlaying && isPausedOrDead)
                {
                    proximityAudioSource.Pause();
                }
                return;
            }

            if (!proximityAudioSource.isPlaying)
            {
                proximityAudioSource.Play();
            }

            proximityAudioSource.volume = Mathf.Clamp01(t) * maxAudioVolume;
            proximityAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
        }

        /// <summary>
        /// Genera un AudioClip procedural de 2 segundos combinando latido grave (lub-dub) y estática VHS filtrada.
        /// </summary>
        private static AudioClip CreateProceduralTensionClip()
        {
            const int sampleRate = 22050;
            const float durationSeconds = 2.0f;
            int totalSamples = Mathf.RoundToInt(sampleRate * durationSeconds);
            float[] samples = new float[totalSamples];

            uint rng = 2463534242u;
            float lowPassStatic = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float time = i / (float)sampleRate;
                float beatCycle = time % 1.0f; // 1 ciclo por segundo (60 BPM base, sube con pitch)

                // Primer latido ("lub") cerca de t = 0.08s
                float lubEnv = Mathf.Exp(-Mathf.Pow((beatCycle - 0.10f) / 0.045f, 2f));
                float lub = Mathf.Sin(2f * Mathf.PI * 52f * time) * lubEnv * 0.72f;

                // Segundo latido ("dub") cerca de t = 0.28s
                float dubEnv = Mathf.Exp(-Mathf.Pow((beatCycle - 0.28f) / 0.055f, 2f));
                float dub = Mathf.Sin(2f * Mathf.PI * 41f * time) * dubEnv * 0.52f;

                // Ruido blanco determinista filtrado paso-bajo para estática VHS suave
                rng ^= rng << 13;
                rng ^= rng >> 17;
                rng ^= rng << 5;
                float white = ((rng & 0xFFFF) / 32768f) - 1f;
                lowPassStatic = lowPassStatic * 0.86f + white * 0.14f;

                // Zumbido grave eléctrico de cinta 2004 (60 Hz tenue)
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * time) * 0.05f;

                samples[i] = Mathf.Clamp(lub + dub + lowPassStatic * 0.30f + hum, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("Procedural_ProximityTension", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
