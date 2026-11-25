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
    ///   reproduce el video a pantalla completa con audio y al terminar entrega el control frente al carro.
    /// - MODO 3D EN TIEMPO REAL (activo por defecto):
    ///   1) Cámara dentro/junto al coche estacionado mirando la cabaña oscura iluminada por los faros en la niebla (3s) + motor apagándose.
    ///   2) Sonido de puerta de coche abriendo/cerrando; la cámara baja del vehículo, mira los árboles y la cabaña.
    ///   3) Enciende la videocámara VHS con estática y subtítulo:
    ///      "NOV. 08. 2004 — 23:05. Llegué a la cabaña. Sus autos siguen aquí... pero nadie responde."
    ///   4) Entrega control fluido al jugador frente al porche.
    /// 100% Unity 6, URP y exclusivamente UnityEngine.InputSystem.
    /// </summary>
    public class CarArrivalCutscene : MonoBehaviour
    {
        [Header("Modo Video Opcional (Arrastra aquí Assets/Video/IntroCoche.mp4)")]
        [SerializeField] private VideoClip introVideoClip;

        [Header("Modo 3D en Tiempo Real")]
        [SerializeField] private Transform carSeatCameraAnchor;
        [SerializeField] private Transform exitStandingAnchor;
        [SerializeField] private float totalRealTimeDuration = 9.4f;

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
        private AudioClip sfxEngineOff;
        private AudioClip sfxCarDoor;
        private AudioClip sfxTapeOn;

        private VideoPlayer videoPlayer;
        private RenderTexture videoRT;
        private GameObject videoCanvasOverlay;

        private FilmGrain runtimeGrain;
        private ChromaticAberration runtimeChroma;
        private float initialGrain = 0.22f;
        private float initialChroma = 0f;

        private Vector3 finalPlayerPos;
        private Quaternion finalPlayerRot;
        private Vector3 defaultPivotLocalPos;

        private bool isPlaying;
        private bool finished;

        private void Awake()
        {
            if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();
            if (camcorderController == null) camcorderController = FindAnyObjectByType<CamcorderController>();

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

            // Tomar control de la intro antes de StoryObjectiveManager.Start()
            StoryObjectiveManager mgr = StoryObjectiveManager.Instance ?? FindAnyObjectByType<StoryObjectiveManager>();
            if (mgr != null) mgr.ExternalIntroControlled = true;

            skipAction = new InputAction("SkipCarCutscene", InputActionType.Button, "<Keyboard>/space");

            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;

            sfxEngineOff = CreateEngineShutoffClip();
            sfxCarDoor = CreateCarDoorClip();
            sfxTapeOn = CreateVhsPowerOnClip();
        }

        private void OnEnable() => skipAction?.Enable();
        private void OnDisable() => skipAction?.Disable();

        private void OnDestroy()
        {
            skipAction?.Dispose();
            if (sfxEngineOff != null) Destroy(sfxEngineOff);
            if (sfxCarDoor != null) Destroy(sfxCarDoor);
            if (sfxTapeOn != null) Destroy(sfxTapeOn);
            CleanupVideoResources();
        }

        private void Start()
        {
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

            // Etiqueta discreta de saltar con Espacio
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

            CharacterController cc = playerController != null ? playerController.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            Vector3 seatPos = carSeatCameraAnchor != null ? carSeatCameraAnchor.position : finalPlayerPos + new Vector3(-1.4f, 1.25f, -4.5f);
            Vector3 standingEyePos = finalPlayerPos + defaultPivotLocalPos;

            // 1) Motor apagándose al inicio (0s)
            if (audioSrc != null && sfxEngineOff != null) audioSrc.PlayOneShot(sfxEngineOff, 0.72f);

            bool carDoorPlayed = false;
            bool camPowerPlayed = false;
            float elapsed = 0f;

            while (elapsed < totalRealTimeDuration && isPlaying)
            {
                elapsed += Time.deltaTime;

                // Fade inicial suave desde negro (0 - 1.1s)
                if (staticFadeOverlay != null)
                {
                    float alpha = 0f;
                    if (elapsed < 1.1f) alpha = 1f - Mathf.Clamp01(elapsed / 1.1f);
                    else if (elapsed >= 6.0f && elapsed <= 7.0f)
                    {
                        // Estática al encender la videocámara VHS
                        float boot = (elapsed - 6.0f) / 1.0f;
                        alpha = (1f - boot) * 0.75f * (0.6f + 0.4f * Mathf.PerlinNoise(elapsed * 40f, 0.7f));
                    }
                    Color c = staticFadeOverlay.color;
                    c.a = Mathf.Clamp01(alpha);
                    staticFadeOverlay.color = c;
                }

                // Barra de estática VHS al encender la cámara (6.0s+)
                if (vhsScanBar != null)
                {
                    bool showBar = elapsed >= 6.0f && elapsed < 7.6f;
                    vhsScanBar.enabled = showBar;
                    if (showBar)
                    {
                        RectTransform rt = vhsScanBar.rectTransform;
                        float y = Mathf.Repeat(elapsed * 1.1f, 1f);
                        rt.anchorMin = new Vector2(0f, y);
                        rt.anchorMax = new Vector2(1f, y);
                        rt.sizeDelta = new Vector2(0f, 20f);
                    }
                }

                // Fase 1 (0 - 3.0s): Sentado en el carro viendo la cabaña en la niebla
                if (elapsed < 3.0f)
                {
                    float t1 = elapsed / 3.0f;
                    if (cameraPivot != null)
                    {
                        cameraPivot.position = seatPos + new Vector3(0f, Mathf.Sin(elapsed * 1.4f) * 0.01f, 0f);
                        float yaw = Mathf.Lerp(4f, 10f, t1);
                        float pitch = Mathf.Lerp(3f, -2f, t1);
                        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
                    }
                }
                // Fase 2 (3.0 - 6.0s): Puerta del carro abre/cierra, baja del vehículo y observa bosque y cabaña
                else if (elapsed < 6.0f)
                {
                    if (!carDoorPlayed)
                    {
                        carDoorPlayed = true;
                        if (audioSrc != null && sfxCarDoor != null) audioSrc.PlayOneShot(sfxCarDoor, 0.80f);
                    }

                    float t2 = (elapsed - 3.0f) / 3.0f;
                    float smooth = t2 * t2 * (3f - 2f * t2);
                    Vector3 midPos = Vector3.Lerp(seatPos, standingEyePos, smooth);
                    midPos.x -= Mathf.Sin(smooth * Mathf.PI) * 0.9f; // Sale por la puerta del conductor
                    midPos.y += Mathf.Abs(Mathf.Sin(t2 * Mathf.PI * 3.5f)) * 0.045f; // Pasos breves

                    // Giro curioso mirando los árboles del bosque a la izquierda y volviendo hacia la cabaña
                    float lookYaw = Mathf.Sin(smooth * Mathf.PI) * -28f;
                    float lookPitch = Mathf.Lerp(5f, -3f, smooth);

                    if (cameraPivot != null)
                    {
                        cameraPivot.position = midPos;
                        cameraPivot.rotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
                    }
                }
                // Fase 3 (6.0 - 9.4s): Enciende videocámara VHS con estática y subtítulo de llegada
                else
                {
                    if (!camPowerPlayed)
                    {
                        camPowerPlayed = true;
                        if (audioSrc != null && sfxTapeOn != null) audioSrc.PlayOneShot(sfxTapeOn, 0.75f);
                        if (camcorderController != null)
                        {
                            camcorderController.SetCamcorderActive(true, notify: true);
                            camcorderController.SetLampActive(true, notify: true);
                        }
                        if (statusTagText != null) statusTagText.text = "● REC   CINTA #01 — CABAÑA";
                    }

                    float t3 = Mathf.Clamp01((elapsed - 6.0f) / 3.4f);
                    if (cameraPivot != null)
                    {
                        float swayX = (Mathf.PerlinNoise(elapsed * 1.4f, 0.3f) - 0.5f) * 0.025f;
                        float swayY = (Mathf.PerlinNoise(0.7f, elapsed * 1.6f) - 0.5f) * 0.022f;
                        cameraPivot.position = standingEyePos + new Vector3(swayX, swayY, 0f);
                        float yaw = Mathf.Lerp(-8f, 0f, t3);
                        float pitch = Mathf.Lerp(6f, 0f, t3);
                        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
                    }

                    if (runtimeGrain != null)
                        runtimeGrain.intensity.value = Mathf.Lerp(0.92f, initialGrain, Mathf.Clamp01(t3 * 1.8f));
                    if (runtimeChroma != null)
                        runtimeChroma.intensity.value = Mathf.Lerp(0.58f, initialChroma, Mathf.Clamp01(t3 * 1.8f));

                    if (synopsisSubtitleText != null)
                    {
                        float typeT = Mathf.Clamp01((elapsed - 6.2f) / 2.2f);
                        int len = Mathf.Clamp(Mathf.FloorToInt(arrivalSubtitle.Length * typeT), 0, arrivalSubtitle.Length);
                        synopsisSubtitleText.text = arrivalSubtitle.Substring(0, len);
                    }
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

            if (cutsceneOverlayRoot != null) cutsceneOverlayRoot.SetActive(false);
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

        private static AudioClip CreateEngineShutoffClip()
        {
            const int sr = 22050;
            const float dur = 2.4f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 123789u;
            float lp = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float norm = t / dur;
                float rpmFreq = Mathf.Lerp(54f, 14f, norm * norm);
                phase += (2f * Mathf.PI * rpmFreq) / sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.90f + w * 0.10f;
                float env = Mathf.Clamp01(1f - Mathf.Pow(norm, 1.8f));
                float sputter = (norm > 0.72f) ? (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 14f * t)) : 1f;
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.45f + Mathf.Sin(phase * 2f) * 0.22f + lp * 0.45f) * env * sputter, -0.9f, 0.9f);
            }
            AudioClip c = AudioClip.Create("SFX_CarEngineOff", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateCarDoorClip()
        {
            const int sr = 22050;
            const float dur = 1.65f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 334455u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;

                // Click de manija/apertura a 0.05s + golpe pesado de cierre a 1.15s
                float openLatch = Mathf.Exp(-Mathf.Max(0f, t - 0.05f) * 45f) * (t >= 0.05f ? w * 0.6f + Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.25f : 0f);
                float tClose = t - 1.15f;
                float slam = (tClose >= 0f) ? Mathf.Exp(-tClose * 20f) * (Mathf.Sin(2f * Mathf.PI * 62f * tClose) * 0.85f + w * 0.52f) : 0f;
                data[i] = Mathf.Clamp(openLatch + slam, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("SFX_CarDoorOpenClose", n, 1, sr, false);
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
