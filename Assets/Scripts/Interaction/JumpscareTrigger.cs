using System.Collections;
using UnityEngine;
using CyclesHell.Audio;
using CyclesHell.AI;
using CyclesHell.Camcorder;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Evento de susto (Jumpscare) de un solo uso:
    /// Dispara stinger fuerte, apagón/parpadeo breve de luces cercanas, sacudida brusca de cámara,
    /// interferencia VHS en el HUD y aparición fugaz (~0.5s) de una sombra que desaparece al instante.
    /// </summary>
    public class JumpscareTrigger : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private bool triggerOnEnter = true;
        [SerializeField] private float apparitionDuration = 0.50f;
        [SerializeField] private float lightBlackoutDuration = 0.65f;
        [SerializeField] private float shakeIntensity = 0.30f;
        [SerializeField] private float shakeDuration = 0.55f;

        [Header("Referencias de Escena")]
        [SerializeField] private GameObject apparitionVisual;
        [SerializeField] private Light[] nearbyLights;
        [SerializeField] private EnemyProximityFX proximityFX;

        private bool hasTriggered;

        private void Awake()
        {
            if (apparitionVisual != null)
            {
                apparitionVisual.SetActive(false);
            }

            if (proximityFX == null)
            {
                proximityFX = FindAnyObjectByType<EnemyProximityFX>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter || hasTriggered) return;
            if (other.CompareTag("Player"))
            {
                TriggerScare();
            }
        }

        public void TriggerScare()
        {
            if (hasTriggered) return;
            hasTriggered = true;
            StartCoroutine(ScareRoutine());
        }

        private IEnumerator ScareRoutine()
        {
            if (HorrorAudioManager.Instance != null)
            {
                HorrorAudioManager.Instance.PlayJumpscareStinger();
            }

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(shakeIntensity, shakeDuration);
            }

            if (proximityFX == null) proximityFX = FindAnyObjectByType<EnemyProximityFX>();
            if (proximityFX != null)
            {
                proximityFX.TriggerJumpscareGlitch(0.70f);
            }

            // Mostrar aparición fugaz orientada hacia el jugador
            if (apparitionVisual != null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                {
                    Vector3 lookPos = playerGo.transform.position;
                    lookPos.y = apparitionVisual.transform.position.y;
                    apparitionVisual.transform.LookAt(lookPos);
                }
                apparitionVisual.SetActive(true);
            }

            // Apagón breve de luces cercanas
            bool[] prevStates = null;
            if (nearbyLights != null && nearbyLights.Length > 0)
            {
                prevStates = new bool[nearbyLights.Length];
                for (int i = 0; i < nearbyLights.Length; i++)
                {
                    if (nearbyLights[i] != null)
                    {
                        prevStates[i] = nearbyLights[i].enabled;
                        nearbyLights[i].enabled = false;
                    }
                }
            }

            yield return new WaitForSeconds(apparitionDuration);

            if (apparitionVisual != null)
            {
                apparitionVisual.SetActive(false);
            }

            float remainingLightTime = Mathf.Max(0f, lightBlackoutDuration - apparitionDuration);
            if (remainingLightTime > 0f)
            {
                yield return new WaitForSeconds(remainingLightTime);
            }

            if (nearbyLights != null && prevStates != null)
            {
                for (int i = 0; i < nearbyLights.Length; i++)
                {
                    if (nearbyLights[i] != null)
                    {
                        nearbyLights[i].enabled = prevStates[i];
                    }
                }
            }
        }
    }
}
