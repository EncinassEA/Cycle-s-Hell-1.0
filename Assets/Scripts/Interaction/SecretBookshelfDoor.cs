using UnityEngine;
using CyclesHell.Camcorder;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Librero antiguo / puerta secreta en el Estudio de planta baja.
    /// Antes de accionar la palanca en la Recámara Principal muestra un aviso de que parece hueco detrás.
    /// Al activarse el mecanismo, se desliza suavemente revelando las escaleras de concreto hacia el subsuelo.
    /// </summary>
    public class SecretBookshelfDoor : MonoBehaviour, IInteractable
    {
        [Header("Movimiento de Apertura")]
        [SerializeField] private Transform slidingRoot;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(-2.15f, 0f, 0f);
        [SerializeField] private float slideSpeed = 1.6f;

        [Header("Textos")]
        [SerializeField] private string lockedPrompt = "[E] Examinar Librero Antiguo";
        [SerializeField] private string lockedHintMessage = "Parece hueco detrás... debe haber un mecanismo o palanca cerca.";

        private Vector3 closedLocalPos;
        private Vector3 targetLocalPos;
        private bool isOpen;

        private AudioSource mechanismAudio;
        private AudioClip rumbleClip;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (slidingRoot == null) slidingRoot = transform;
            closedLocalPos = slidingRoot.localPosition;
            targetLocalPos = closedLocalPos;

            mechanismAudio = gameObject.AddComponent<AudioSource>();
            mechanismAudio.playOnAwake = false;
            mechanismAudio.loop = false;
            mechanismAudio.spatialBlend = 0.65f;
            mechanismAudio.minDistance = 4f;
            mechanismAudio.maxDistance = 30f;

            rumbleClip = CreateHeavyRumbleClip();
        }

        private void OnDestroy()
        {
            if (rumbleClip != null) Destroy(rumbleClip);
        }

        private void Update()
        {
            if (slidingRoot == null) return;
            if ((slidingRoot.localPosition - targetLocalPos).sqrMagnitude > 0.0001f)
            {
                slidingRoot.localPosition = Vector3.MoveTowards(
                    slidingRoot.localPosition,
                    targetLocalPos,
                    slideSpeed * Time.deltaTime);
            }
        }

        public string GetInteractionPrompt(PlayerInteraction interactor) => lockedPrompt;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null && !isOpen;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null || isOpen) return;
            interactor.ShowStatusMessage(lockedHintMessage);
        }

        public void OpenSecretPassage()
        {
            if (isOpen) return;
            isOpen = true;
            targetLocalPos = closedLocalPos + openLocalOffset;

            if (mechanismAudio != null && rumbleClip != null)
            {
                mechanismAudio.PlayOneShot(rumbleClip, 0.85f);
            }

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.14f, 0.85f);
            }
        }

        private static AudioClip CreateHeavyRumbleClip()
        {
            const int sr = 22050;
            const float dur = 2.0f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 778899u;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                lp = lp * 0.90f + w * 0.10f;
                float grind = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.45f +
                              Mathf.Sin(2f * Mathf.PI * 69f * t) * 0.25f;
                float latch = (t < 0.14f) ? Mathf.Exp(-t * 32f) * w * 0.85f : 0f;
                data[i] = Mathf.Clamp((grind + lp * 1.4f) * env + latch, -0.95f, 0.95f);
            }
            AudioClip clip = AudioClip.Create("SFX_HeavySecretMechanism", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
