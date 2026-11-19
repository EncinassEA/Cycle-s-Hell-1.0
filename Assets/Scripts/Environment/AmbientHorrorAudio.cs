using UnityEngine;

namespace CyclesHell.Environment
{
    /// <summary>
    /// Audio ambiental oscuro y opresivo para los pasillos.
    /// Si no tiene un AudioClip asignado, genera un zumbido eléctrico / drone subterráneo procedural sin depender de archivos externos.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientHorrorAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip ambientClip;
        [SerializeField] private float volume = 0.22f;

        private AudioSource audioSource;
        private AudioClip generatedDrone;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = volume;

            if (ambientClip != null)
            {
                audioSource.clip = ambientClip;
            }
            else if (audioSource.clip == null)
            {
                generatedDrone = CreateProceduralDarkDrone();
                audioSource.clip = generatedDrone;
            }
        }

        private void Start()
        {
            if (audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        private void OnDestroy()
        {
            if (generatedDrone != null)
            {
                Destroy(generatedDrone);
            }
        }

        private static AudioClip CreateProceduralDarkDrone()
        {
            const int sampleRate = 22050;
            const float duration = 6.0f;
            int totalSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[totalSamples];

            uint rng = 135792468u;
            float lpNoise = 0f;

            // Frecuencias exactas múltiplos de 1/duration para un loop 100% sin saltos (seamless)
            float f1 = 48.0f;  // zumbido eléctrico grave
            float f2 = 55.0f;  // disonancia sutil oscura
            float f3 = 110.0f; // armónico fluorescente tenue
            float lfo = 0.5f;  // modulación lenta (3 ciclos exactos en 6s)

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)sampleRate;
                float mod = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * lfo * t);

                float hum1 = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.28f;
                float hum2 = Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.16f * mod;
                float hum3 = Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.05f;

                rng ^= rng << 13;
                rng ^= rng >> 17;
                rng ^= rng << 5;
                float white = ((rng & 0xFFFF) / 32768f) - 1f;
                lpNoise = lpNoise * 0.94f + white * 0.06f;

                data[i] = Mathf.Clamp(hum1 + hum2 + hum3 + lpNoise * 0.22f, -0.8f, 0.8f);
            }

            AudioClip clip = AudioClip.Create("Procedural_DarkCorridorAmbient", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
