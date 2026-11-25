using UnityEngine;
using CyclesHell.UI;
using CyclesHell.Audio;
using CyclesHell.Camcorder;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Palanca/mecanismo oculto en la Recámara Principal (planta alta).
    /// Al activarlo con [E]: abre el librero secreto del Estudio abajo, actualiza el objetivo del HUD
    /// y dispara un susto sonoro (golpe fuerte en las escaleras/puerta de abajo).
    /// </summary>
    public class SecretLeverInteractable : MonoBehaviour, IInteractable
    {
        [Header("Referencias")]
        [SerializeField] private SecretBookshelfDoor secretBookshelf;
        [SerializeField] private Transform leverHandlePivot;
        [SerializeField] private AudioSource stairsBangSource;

        [Header("Textos")]
        [SerializeField] private string promptText = "[E] Accionar Palanca Oculta";
        [SerializeField] private string activatedObjective = "Objetivo: Se abrió un paso secreto en el Estudio. Baja a investigar";
        [SerializeField] private string statusToast = "Un mecanismo pesado se abrió abajo... ¡y algo golpeó las escaleras!";

        private bool activated;
        private Quaternion startRot;
        private Quaternion endRot;
        private AudioClip sfxLeverClick;
        private AudioClip sfxStairsBang;

        private void Awake()
        {
            if (secretBookshelf == null)
            {
                secretBookshelf = FindAnyObjectByType<SecretBookshelfDoor>();
            }

            if (leverHandlePivot != null)
            {
                startRot = leverHandlePivot.localRotation;
                endRot = startRot * Quaternion.Euler(90f, 0f, 0f);
            }

            sfxLeverClick = CreateLeverClickClip();
            sfxStairsBang = CreateStairsBangClip();
        }

        private void OnDestroy()
        {
            if (sfxLeverClick != null) Destroy(sfxLeverClick);
            if (sfxStairsBang != null) Destroy(sfxStairsBang);
        }

        private void Update()
        {
            if (!activated || leverHandlePivot == null) return;
            leverHandlePivot.localRotation = Quaternion.Slerp(
                leverHandlePivot.localRotation,
                endRot,
                1f - Mathf.Exp(-7f * Time.deltaTime));
        }

        public string GetInteractionPrompt(PlayerInteraction interactor) => promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null && !activated;

        public void Interact(PlayerInteraction interactor)
        {
            if (interactor == null || activated) return;
            activated = true;

            AudioSource.PlayClipAtPoint(sfxLeverClick, transform.position, 0.8f);

            if (secretBookshelf != null)
            {
                secretBookshelf.OpenSecretPassage();
            }

            if (StoryObjectiveManager.Instance != null)
            {
                StoryObjectiveManager.Instance.SetCustomObjectiveOverride(
                    activatedObjective,
                    new Color(1.0f, 0.84f, 0.42f, 0.96f));
            }

            interactor.ShowStatusMessage(statusToast);

            // Susto psicológico #2: golpe fuerte viniendo de las escaleras de abajo tras 0.75s
            Invoke(nameof(PlayStairsScareBang), 0.75f);
        }

        private void PlayStairsScareBang()
        {
            if (stairsBangSource != null && sfxStairsBang != null)
            {
                stairsBangSource.PlayOneShot(sfxStairsBang, 0.95f);
            }
            else if (sfxStairsBang != null)
            {
                AudioSource.PlayClipAtPoint(sfxStairsBang, transform.position + Vector3.down * 3.5f, 0.9f);
            }

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.22f, 0.42f);
            }

            if (HorrorAudioManager.Instance != null)
            {
                HorrorAudioManager.Instance.PlayJumpscareStinger();
            }
        }

        private static AudioClip CreateLeverClickClip()
        {
            const int sr = 22050;
            const float dur = 0.42f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 54321u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float clack = Mathf.Exp(-t * 48f) * w * 0.8f;
                float ring = Mathf.Sin(2f * Mathf.PI * 640f * t) * Mathf.Exp(-t * 14f) * 0.35f;
                data[i] = Mathf.Clamp(clack + ring, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("SFX_LeverClick", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateStairsBangClip()
        {
            const int sr = 22050;
            const float dur = 1.15f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 998877u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;

                // Dos golpes secos violentos en madera + eco sordo
                float hit1 = Mathf.Exp(-t * 18f) * (Mathf.Sin(2f * Mathf.PI * 68f * t) * 0.85f + w * 0.5f);
                float t2 = Mathf.Max(0f, t - 0.28f);
                float hit2 = (t >= 0.28f) ? Mathf.Exp(-t2 * 16f) * (Mathf.Sin(2f * Mathf.PI * 58f * t2) * 0.9f + w * 0.55f) : 0f;
                data[i] = Mathf.Clamp(hit1 + hit2, -0.98f, 0.98f);
            }
            AudioClip c = AudioClip.Create("SFX_StairsDoorBang", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
