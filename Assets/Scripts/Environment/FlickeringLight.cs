using UnityEngine;

namespace CyclesHell.Environment
{
    /// <summary>
    /// Simula un fallo eléctrico irregular en lámparas fluorescentes viejas,
    /// modulando tanto la intensidad de la luz como la emisión del tubo fluorescente.
    /// </summary>
    public class FlickeringLight : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer emissiveTubeRenderer;

        [Header("Comportamiento del Parpadeo")]
        [SerializeField] private float minIntensityMultiplier = 0.08f;
        [SerializeField] private float maxIntensityMultiplier = 1.15f;
        [SerializeField] private float flickerSpeed = 14f;
        [SerializeField] private float blackoutChance = 0.14f;
        [SerializeField] private Color baseEmissionColor = new Color(0.62f, 0.78f, 0.76f, 1f);

        private float baseLightIntensity;
        private float noiseSeed;
        private Material tubeMaterialInstance;

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }

            if (targetLight != null)
            {
                baseLightIntensity = targetLight.intensity;
            }

            noiseSeed = Random.Range(0f, 100f);

            if (emissiveTubeRenderer != null)
            {
                tubeMaterialInstance = emissiveTubeRenderer.material;
            }
        }

        private void Update()
        {
            if (targetLight == null) return;

            float t = Time.time * flickerSpeed + noiseSeed;
            float n1 = Mathf.PerlinNoise(t, noiseSeed);
            float n2 = Mathf.PerlinNoise(t * 2.7f, noiseSeed + 13.7f);
            float combined = n1 * 0.65f + n2 * 0.35f;

            float mult;
            if (combined < blackoutChance)
            {
                mult = minIntensityMultiplier;
            }
            else if (combined < blackoutChance + 0.16f)
            {
                mult = Mathf.Lerp(minIntensityMultiplier, 0.65f, (combined - blackoutChance) / 0.16f);
            }
            else
            {
                mult = Mathf.Lerp(0.75f, maxIntensityMultiplier, (combined - 0.3f) / 0.7f);
            }

            targetLight.intensity = baseLightIntensity * mult;

            if (tubeMaterialInstance != null)
            {
                tubeMaterialInstance.EnableKeyword("_EMISSION");
                tubeMaterialInstance.SetColor("_EmissionColor", baseEmissionColor * (mult * 1.4f));
            }
        }
    }
}
