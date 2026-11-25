using System.Collections;
using UnityEngine;
using CyclesHell.Audio;
using CyclesHell.AI;
using CyclesHell.Camcorder;

namespace CyclesHell.Interaction
{
    /// <summary>
    /// Susto psicológico #1 al subir las escaleras de la cabaña por primera vez:
    /// La luz del pasillo superior parpadea/revienta brevemente y una silueta oscura cruza rápido
    /// al final del pasillo hacia el cuarto secundario y desaparece (sin dañar al jugador).
    /// </summary>
    public class UpstairsHallScareTrigger : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Light hallwayLight;
        [SerializeField] private Transform silhouetteTransform;
        [SerializeField] private Vector3 silhouetteStartPos = new Vector3(0.65f, 3.52f, -2.0f);
        [SerializeField] private Vector3 silhouetteEndPos = new Vector3(-3.8f, 3.52f, -2.0f);
        [SerializeField] private float dashDuration = 0.78f;

        private bool triggered;
        private AudioClip bulbPopClip;

        private void Awake()
        {
            if (silhouetteTransform != null)
            {
                silhouetteTransform.position = silhouetteStartPos;
                silhouetteTransform.gameObject.SetActive(false);
            }

            bulbPopClip = CreateBulbPopClip();
        }

        private void OnDestroy()
        {
            if (bulbPopClip != null) Destroy(bulbPopClip);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !other.CompareTag("Player")) return;
            triggered = true;
            StartCoroutine(ScareRoutine());
        }

        private IEnumerator ScareRoutine()
        {
            if (bulbPopClip != null)
            {
                Vector3 soundPos = hallwayLight != null ? hallwayLight.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(bulbPopClip, soundPos, 0.88f);
            }

            if (HorrorAudioManager.Instance != null)
            {
                HorrorAudioManager.Instance.PlayJumpscareStinger();
            }

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.16f, 0.40f);
            }

            EnemyProximityFX prox = FindAnyObjectByType<EnemyProximityFX>();
            if (prox != null)
            {
                prox.TriggerJumpscareGlitch(0.65f);
            }

            float origIntensity = hallwayLight != null ? hallwayLight.intensity : 1.4f;

            if (silhouetteTransform != null)
            {
                silhouetteTransform.position = silhouetteStartPos;
                silhouetteTransform.gameObject.SetActive(true);
            }

            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, dashDuration));

                if (hallwayLight != null)
                {
                    bool flash = Mathf.PerlinNoise(elapsed * 42f, 2.3f) > 0.55f;
                    hallwayLight.intensity = flash ? origIntensity * 1.9f : origIntensity * 0.04f;
                }

                if (silhouetteTransform != null)
                {
                    Vector3 p = Vector3.Lerp(silhouetteStartPos, silhouetteEndPos, t);
                    p.y += Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f)) * 0.06f;
                    silhouetteTransform.position = p;
                }

                yield return null;
            }

            if (silhouetteTransform != null)
            {
                silhouetteTransform.gameObject.SetActive(false);
            }

            // Apagón breve de la lámpara del pasillo y luego vuelve tenue
            if (hallwayLight != null)
            {
                hallwayLight.enabled = false;
                yield return new WaitForSeconds(0.95f);
                hallwayLight.enabled = true;
                hallwayLight.intensity = origIntensity * 0.7f;
            }
        }

        private static AudioClip CreateBulbPopClip()
        {
            const int sr = 22050;
            const float dur = 0.45f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];
            uint rng = 112233u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float w = ((rng & 0xFFFF) / 32768f) - 1f;
                float pop = Mathf.Exp(-t * 45f) * w * 0.92f;
                float zzz = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 9f) * 0.28f;
                data[i] = Mathf.Clamp(pop + zzz, -0.95f, 0.95f);
            }
            AudioClip c = AudioClip.Create("SFX_BulbPopScare", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
