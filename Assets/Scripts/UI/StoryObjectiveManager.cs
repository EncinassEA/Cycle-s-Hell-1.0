using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CyclesHell.Player;
using CyclesHell.Camcorder;
using CyclesHell.Survival;
using CyclesHell.Interaction;
using CyclesHell.Audio;

namespace CyclesHell.UI
{
    /// <summary>
    /// Gestiona la introducción VHS de sinopsis (con máquina de escribir y salto con Espacio/E),
    /// el progreso de las 4 pistas de los compañeros desaparecidos, el objetivo en HUD
    /// y la pantalla final de victoria ("CINTA RECUPERADA - SOBREVIVISTE").
    /// Usa exclusivamente UnityEngine.InputSystem.
    /// </summary>
    public class StoryObjectiveManager : MonoBehaviour
    {
        public static StoryObjectiveManager Instance { get; private set; }

        [Header("Referencias del Jugador")]
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private CamcorderController camcorderController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInteraction playerInteraction;

        [Header("Intro VHS de Sinopsis")]
        [SerializeField] private GameObject introPanelRoot;
        [SerializeField] private TMP_Text introBodyText;
        [SerializeField] private float charsPerSecond = 44f;
        [SerializeField] private float autoCloseDelayAfterTyping = 4.5f;

        [TextArea(4, 10)]
        [SerializeField] private string synopsisText =
            "NOV. 08. 2004 — Tus compañeros se adelantaron sin ti a investigar la cabaña. " +
            "Sin señal ni rastros de vida, decides entrar a buscarlos...\n\n" +
            "Bajo la casa descubres un laboratorio subterráneo de experimentos.";

        [Header("HUD de Objetivos")]
        [SerializeField] private TMP_Text objectiveHudText;
        [SerializeField] private int requiredClues = 5;

        [Header("Pantalla de Fin de Nivel (Victoria)")]
        [SerializeField] private GameObject victoryPanelRoot;
        [SerializeField] private UnityEngine.UI.Button victoryMainMenuButton;
        [SerializeField] private string mainMenuSceneName = "SampleScene";

        private readonly HashSet<string> collectedClueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private InputAction skipIntroAction;

        private bool isIntroActive;
        private bool isExternalIntroActive;
        private bool isVictoryActive;
        private float typingProgress;
        private float postTypingTimer;
        private int lastIntroClosedFrame = -1;

        public bool ExternalIntroControlled { get; set; }
        public bool IsIntroActive => isIntroActive || isExternalIntroActive;
        public bool IsVictoryActive => isVictoryActive;
        public int LastIntroClosedFrame => lastIntroClosedFrame;
        public int CluesFound => collectedClueIds.Count;
        public int RequiredClues => requiredClues;
        public bool HasAllClues => collectedClueIds.Count >= requiredClues;
        public string SynopsisText => synopsisText;

        public event Action OnAllCluesCollected;

        private void Awake()
        {
            Instance = this;

            if (firstPersonController == null) firstPersonController = FindAnyObjectByType<FirstPersonController>();
            if (camcorderController == null) camcorderController = FindAnyObjectByType<CamcorderController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerInventory == null) playerInventory = FindAnyObjectByType<PlayerInventory>();
            if (playerInteraction == null) playerInteraction = FindAnyObjectByType<PlayerInteraction>();

            skipIntroAction = new InputAction("SkipIntro", InputActionType.Button);
            skipIntroAction.AddBinding("<Keyboard>/space");
            skipIntroAction.AddBinding("<Keyboard>/e");
        }

        private void OnEnable()
        {
            skipIntroAction?.Enable();
            if (victoryMainMenuButton != null)
            {
                victoryMainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void OnDisable()
        {
            skipIntroAction?.Disable();
            if (victoryMainMenuButton != null)
            {
                victoryMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            skipIntroAction?.Dispose();
        }

        private void Start()
        {
            if (victoryPanelRoot != null) victoryPanelRoot.SetActive(false);
            UpdateObjectiveHud();

            if (ExternalIntroControlled)
            {
                if (introPanelRoot != null) introPanelRoot.SetActive(false);
                return;
            }

            if (introPanelRoot != null)
            {
                StartIntro();
            }
        }

        public void SetExternalIntroActive(bool active)
        {
            isExternalIntroActive = active;
            if (!active)
            {
                lastIntroClosedFrame = Time.frameCount;
            }
            if (introPanelRoot != null && active)
            {
                introPanelRoot.SetActive(false);
            }
            SetGameplayControlsBlocked(active, unlockCursor: false);
        }

        private void Update()
        {
            if (!isIntroActive) return;

            if (skipIntroAction.WasPressedThisFrame())
            {
                if (typingProgress < synopsisText.Length - 1)
                {
                    // Primer toque muestra todo el texto; segundo toque cierra la intro
                    typingProgress = synopsisText.Length;
                    if (introBodyText != null) introBodyText.text = synopsisText;
                }
                else
                {
                    CloseIntro();
                }
                return;
            }

            if (typingProgress < synopsisText.Length)
            {
                typingProgress = Mathf.Min(synopsisText.Length, typingProgress + charsPerSecond * Time.unscaledDeltaTime);
                if (introBodyText != null)
                {
                    introBodyText.text = synopsisText.Substring(0, Mathf.FloorToInt(typingProgress));
                }
            }
            else
            {
                postTypingTimer += Time.unscaledDeltaTime;
                if (postTypingTimer >= autoCloseDelayAfterTyping)
                {
                    CloseIntro();
                }
            }
        }

        private void StartIntro()
        {
            isIntroActive = true;
            typingProgress = 0f;
            postTypingTimer = 0f;

            if (introBodyText != null) introBodyText.text = "";
            if (introPanelRoot != null) introPanelRoot.SetActive(true);

            SetGameplayControlsBlocked(true, unlockCursor: false);
        }

        public void CloseIntro()
        {
            if (!isIntroActive) return;
            isIntroActive = false;
            lastIntroClosedFrame = Time.frameCount;

            if (introPanelRoot != null) introPanelRoot.SetActive(false);
            SetGameplayControlsBlocked(false, unlockCursor: false);
        }

        public bool RegisterClue(string clueId)
        {
            if (string.IsNullOrWhiteSpace(clueId)) return false;

            bool newlyAdded = collectedClueIds.Add(clueId.Trim());
            if (newlyAdded)
            {
                if (HorrorAudioManager.Instance != null)
                {
                    HorrorAudioManager.Instance.PlayClueCollectedSound();
                }

                UpdateObjectiveHud();

                if (HasAllClues)
                {
                    OnAllCluesCollected?.Invoke();
                }
            }
            return newlyAdded;
        }

        private void UpdateObjectiveHud()
        {
            if (objectiveHudText == null) return;

            if (HasAllClues)
            {
                objectiveHudText.text = "Objetivo: Has descubierto lo que les pasó. Escapa por la salida del laboratorio";
                objectiveHudText.color = new Color(0.50f, 1.0f, 0.58f, 0.96f);
            }
            else
            {
                objectiveHudText.text = $"Objetivo: Explora el laboratorio y encuentra pistas sobre tus compañeros ({CluesFound}/{requiredClues})";
                objectiveHudText.color = new Color(0.92f, 0.94f, 0.90f, 0.88f);
            }
        }

        public void TriggerVictory()
        {
            if (isVictoryActive) return;
            isVictoryActive = true;

            SetGameplayControlsBlocked(true, unlockCursor: true);
            Time.timeScale = 0f;

            if (victoryPanelRoot != null)
            {
                victoryPanelRoot.SetActive(true);
            }
        }

        public void ReturnToMainMenu()
        {
            isVictoryActive = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        private void SetGameplayControlsBlocked(bool blocked, bool unlockCursor)
        {
            if (firstPersonController != null)
            {
                firstPersonController.SetControlPaused(blocked, blocked, blocked);
                firstPersonController.SetCursorLocked(!blocked && !unlockCursor);
            }
            else
            {
                Cursor.lockState = (blocked || unlockCursor) ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = blocked || unlockCursor;
            }

            if (camcorderController != null) camcorderController.InputEnabled = !blocked;
            if (playerInventory != null) playerInventory.InputBlocked = blocked;
            if (playerHealth != null) playerHealth.HealInputBlocked = blocked;
            if (playerInteraction != null) playerInteraction.InputBlocked = blocked;
        }
    }
}
