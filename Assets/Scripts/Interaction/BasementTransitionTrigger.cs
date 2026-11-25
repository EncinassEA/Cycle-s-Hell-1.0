using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CyclesHell.Player;
using CyclesHell.Camcorder;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Compuerta metálica / trigger al fondo de las escaleras secretas del Estudio.
    /// Muestra una transición oscura estilo VHS ("ACCEDIENDO AL SUBSUELO... NIVEL -1")
    /// y carga automáticamente "Nivel_PruebaCamara" (el laboratorio subterráneo).
    /// </summary>
    public class BasementTransitionTrigger : MonoBehaviour, IInteractable
    {
        [Header("Destino")]
        [SerializeField] private string targetSceneName = "Nivel_PruebaCamara";
        [SerializeField] private string promptText = "[E] Descender al Subsuelo (Nivel -1)";
        [SerializeField] private string transitionTitle = "ACCEDIENDO AL SUBSUELO... NIVEL -1";
        [SerializeField] private string transitionSubtitle = "CINTA #01 — GRABACIÓN CONTINUA  |  NOV. 08. 2004";
        [SerializeField] private float transitionDuration = 2.3f;
        [SerializeField] private bool triggerOnEnter = true;

        private bool isTransitioning;

        public string GetInteractionPrompt(PlayerInteraction interactor) => promptText;

        public bool CanInteract(PlayerInteraction interactor) => interactor != null && !isTransitioning;

        public void Interact(PlayerInteraction interactor)
        {
            if (isTransitioning) return;
            StartTransition();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter || isTransitioning) return;
            if (other.CompareTag("Player"))
            {
                StartTransition();
            }
        }

        public void StartTransition()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc != null) fpc.SetControlPaused(true, true, true);

            CamcorderController cam = FindAnyObjectByType<CamcorderController>();
            if (cam != null) cam.InputEnabled = false;

            // Crear overlay VHS oscuro en tiempo real
            Canvas canvas = FindAnyObjectByType<Canvas>();
            GameObject overlayGo = new GameObject("BasementTransitionOverlay", typeof(RectTransform), typeof(Image));
            if (canvas != null)
            {
                overlayGo.transform.SetParent(canvas.transform, false);
            }
            RectTransform rt = overlayGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = overlayGo.GetComponent<Image>();
            bg.color = new Color(0.01f, 0.015f, 0.01f, 0f);
            bg.raycastTarget = true;

            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(overlayGo.transform, false);
            RectTransform tRt = titleGo.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.1f, 0.48f);
            tRt.anchorMax = new Vector2(0.9f, 0.62f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            var tTmp = titleGo.GetComponent<TextMeshProUGUI>();
            tTmp.text = transitionTitle;
            tTmp.fontSize = 36f;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.color = new Color(0.92f, 0.96f, 0.90f, 0f);

            GameObject subGo = new GameObject("Sub", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(overlayGo.transform, false);
            RectTransform sRt = subGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.1f, 0.38f);
            sRt.anchorMax = new Vector2(0.9f, 0.48f);
            sRt.offsetMin = Vector2.zero;
            sRt.offsetMax = Vector2.zero;
            var sTmp = subGo.GetComponent<TextMeshProUGUI>();
            sTmp.text = transitionSubtitle;
            sTmp.fontSize = 20f;
            sTmp.alignment = TextAlignmentOptions.Center;
            sTmp.color = new Color(0.68f, 0.78f, 0.70f, 0f);

            float elapsed = 0f;
            float fadeTime = 0.75f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeTime);
                bg.color = new Color(0.01f, 0.015f, 0.01f, alpha);
                float txtAlpha = Mathf.Clamp01((elapsed - 0.25f) / 0.55f);
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(elapsed * 20f, 1.3f);
                tTmp.color = new Color(0.92f, 0.96f, 0.90f, txtAlpha * flicker);
                sTmp.color = new Color(0.68f, 0.78f, 0.70f, txtAlpha * 0.85f);
                yield return null;
            }

            Time.timeScale = 1f;
            if (!string.IsNullOrWhiteSpace(targetSceneName) && Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogError($"[BasementTransitionTrigger] La escena '{targetSceneName}' no está en Build Settings.");
            }
        }
    }
}
