using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Video;
using CyclesHell.Player;
using CyclesHell.UI;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Cinemática de llegada en carro para "Nivel_01_Cabana.unity" (saltable en cualquier momento con Espacio).
    /// - MODO VIDEO OPCIONAL: Si asignas un VideoClip (p. ej. Assets/Video/IntroCoche.mp4) en el Inspector,
    ///   reproduce el video a pantalla completa con audio y al terminar entrega el control frente al porche.
    /// - MODO 3D EN TIEMPO REAL (activo por defecto, ~8.8s):
    ///   FASE 1 (~3.5s) — Llegada en movimiento: el carro arranca ~15m atrás en el camino de tierra y avanza
    ///                    desacelerando hasta frenar frente al porche con cabeceo de frenado y faros cortando la niebla.
    ///   FASE 2 (~2.5s) — Apagado y bajada: motor apagándose, leve parpadeo de faros, seguro/puerta abriéndose,
    ///                    salida fluida con head-bobbing natural y portazo al cerrar.
    ///   FASE 3 (~2.8s) — Encendido de videocámara: par de pasos hacia la cabaña mirando hacia las ventanas del
    ///                    segundo piso (una luz superior se apaga para generar tensión), encendido VHS con estática,
    ///                    enfoque rápido y texto de fecha/sinopsis antes de entregar el control limpio.
    /// 100% Unity 6, URP y exclusivamente UnityEngine.InputSystem.
    /// </summary>
    public class CarArrivalCutscene : MonoBehaviour
    {
        [Header("Modo Video Opcional (Arrastra aquí Assets/Video/IntroCoche.mp4)")]
        [SerializeField] private VideoClip introVideoClip;

        [Header("Modo 3D en Tiempo Real — Carro y Recorrido")]
        [SerializeField] private Transform doctorCarTransform;
        [SerializeField] private Transform carSeatCameraAnchor;
        [SerializeField] private Transform exitStandingAnchor;
        [SerializeField] private float approachDistance = 15.0f;
        [SerializeField] private float phase1DriveDuration = 3.5f;
        [SerializeField] private float phase2ExitDuration = 2.5f;
        [SerializeField] private float phase3CamcorderDuration = 2.8f;

        [Header("Tensión en Ventana Superior (Opcional)")]
        [SerializeField] private Light upstairsWindowTeaseLight;

        [Header("Referencias de Escena")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private CamcorderController camcorderController;
        [SerializeField] private Volume baseVolume;

        [Header("Overlay VHS")]
        [SerializeField] private GameObject cutsceneOverlayRoot;
        [SerializeField] private Image staticFadeOverlay;
        [SerializeField] private Image vhsScanBar;
        [SerializeField] private TMP_Text statusTagText;
        [SerializeField] private TMP_Text synopsisSubtitleText;
        [SerializeField] private TMP_Text skipHintText;

        [TextArea(2, 5)]
        [SerializeField] private string arrivalSubtitle =
            "NOV. 08. 2004 — 23:05. Llegué a la cabaña. Sus autos siguen aquí... pero nadie responde.";

        private InputAction skipAction;
        private AudioSource audioSrc;
        private AudioClip sfxDriveBrake;
        private AudioClip sfxEngineOff;
        private AudioClip sfxDoorOpen;
        private AudioClip sfxDoorSlam;
        private AudioClip sfxTapeOn;

        private VideoPlayer videoPlayer;
        private RenderTexture videoRT;
        private GameObject videoCanvasOverlay;

        private DepthOfField runtimeDof;
        private FilmGrain runtimeGrain;
        private ChromaticAberration runtimeChroma;
        private bool initialDofActive;
        private float initialGrain = 0.22f;
        private float initialChroma = 0f;

        private Vector3 finalCarPos;
        private Quaternion finalCarRot;
        private Light[] carHeadlights;
        private float[] carHeadlightBaseIntensities;

        private Vector3 finalPlayerPos;
        private Quaternion finalPlayerRot;
        private Vector3 defaultPivotLocalPos;

        private bool isPlaying;
        private bool finished;

        private void Awake()
        {
            if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();
            if (camcorderController == null) camcorderController = FindAnyObjectByType<CamcorderController>();

            if (doctorCarTransform == null)
            {
                GameObject carGo = GameObject.Find("DoctorCar_Sedan");
                if (carGo != null) doctorCarTransform = carGo.transform;
            }
            if (carSeatCameraAnchor == null && doctorCarTransform != null)
            {
                carSeatCameraAnchor = doctorCarTransform.Find("CarSeatCameraAnchor");
            }

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
                GameObject v = GameObject.Find("Base_GlobalVolume");
                if (v != null) baseVolume = v.GetComponent<Volume>();
            }

            StoryObjectiveManager mgr = StoryObjectiveManager.Instance ?? FindAnyObjectByType<StoryObjectiveManager>();
            if (mgr != null) mgr.ExternalIntroControlled = true;

            skipAction = new InputAction("SkipCarCutscene", InputActionType.Button, "<Keyboard>/space");

            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;

            sfxDriveBrake = CreateDriveAndGravelBrakeClip();
            sfxEngineOff = CreateEngineShutoffClip();
            sfxDoorOpen = CreateCarDoorOpenClip();
            sfxDoorSlam = CreateCarDoorSlamClip();
            sfxTapeOn = CreateVhsPowerOnClip();
        }

        private void OnEnable() => skipAction?.Enable();
        private void OnDisable() => skipAction?.Disable();

        private void OnDestroy()
        {
            skipAction?.Dispose();
            if (sfxDriveBrake != null) Destroy(sfxDriveBrake);
            if (sfxEngineOff != null) Destroy(sfxEngineOff);
            if (sfxDoorOpen != null) Destroy(sfxDoorOpen);
            if (sfxDoorSlam != null) Destroy(sfxDoorSlam);
            if (sfxTapeOn != null) Destroy(sfxTapeOn);
            CleanupVideoResources();
        }

        private void Start()
        {
            if (doctorCarTransform != null)
            {
                finalCarPos = doctorCarTransform.position;
                finalCarRot = doctorCarTransform.rotation;
                carHeadlights = doctorCarTransform.GetComponentsInChildren<Light>(true);
                carHeadlightBaseIntensities = new float[carHeadlights.Length];
                for (int i = 0; i < carHeadlights.Length; i++)
                {
                    carHeadlightBaseIntensities[i] = carHeadlights[i].intensity;
                }
            }

            if (playerController != null)
            {
                finalPlayerPos = exitStandingAnchor != null ? exitStandingAnchor.position : playerController.transform.position;
                finalPlayerRot = exitStandingAnchor != null ? exitStandingAnchor.rotation : playerController.transform.rotation;
            }
            if (cameraPivot != null)
            {
                defaultPivotLocalPos = cameraPivot.localPosition;
            }

            EnsureOverlayReferences();
            CacheVolumeOverrides();
            BeginCutscene();
        }

        private void Update()
        {
            if (!isPlaying) return;
            if (skipAction.WasPressedThisFrame())
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
            }

            if (!baseVolume.profile.TryGet(out runtimeGrain)) runtimeGrain = baseVolume.profile.Add<FilmGrain>(true);
            runtimeGrain.intensity.overrideState = true;
            initialGrain = runtimeGrain.intensity.value;

            if (!baseVolume.profile.TryGet(out runtimeChroma)) runtimeChroma = baseVolume.profile.Add<ChromaticAberration>(true);
            runtimeChroma.intensity.overrideState = true;
            initialChroma = runtimeChroma.intensity.value;
        }

        private void BeginCutscene()
        {
            isPlaying = true;
            finished = false;

            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.ExternalIntroControlled = true;
                StoryObjectiveManager.Instance.SetExternalIntroActive(true);
            }

            if (introVideoClip != null)
            {
                StartCoroutine(PlayVideoModeRoutine());
            }
            else
            {
                StartCoroutine(PlayRealTime3DRoutine());
            }
        }

        private IEnumerator PlayVideoModeRoutine()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            videoCanvasOverlay = new GameObject("IntroVideoOverlay", typeof(RectTransform), typeof(RawImage));
            if (canvas != null) videoCanvasOverlay.transform.SetParent(canvas.transform, false);

            RectTransform rt = videoCanvasOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            videoRT = new RenderTexture(1920, 1080, 0);
            RawImage raw = videoCanvasOverlay.GetComponent<RawImage>();
            raw.texture = videoRT;
            raw.color = Color.white;

            GameObject hintGo = new GameObject("VideoSkipHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(videoCanvasOverlay.transform, false);
            RectTransform hRt = hintGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.5f, 0f);
            hRt.anchorMax = new Vector2(0.5f, 0f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.anchoredPosition = new Vector2(0f, 30f);
            hRt.sizeDelta = new Vector2(460f, 36f);
            var hTmp = hintGo.GetComponent<TextMeshProUGUI>();
            hTmp.text = "[ESPACIO] SALTAR VIDEO";
            hTmp.fontSize = 18f;
            hTmp.alignment = TextAlignmentOptions.Center;
            hTmp.color = new Color(0.92f, 0.94f, 0.90f, 0.80f);

            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.clip = introVideoClip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRT;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audioSrc);

            bool videoDone = false;
            videoPlayer.loopPointReached += _ => videoDone = true;
            videoPlayer.Prepare();

            float waitPrep = 0f;
            while (!videoPlayer.isPrepared && waitPrep < 5f && isPlaying)
            {
                waitPrep += Time.unscaledDeltaTime;
                yield return null;
            }

            if (isPlaying)
            {
                videoPlayer.Play();
                while (!videoDone && isPlaying)
                {
                    yield return null;
                }
            }

            if (!finished) FinishCutscene();
        }

        private IEnumerator PlayRealTime3DRoutine()
        {
            if (cutsceneOverlayRoot != null) cutsceneOverlayRoot.SetActive(true);
            if (skipHintText != null) skipHintText.text = "[ESPACIO] SALTAR SECUENCIA";
            if (synopsisSubtitleText != null) synopsisSubtitleText.text = "";
            if (statusTagText != null) statusTagText.text = "23:05   LLEGADA A LA CABAÑA";

            if (camcorderController != null)
            {
                camcorderController.SetCamcorderActive(false, notify: true);
            }

            if (upstairsWindowTeaseLight != null)
            {
                upstairsWindowTeaseLight.enabled = true;
            }

            CharacterController cc = playerController != null ? playerController.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            // Posicionar carro 15m atrás sobre el camino de tierra
            Vector3 driveForward = doctorCarTransform != null ? doctorCarTransform.forward : Vector3.forward;
            driveForward.y = 0f;
            if (driveForward.sqrMagnitude < 0.01f) driveForward = Vector3.forward;
            driveForward.Normalize();

            Vector3 startCarPos = finalCarPos - driveForward * approachDistance;
            if (doctorCarTransform != null)
            {
                doctorCarTransform.position = startCarPos;
                doctorCarTransform.rotation = finalCarRot;
            }

            Vector3 standingEyePos = finalPlayerPos + defaultPivotLocalPos;
            Vector3 preStepPos = standingEyePos - Vector3.forward * 1.35f;

            // =========================================================================
            // FASE 1 (~3.5s) — LLEGADA EN MOVIMIENTO CON FRENADO SOBRE GRAVA/TIERRA
            // =========================================================================
            if (audioSrc != null && sfxDriveBrake != null)
            {
                audioSrc.PlayOneShot(sfxDriveBrake, 0.76f);
            }

            float t = 0f;
            while (t < phase1DriveDuration && isPlaying)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.1f, phase1DriveDuration));
                // Desaceleración realista (ease-out cúbico)
                float easeOut = 1f - Mathf.Pow(1f - u, 3f);

                // Ligero cabeceo de frenado al detenerse (u = 0.72..1.0)
                float brakePitch = 0f;
                if (u > 0.72f)
                {
                    float bu = (u - 0.72f) / 0.28f;
                    brakePitch = Mathf.Sin(bu * Mathf.PI) * 1.9f * (1f - bu * 0.35f);
                }

                // Vibración sutil del camino de tierra (se reduce al frenar)
                float roadBump = (Mathf.PerlinNoise(t * 9.5f, 0.4f) - 0.5f) * 0.032f * (1f - u);

                if (doctorCarTransform != null)
                {
                    Vector3 curCarPos = Vector3.Lerp(startCarPos, finalCarPos, easeOut);
                    curCarPos.y = finalCarPos.y + roadBump * 0.5f;
                    doctorCarTransform.position = curCarPos;
                    doctorCarTransform.rotation = finalCarRot * Quaternion.Euler(brakePitch, 0f, 0f);
                }

                if (cameraPivot != null && carSeatCameraAnchor != null)
                {
                    cameraPivot.position = carSeatCameraAnchor.position + new Vector3(0f, roadBump * 0.4f, 0f);
                    float yaw = (doctorCarTransform != null ? doctorCarTransform.eulerAngles.y : 0f) + Mathf.Sin(u * Mathf.PI) * 3.5f;
                    cameraPivot.rotation = Quaternion.Euler(2.5f + brakePitch * 0.8f, yaw, 0f);
                }

                // Fade inicial suave desde negro (0 - 0.9s)
                if (staticFadeOverlay != null)
                {
                    Color c = staticFadeOverlay.color;
                    c.a = Mathf.Clamp01(1f - (t / 0.9f));
                    staticFadeOverlay.color = c;
                }
                if (vhsScanBar != null) vhsScanBar.enabled = false;

                yield return null;
            }

            // Asegurar carro completamente quieto en su sitio final
            if (doctorCarTransform != null)
            {
                doctorCarTransform.position = finalCarPos;
                doctorCarTransform.rotation = finalCarRot;
            }

            // =========================================================================
            // FASE 2 (~2.5s) — APAGADO DEL MOTOR, PARPADEO DE FAROS Y BAJADA DEL CARRO
            // =========================================================================
            if (audioSrc != null && sfxEngineOff != null) audioSrc.PlayOneShot(sfxEngineOff, 0.74f);

            Vector3 seatWorldPos = carSeatCameraAnchor != null ? carSeatCameraAnchor.position : finalCarPos + new Vector3(-0.4f, 1.32f, 0.15f);
            Vector3 outsideDoorPos = finalCarPos + (doctorCarTransform != null ? -doctorCarTransform.right * 1.55f : Vector3.left * 1.55f) + Vector3.up * 1.55f;

            bool doorOpenPlayed = false;
            bool doorSlamPlayed = false;
            t = 0f;
            while (t < phase2ExitDuration && isPlaying)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.1f, phase2ExitDuration));

                // Leve parpadeo de faros al apagar el motor (0.15s..0.55s)
                if (carHeadlights != null && t >= 0.12f && t <= 0.62f)
                {
                    float flick = (Mathf.PerlinNoise(t * 28f, 1.9f) > 0.55f) ? 0.45f : 0.95f;
                    SetCarHeadlightsMultiplier(flick);
                }
                else
                {
                    SetCarHeadlightsMultiplier(1f);
                }

                // Seguro y puerta abriéndose a los 0.45s
                if (!doorOpenPlayed && t >= 0.45f)
                {
                    doorOpenPlayed = true;
                    if (audioSrc != null && sfxDoorOpen != null) audioSrc.PlayOneShot(sfxDoorOpen, 0.78f);
                }

                // Portazo pesado al cerrar a los 1.95s
                if (!doorSlamPlayed && t >= 1.95f)
                {
                    doorSlamPlayed = true;
                    if (audioSrc != null && sfxDoorSlam != null) audioSrc.PlayOneShot(sfxDoorSlam, 0.85f);
                    if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.05f, 0.16f);
                }

                // Cámara sale suavemente del asiento -> costado izquierdo -> camina hacia el camino frente al porche
                if (cameraPivot != null)
                {
                    Vector3 camPos;
                    float lookYaw;
                    float lookPitch;

                    if (u < 0.45f)
                    {
                        float e1 = u / 0.45f;
                        float s1 = e1 * e1 * (3f - 2f * e1);
                        camPos = Vector3.Lerp(seatWorldPos, outsideDoorPos, s1);
                        lookYaw = Mathf.Lerp(8f, -24f, s1);
                        lookPitch = Mathf.Lerp(4f, 10f, s1);
                    }
                    else
                    {
                        float e2 = (u - 0.45f) / 0.55f;
                        float s2 = e2 * e2 * (3f - 2f * e2);
                        camPos = Vector3.Lerp(outsideDoorPos, preStepPos, s2);
                        // Head-bobbing natural al dar los primeros pasos alrededor del carro
                        float bobY = Mathf.Abs(Mathf.Sin(e2 * Mathf.PI * 3f)) * 0.055f;
                        float bobX = Mathf.Cos(e2 * Mathf.PI * 1.5f) * 0.03f;
                        camPos += new Vector3(bobX, bobY, 0f);
                        lookYaw = Mathf.Lerp(-24f, -4f, s2);
                        lookPitch = Mathf.Lerp(10f, -6f, s2);
                    }

                    cameraPivot.position = camPos;
                    cameraPivot.rotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
                }

                yield return null;
            }

            SetCarHeadlightsMultiplier(1f);

            // =========================================================================
            // FASE 3 (~2.8s) — PASOS HACIA LA CABAÑA, LUZ EN VENTANA SUPERIOR Y VHS ON
            // =========================================================================
            bool camOnTriggered = false;
            bool windowLightSnappedOff = false;
            t = 0f;
            while (t < phase3CamcorderDuration && isPlaying)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.1f, phase3CamcorderDuration));

                // Encendido de videocámara VHS a los 0.35s
                if (!camOnTriggered && t >= 0.35f)
                {
                    camOnTriggered = true;
                    if (audioSrc != null && sfxTapeOn != null) audioSrc.PlayOneShot(sfxTapeOn, 0.78f);
                    if (camcorderController != null)
                    {
                        camcorderController.SetCamcorderActive(true, notify: true);
                        camcorderController.SetLampActive(true, notify: true);
                    }
                    if (statusTagText != null) statusTagText.text = "● REC   CINTA #01 — CABAÑA";
                }

                // Luz de la ventana del segundo piso se apaga de golpe justo cuando mira arriba (0.85s)
                if (!windowLightSnappedOff && t >= 0.85f)
                {
                    windowLightSnappedOff = true;
                    if (upstairsWindowTeaseLight != null) upstairsWindowTeaseLight.enabled = false;
                }

                // Par de pasos suaves hacia la cabaña mirando arriba a las ventanas oscuras y luego al porche
                if (cameraPivot != null)
                {
                    float walkU = Mathf.Clamp01(u / 0.75f);
                    float smoothWalk = walkU * walkU * (3f - 2f * walkU);
                    Vector3 pos = Vector3.Lerp(preStepPos, standingEyePos, smoothWalk);
                    float stepBob = (walkU < 1f) ? Mathf.Abs(Mathf.Sin(walkU * Mathf.PI * 2.5f)) * 0.045f : 0f;
                    pos.y += stepBob;

                    // Mira hacia las ventanas superiores (-18°) y baja hacia la entrada principal (0°)
                    float pitchCurve = u < 0.45f
                        ? Mathf.Lerp(-6f, -18f, u / 0.45f)
                        : Mathf.Lerp(-18f, 0f, (u - 0.45f) / 0.55f);
                    float yawCurve = Mathf.Lerp(-4f, 0f, u);

                    cameraPivot.position = pos;
                    cameraPivot.rotation = Quaternion.Euler(pitchCurve, yawCurve, 0f);
                }

                // Estática VHS + enfoque rápido URP al encender cámara
                float vhsBoot = Mathf.Clamp01((t - 0.35f) / 1.05f);
                if (t >= 0.35f)
                {
                    if (staticFadeOverlay != null)
                    {
                        Color c = staticFadeOverlay.color;
                        c.a = (1f - vhsBoot) * 0.72f * (0.65f + 0.35f * Mathf.PerlinNoise(t * 42f, 0.5f));
                        staticFadeOverlay.color = c;
                    }
                    if (vhsScanBar != null)
                    {
                        bool showBar = vhsBoot < 0.95f;
                        vhsScanBar.enabled = showBar;
                        if (showBar)
                        {
                            RectTransform rt = vhsScanBar.rectTransform;
                            float y = Mathf.Repeat(t * 1.25f, 1f);
                            rt.anchorMin = new Vector2(0f, y);
                            rt.anchorMax = new Vector2(1f, y);
                            rt.sizeDelta = new Vector2(0f, Mathf.Lerp(26f, 8f, vhsBoot));
                        }
                    }
                    if (runtimeDof != null)
                    {
                        bool blur = vhsBoot < 0.85f;
                        runtimeDof.active = blur || initialDofActive;
                        if (blur)
                        {
                            runtimeDof.mode.Override(DepthOfFieldMode.Gaussian);
                            runtimeDof.gaussianStart.Override(Mathf.Lerp(0.3f, 8f, vhsBoot));
                            runtimeDof.gaussianEnd.Override(Mathf.Lerp(2.2f, 20f, vhsBoot));
                            runtimeDof.gaussianMaxRadius.Override(Mathf.Lerp(1.1f, 0.1f, vhsBoot));
                        }
                    }
                    if (runtimeGrain != null) runtimeGrain.intensity.value = Mathf.Lerp(0.95f, initialGrain, vhsBoot);
                    if (runtimeChroma != null) runtimeChroma.intensity.value = Mathf.Lerp(0.62f, initialChroma, vhsBoot);
                }

                // Subtítulo sincronizado de fecha/sinopsis
                if (synopsisSubtitleText != null && t >= 0.45f)
                {
                    float typeT = Mathf.Clamp01((t - 0.45f) / 1.85f);
                    int len = Mathf.Clamp(Mathf.FloorToInt(arrivalSubtitle.Length * typeT), 0, arrivalSubtitle.Length);
                    synopsisSubtitleText.text = arrivalSubtitle.Substring(0, len);
                }

                yield return null;
            }

            if (cc != null) cc.enabled = true;
            if (!finished) FinishCutscene();
        }

        public void FinishCutscene()
        {
            if (finished) return;
            finished = true;
            isPlaying = false;

            StopAllCoroutines();
            if (audioSrc != null) audioSrc.Stop();
            CleanupVideoResources();

            // Garantizar carro estacionado en su posición final exacta con faros restaurados
            if (doctorCarTransform != null)
            {
                doctorCarTransform.position = finalCarPos;
                doctorCarTransform.rotation = finalCarRot;
            }
            SetCarHeadlightsMultiplier(1f);

            if (upstairsWindowTeaseLight != null)
            {
                upstairsWindowTeaseLight.enabled = false;
            }

            if (cutsceneOverlayRoot != null) cutsceneOverlayRoot.SetActive(false);
            if (runtimeDof != null) runtimeDof.active = initialDofActive;
            if (runtimeGrain != null) runtimeGrain.intensity.value = initialGrain;
            if (runtimeChroma != null) runtimeChroma.intensity.value = initialChroma;

            if (playerController != null)
            {
                CharacterController cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = finalPlayerPos;
                playerController.transform.rotation = finalPlayerRot;
                if (cc != null) cc.enabled = true;
            }

            if (cameraPivot != null)
            {
                cameraPivot.localPosition = defaultPivotLocalPos;
                cameraPivot.localRotation = Quaternion.identity;
            }

            if (playerController != null)
            {
                playerController.SyncLookAnglesFromTransform();
            }

            if (camcorderController != null)
            {
                camcorderController.SetCamcorderActive(true, notify: true);
                camcorderController.SetLampActive(true, notify: true);
            }

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

        private void SetCarHeadlightsMultiplier(float mult)
        {
            if (carHeadlights == null || carHeadlightBaseIntensities == null) return;
            for (int i = 0; i < carHeadlights.Length; i++)
            {
                if (carHeadlights[i] != null && carHeadlights[i].type == LightType.Spot)
                {
                    carHeadlights[i].intensity = carHeadlightBaseIntensities[i] * mult;
                }
            }
        }

        private void CleanupVideoResources()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                Destroy(videoPlayer);
                videoPlayer = null;
            }
            if (videoCanvasOverlay != null)
            {
                Destroy(videoCanvasOverlay);
                videoCanvasOverlay = null;
            }
            if (videoRT != null)
            {
                videoRT.Release();
                Destroy(videoRT);
                videoRT = null;
            }
        }

        private static AudioClip CreateDriveAndGravelBrakeClip()
        {
            const int sr = 22050;
            const float dur = 3.55f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 551122u;
            float lp = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float u = t / dur;
                // Motor bajando de RPM suavemente (92 Hz -> 44 Hz)
                float rpm = Mathf.Lerp(92f, 44f, u * u);
                phase += (2f * Mathf.PI * rpm) / sr;
                float engine = Mathf.Sin(phase) * 0.34f + Mathf.Sin(phase * 2f) * 0.16f + Mathf.Sin(phase * 0.5f) * 0.12f;

                // Grava / tierra bajo neumáticos (más audible durante la frenada u = 0.5..0.95)
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.72f + w * 0.28f;
                float gravelEnv = (u < 0.96f) ? Mathf.Lerp(0.30f, 0.75f, Mathf.Sin(Mathf.Clamp01(u / 0.96f) * Mathf.PI)) : 0f;
                float masterEnv = Mathf.Clamp01((1f - u) * 5.5f + 0.25f) * Mathf.Clamp01(u * 12f);

                data[i] = Mathf.Clamp((engine + lp * gravelEnv * 0.55f) * masterEnv, -0.92f, 0.92f);
            }
            AudioClip c = AudioClip.Create("SFX_CarDriveAndBrakeGravel", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateEngineShutoffClip()
        {
            const int sr = 22050;
            const float dur = 1.15f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 123789u;
            float lp = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float norm = t / dur;
                float rpmFreq = Mathf.Lerp(46f, 12f, norm * norm);
                phase += (2f * Mathf.PI * rpmFreq) / sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.90f + w * 0.10f;
                float env = Mathf.Clamp01(1f - Mathf.Pow(norm, 1.5f));
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.45f + lp * 0.35f) * env, -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("SFX_CarEngineOff", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateCarDoorOpenClip()
        {
            const int sr = 22050;
            const float dur = 0.55f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 224466u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float latch = Mathf.Exp(-t * 46f) * (w * 0.65f + Mathf.Sin(2f * Mathf.PI * 580f * t) * 0.28f);
                float hinge = (t > 0.12f) ? Mathf.Sin(2f * Mathf.PI * 220f * t) * Mathf.Exp(-(t - 0.12f) * 8f) * 0.14f : 0f;
                data[i] = Mathf.Clamp(latch + hinge, -0.92f, 0.92f);
            }
            AudioClip c = AudioClip.Create("SFX_CarDoorOpen", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateCarDoorSlamClip()
        {
            const int sr = 22050;
            const float dur = 0.55f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 334455u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float slam = Mathf.Exp(-t * 20f) * (Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.88f + w * 0.52f);
                data[i] = Mathf.Clamp(slam, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("SFX_CarDoorSlam", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateVhsPowerOnClip()
        {
            const int sr = 22050;
            const float dur = 0.75f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 987123u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float click = Mathf.Exp(-t * 55f) * w * 0.65f;
                float beep = (t >= 0.20f && t <= 0.32f) ? Mathf.Sin(2f * Mathf.PI * 1480f * t) * 0.30f : 0f;
                data[i] = Mathf.Clamp(click + beep + w * 0.14f * Mathf.Exp(-t * 2.5f), -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("SFX_CamcorderOn", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
