using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using CyclesHell.Player;
using CyclesHell.AI;
using CyclesHell.UI;
using CyclesHell.Audio;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Cinemática de llegada en tiempo real estilo Found-Footage 2004 (~9 segundos, saltable con Espacio).
    /// - Encendido de videocámara VHS (estática fuerte + sonido mecánico REC + enfoque que aclara).
    /// - Movimiento suave mirando hacia el suelo y elevándose hacia el pasillo principal mientras
    ///   una luz del fondo parpadea y una silueta rápida cruza al final del corredor y desaparece.
    /// - Sincroniza el texto de la sinopsis sin tapar la escena 3D y entrega control limpio al jugador.
    /// Compatible 100% con Unity 6, URP y exclusivamente UnityEngine.InputSystem.
    /// </summary>
    public class ArrivalCutsceneController : MonoBehaviour
    {
        [Header("Duración y Cámara")]
        [SerializeField] private float totalDuration = 9.0f;
        [SerializeField] private float startPitchDegrees = 42f;
        [SerializeField] private float endPitchDegrees = 0f;
        [SerializeField] private float startYawOffset = -6f;

        [Header("Referencias de Escena")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private CamcorderController camcorderController;
        [SerializeField] private Volume baseVolume;
        [SerializeField] private EnemyAI enemyAI;
        [SerializeField] private Transform safeEnemyStartWaypoint;

        [Header("Eventos en el Pasillo")]
        [SerializeField] private Light distantCorridorLight;
        [SerializeField] private Transform silhouetteTransform;
        [SerializeField] private Vector3 silhouetteStartPos = new Vector3(-2.15f, 0f, 14.8f);
        [SerializeField] private Vector3 silhouetteEndPos = new Vector3(2.25f, 0f, 14.8f);
        [SerializeField] private float silhouetteStartTime = 4.1f;
        [SerializeField] private float silhouetteDuration = 1.05f;

        [Header("Overlay VHS y Sinopsis")]
        [SerializeField] private GameObject cutsceneOverlayRoot;
        [SerializeField] private Image staticFadeOverlay;
        [SerializeField] private Image vhsScanBar;
        [SerializeField] private TMP_Text statusTagText;
        [SerializeField] private TMP_Text synopsisSubtitleText;
        [SerializeField] private TMP_Text skipHintText;

        [TextArea(3, 6)]
        [SerializeField] private string synopsisLine1 =
            "NOV. 08. 2004 — 23:14  |  Sin señal de radio ni rastro de mis compañeros en la cabaña...";
        [TextArea(3, 6)]
        [SerializeField] private string synopsisLine2 =
            "Bajo la trampilla del sótano descubrí este laboratorio subterráneo. Debo encontrar 5 expedientes y salir vivo.";

        private InputAction skipCutsceneAction;
        private AudioSource cutsceneAudioSource;
        private AudioClip sfxTapePowerOn;
        private AudioClip sfxStaticLoop;
        private AudioClip sfxDistantThud;

        private DepthOfField runtimeDof;
        private FilmGrain runtimeGrain;
        private ChromaticAberration runtimeChroma;
        private ColorAdjustments runtimeColorAdj;

        private bool initialDofActive;
        private float initialDofFocus = 10f;
        private float initialGrain = 0.22f;
        private float initialChroma = 0f;
        private float initialExposure = 0f;

        private bool isPlaying;
        private bool hasFinished;
        private bool thudPlayed;
        private float basePlayerYaw;
        private Vector3 basePivotLocalPos;

        public static bool IsCutsceneActive { get; private set; }

        private void Awake()
        {
            if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();
            if (camcorderController == null) camcorderController = FindAnyObjectByType<CamcorderController>();
            if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>();

            if (cameraPivot == null && playerController != null)
            {
                UnityEngine.Camera cam = playerController.GetComponentInChildren<UnityEngine.Camera>();
                if (cam != null)
                {
                    cameraPivot = cam.transform.parent != null && cam.transform.parent != playerController.transform
                        ? cam.transform.parent
                        : cam.transform;
                }
            }

            if (baseVolume == null)
            {
                GameObject volGo = GameObject.Find("Base_GlobalVolume");
                if (volGo != null) baseVolume = volGo.GetComponent<Volume>();
            }

            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.ExternalIntroControlled = true;
            }
            else
            {
                StoryObjectiveManager mgr = FindAnyObjectByType<StoryObjectiveManager>();
                if (mgr != null) mgr.ExternalIntroControlled = true;
            }

            skipCutsceneAction = new InputAction("SkipArrivalCutscene", InputActionType.Button, "<Keyboard>/space");

            cutsceneAudioSource = gameObject.AddComponent<AudioSource>();
            cutsceneAudioSource.playOnAwake = false;
            cutsceneAudioSource.spatialBlend = 0f;
            cutsceneAudioSource.loop = true;

            sfxTapePowerOn = CreateTapePowerOnClip();
            sfxStaticLoop = CreateStaticHumClip();
            sfxDistantThud = CreateDistantThudClip();
        }

        private void OnEnable()
        {
            skipCutsceneAction?.Enable();
        }

        private void OnDisable()
        {
            skipCutsceneAction?.Disable();
        }

        private void OnDestroy()
        {
            if (isPlaying) IsCutsceneActive = false;
            skipCutsceneAction?.Dispose();
            if (sfxTapePowerOn != null) Destroy(sfxTapePowerOn);
            if (sfxStaticLoop != null) Destroy(sfxStaticLoop);
            if (sfxDistantThud != null) Destroy(sfxDistantThud);
        }

        private void Start()
        {
            if (silhouetteTransform != null)
            {
                silhouetteTransform.position = silhouetteStartPos;
                silhouetteTransform.gameObject.SetActive(false);
            }

            EnsureOverlayReferences();
            CacheVolumeOverrides();
            StartCutscene();
        }

        private void Update()
        {
            if (!isPlaying) return;

            if (skipCutsceneAction.WasPressedThisFrame())
            {
                FinishCutscene();
            }
        }

        private void EnsureOverlayReferences()
        {
            if (cutsceneOverlayRoot != null) return;

            GameObject found = GameObject.Find("ArrivalCutsceneOverlay");
            if (found != null)
            {
                cutsceneOverlayRoot = found;
                Transform fadeT = found.transform.Find("StaticFadeOverlay");
                if (fadeT != null) staticFadeOverlay = fadeT.GetComponent<Image>();

                Transform scanT = found.transform.Find("VHSScanBar");
                if (scanT != null) vhsScanBar = scanT.GetComponent<Image>();

                Transform tagT = found.transform.Find("CutsceneStatusTag");
                if (tagT != null) statusTagText = tagT.GetComponent<TMP_Text>();

                Transform subT = found.transform.Find("SubtitlePanel/SynopsisSubtitleText");
                if (subT != null) synopsisSubtitleText = subT.GetComponent<TMP_Text>();

                Transform hintT = found.transform.Find("SubtitlePanel/SkipHintText");
                if (hintT != null) skipHintText = hintT.GetComponent<TMP_Text>();
            }
        }

        private void CacheVolumeOverrides()
        {
            if (baseVolume == null || baseVolume.profile == null) return;

            if (!baseVolume.profile.TryGet(out runtimeDof))
            {
                runtimeDof = baseVolume.profile.Add<DepthOfField>(true);
                initialDofActive = false;
            }
            else
            {
                initialDofActive = runtimeDof.active;
                initialDofFocus = runtimeDof.focusDistance.value;
            }

            if (!baseVolume.profile.TryGet(out runtimeGrain))
            {
                runtimeGrain = baseVolume.profile.Add<FilmGrain>(true);
            }
            runtimeGrain.intensity.overrideState = true;
            initialGrain = runtimeGrain.intensity.value;

            if (!baseVolume.profile.TryGet(out runtimeChroma))
            {
                runtimeChroma = baseVolume.profile.Add<ChromaticAberration>(true);
            }
            runtimeChroma.intensity.overrideState = true;
            initialChroma = runtimeChroma.intensity.value;

            if (!baseVolume.profile.TryGet(out runtimeColorAdj))
            {
                runtimeColorAdj = baseVolume.profile.Add<ColorAdjustments>(true);
            }
            runtimeColorAdj.postExposure.overrideState = true;
            initialExposure = runtimeColorAdj.postExposure.value;
        }

        private void StartCutscene()
        {
            isPlaying = true;
            hasFinished = false;
            thudPlayed = false;
            IsCutsceneActive = true;

            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.ExternalIntroControlled = true;
                StoryObjectiveManager.Instance.SetExternalIntroActive(true);
            }

            if (playerController != null)
            {
                basePlayerYaw = playerController.transform.eulerAngles.y;
            }

            if (cameraPivot != null)
            {
                basePivotLocalPos = cameraPivot.localPosition;
                cameraPivot.localRotation = Quaternion.Euler(startPitchDegrees, 0f, 0f);
            }

            if (camcorderController != null)
            {
                camcorderController.SetCamcorderActive(true, notify: true);
                camcorderController.SetLampActive(true, notify: true);
            }

            if (cutsceneOverlayRoot != null)
            {
                cutsceneOverlayRoot.SetActive(true);
            }

            if (skipHintText != null)
            {
                skipHintText.text = "[ESPACIO] SALTAR SECUENCIA";
            }

            // Audio de encendido VHS + estática suave
            if (cutsceneAudioSource != null)
            {
                if (sfxTapePowerOn != null) cutsceneAudioSource.PlayOneShot(sfxTapePowerOn, 0.78f);
                if (sfxStaticLoop != null)
                {
                    cutsceneAudioSource.clip = sfxStaticLoop;
                    cutsceneAudioSource.volume = 0.42f;
                    cutsceneAudioSource.Play();
                }
            }

            StartCoroutine(CutsceneRoutine());
        }

        private IEnumerator CutsceneRoutine()
        {
            float elapsed = 0f;
            float baseLightIntensity = distantCorridorLight != null ? distantCorridorLight.intensity : 1.5f;

            while (elapsed < totalDuration && isPlaying)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, totalDuration));

                // 1) Efecto de encendido VHS (0.0s - 2.1s): oscuridad -> estática -> enfoque nítido
                float bootNorm = Mathf.Clamp01(elapsed / 2.1f);
                float smoothBoot = bootNorm * bootNorm * (3f - 2f * bootNorm);

                if (staticFadeOverlay != null)
                {
                    float staticJitter = (1f - smoothBoot) * (0.12f * Mathf.PerlinNoise(elapsed * 34f, 1.1f));
                    Color c = staticFadeOverlay.color;
                    c.a = Mathf.Clamp01((1f - smoothBoot) * 0.92f + staticJitter);
                    staticFadeOverlay.color = c;
                }

                if (vhsScanBar != null)
                {
                    float barAlpha = Mathf.Lerp(0.38f, 0.05f, smoothBoot);
                    bool flashBar = elapsed < 2.4f || (elapsed >= silhouetteStartTime && elapsed <= silhouetteStartTime + silhouetteDuration + 0.35f);
                    vhsScanBar.enabled = flashBar;
                    if (flashBar)
                    {
                        RectTransform rt = vhsScanBar.rectTransform;
                        float y = Mathf.Repeat(elapsed * 0.85f + Mathf.PerlinNoise(elapsed * 9f, 3.2f) * 0.25f, 1f);
                        rt.anchorMin = new Vector2(0f, y);
                        rt.anchorMax = new Vector2(1f, y);
                        rt.sizeDelta = new Vector2(0f, Mathf.Lerp(26f, 8f, smoothBoot));
                        Color bc = vhsScanBar.color;
                        bc.a = barAlpha;
                        vhsScanBar.color = bc;
                    }
                }

                if (statusTagText != null)
                {
                    if (elapsed < 1.1f) statusTagText.text = "PLAY  ►  SP   00:00:00";
                    else if (elapsed < 2.2f) statusTagText.text = "● REC   AUTO-FOCUS...";
                    else statusTagText.text = "● REC   CINTA #01 — SÓTANO";
                }

                // Post-procesado URP: desenfoque, grano y exposición aclarando
                if (runtimeDof != null)
                {
                    bool blurOn = elapsed < 2.4f;
                    runtimeDof.active = blurOn || initialDofActive;
                    if (blurOn)
                    {
                        runtimeDof.mode.Override(DepthOfFieldMode.Gaussian);
                        runtimeDof.gaussianStart.Override(Mathf.Lerp(0.2f, 6.0f, smoothBoot));
                        runtimeDof.gaussianEnd.Override(Mathf.Lerp(2.0f, 18.0f, smoothBoot));
                        runtimeDof.gaussianMaxRadius.Override(Mathf.Lerp(1.35f, 0.1f, smoothBoot));
                    }
                }

                if (runtimeGrain != null)
                {
                    float silSpike = (elapsed >= silhouetteStartTime && elapsed <= silhouetteStartTime + silhouetteDuration) ? 0.32f : 0f;
                    runtimeGrain.intensity.value = Mathf.Clamp01(Mathf.Lerp(0.95f, initialGrain, smoothBoot) + silSpike);
                }

                if (runtimeChroma != null)
                {
                    float silSpike = (elapsed >= silhouetteStartTime && elapsed <= silhouetteStartTime + silhouetteDuration) ? 0.45f : 0f;
                    runtimeChroma.intensity.value = Mathf.Clamp01(Mathf.Lerp(0.65f, initialChroma, smoothBoot) + silSpike);
                }

                if (runtimeColorAdj != null)
                {
                    runtimeColorAdj.postExposure.value = Mathf.Lerp(-2.6f, initialExposure, smoothBoot);
                }

                if (cutsceneAudioSource != null)
                {
                    cutsceneAudioSource.volume = Mathf.Lerp(0.40f, 0.04f, smoothBoot);
                }

                // 2) Movimiento suave de cámara: mirando abajo -> levantando hacia el pasillo
                if (cameraPivot != null && playerController != null)
                {
                    float liftT = Mathf.Clamp01((elapsed - 0.9f) / 4.2f);
                    float smoothLift = liftT * liftT * (3f - 2f * liftT);
                    float pitch = Mathf.Lerp(startPitchDegrees, endPitchDegrees, smoothLift);

                    // Ligero paneo humano al inspeccionar el pasillo
                    float yawOffset = Mathf.Lerp(startYawOffset, 0f, smoothLift);
                    if (elapsed > 5.3f)
                    {
                        float settleT = Mathf.Clamp01((elapsed - 5.3f) / 3.2f);
                        yawOffset += Mathf.Sin(settleT * Mathf.PI * 1.4f) * 3.8f * (1f - settleT);
                    }

                    // Respiración sutil found-footage en la mano
                    float swayX = (Mathf.PerlinNoise(elapsed * 1.3f, 0.4f) - 0.5f) * 0.032f;
                    float swayY = (Mathf.PerlinNoise(0.8f, elapsed * 1.6f) - 0.5f) * 0.028f;
                    float rollZ = (Mathf.PerlinNoise(elapsed * 0.9f, 5.1f) - 0.5f) * 1.6f * (1f - t * 0.6f);

                    playerController.transform.rotation = Quaternion.Euler(0f, basePlayerYaw + yawOffset, 0f);
                    cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, rollZ);
                    cameraPivot.localPosition = basePivotLocalPos + new Vector3(swayX, swayY, 0f);
                }

                // 3) Luz del fondo parpadeando y silueta rápida cruzando el corredor
                if (distantCorridorLight != null && elapsed > 2.6f && elapsed < 6.4f)
                {
                    float flick = Mathf.PerlinNoise(elapsed * 26f, 7.2f);
                    distantCorridorLight.intensity = flick < 0.35f ? baseLightIntensity * 0.06f : baseLightIntensity * Mathf.Lerp(0.9f, 1.65f, flick);
                }

                if (silhouetteTransform != null)
                {
                    if (elapsed >= silhouetteStartTime && elapsed <= silhouetteStartTime + silhouetteDuration)
                    {
                        if (!silhouetteTransform.gameObject.activeSelf)
                        {
                            silhouetteTransform.gameObject.SetActive(true);
                        }

                        if (!thudPlayed)
                        {
                            thudPlayed = true;
                            if (cutsceneAudioSource != null && sfxDistantThud != null)
                            {
                                cutsceneAudioSource.PlayOneShot(sfxDistantThud, 0.62f);
                            }
                            if (CameraShake.Instance != null)
                            {
                                CameraShake.Instance.Shake(0.06f, 0.25f);
                            }
                        }

                        float st = Mathf.Clamp01((elapsed - silhouetteStartTime) / Mathf.Max(0.05f, silhouetteDuration));
                        Vector3 pos = Vector3.Lerp(silhouetteStartPos, silhouetteEndPos, st);
                        pos.y += Mathf.Abs(Mathf.Sin(st * Mathf.PI * 5f)) * 0.07f;
                        silhouetteTransform.position = pos;
                    }
                    else if (silhouetteTransform.gameObject.activeSelf)
                    {
                        silhouetteTransform.gameObject.SetActive(false);
                    }
                }

                // 4) Subtítulo sincronizado de la sinopsis
                if (synopsisSubtitleText != null)
                {
                    if (elapsed < 1.1f)
                    {
                        synopsisSubtitleText.text = "";
                    }
                    else if (elapsed < 4.9f)
                    {
                        float p = Mathf.Clamp01((elapsed - 1.1f) / 2.2f);
                        int len = Mathf.Clamp(Mathf.FloorToInt(synopsisLine1.Length * p), 0, synopsisLine1.Length);
                        synopsisSubtitleText.text = synopsisLine1.Substring(0, len);
                    }
                    else
                    {
                        float p = Mathf.Clamp01((elapsed - 4.9f) / 2.5f);
                        int len = Mathf.Clamp(Mathf.FloorToInt(synopsisLine2.Length * p), 0, synopsisLine2.Length);
                        synopsisSubtitleText.text = synopsisLine2.Substring(0, len);
                    }
                }

                yield return null;
            }

            if (!hasFinished)
            {
                FinishCutscene();
            }
        }

        public void FinishCutscene()
        {
            if (hasFinished) return;
            hasFinished = true;
            isPlaying = false;
            IsCutsceneActive = false;

            StopAllCoroutines();

            if (cutsceneAudioSource != null)
            {
                cutsceneAudioSource.Stop();
            }

            if (silhouetteTransform != null)
            {
                silhouetteTransform.gameObject.SetActive(false);
            }

            if (cutsceneOverlayRoot != null)
            {
                cutsceneOverlayRoot.SetActive(false);
            }

            // Restaurar post-procesado URP limpio
            if (runtimeDof != null)
            {
                runtimeDof.active = initialDofActive;
                runtimeDof.focusDistance.value = initialDofFocus;
            }
            if (runtimeGrain != null) runtimeGrain.intensity.value = initialGrain;
            if (runtimeChroma != null) runtimeChroma.intensity.value = initialChroma;
            if (runtimeColorAdj != null) runtimeColorAdj.postExposure.value = initialExposure;

            // Alinear cámara limpia mirando hacia el pasillo principal
            if (playerController != null)
            {
                playerController.transform.rotation = Quaternion.Euler(0f, basePlayerYaw, 0f);
            }
            if (cameraPivot != null)
            {
                cameraPivot.localPosition = basePivotLocalPos;
                cameraPivot.localRotation = Quaternion.Euler(endPitchDegrees, 0f, 0f);
            }
            if (playerController != null)
            {
                playerController.SyncLookAnglesFromTransform();
            }

            // Asegurar que el enemigo arranque lejos patrullando
            if (enemyAI != null && safeEnemyStartWaypoint != null && playerController != null)
            {
                float dist = Vector3.Distance(enemyAI.transform.position, playerController.transform.position);
                if (dist < 14f)
                {
                    UnityEngine.AI.NavMeshAgent nav = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (nav != null && nav.isOnNavMesh)
                    {
                        nav.Warp(safeEnemyStartWaypoint.position);
                    }
                }
            }

            // Entregar control completo al jugador
            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.SetExternalIntroActive(false);
            }
            else if (playerController != null)
            {
                playerController.SetControlPaused(false, false, false);
                playerController.SetCursorLocked(true);
            }
        }

        private static AudioClip CreateTapePowerOnClip()
        {
            const int sr = 22050;
            const float dur = 0.85f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 654321u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;

                // Click mecánico de casete + pitido "REC" a los 0.25s + zumbido motor cinta
                float click = Mathf.Exp(-t * 60f) * w * 0.7f;
                float beepEnv = (t >= 0.24f && t <= 0.36f) ? 1f : 0f;
                float beep = Mathf.Sin(2f * Mathf.PI * 1480f * t) * beepEnv * 0.32f;
                float motor = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 290f, t / dur) * t) * 0.14f * Mathf.Exp(-t * 2.2f);
                data[i] = Mathf.Clamp(click + beep + motor + w * 0.08f * Mathf.Exp(-t * 2.5f), -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("SFX_VHS_PowerOn", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateStaticHumClip()
        {
            const int sr = 22050;
            const float dur = 2.0f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 314159u;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.82f + w * 0.18f;
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.12f;
                data[i] = Mathf.Clamp(lp * 0.55f + hum, -0.8f, 0.8f);
            }
            AudioClip c = AudioClip.Create("SFX_VHS_StaticLoop", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateDistantThudClip()
        {
            const int sr = 22050;
            const float dur = 0.7f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 271828u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 38f, t / dur) * t) * Mathf.Exp(-t * 7.5f) * 0.72f;
                float scrape = Mathf.Sin(2f * Mathf.PI * 310f * t) * Mathf.Exp(-t * 5.0f) * 0.16f;
                data[i] = Mathf.Clamp(thud + scrape + w * 0.18f * Mathf.Exp(-t * 10f), -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("SFX_DistantSilhouetteThud", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
