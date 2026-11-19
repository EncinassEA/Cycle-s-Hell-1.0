using UnityEngine;

namespace CyclesHell.Camcorder
{
    /// <summary>
    /// Temblor de cámara acoplado a Main Camera (capa independiente del head-bobbing en CameraPivot).
    /// Soporta temblor continuo sutil por proximidad/persecución y sacudidas bruscas (ataques, flash UV, jumpscares).
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Límites")]
        [SerializeField] private float maxPositionOffset = 0.22f;
        [SerializeField] private float maxRollAngle = 4.5f;
        [SerializeField] private float noiseFrequency = 32f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;

        private float continuousIntensity;
        private float impulseIntensity;
        private float impulseDuration;
        private float impulseTimer;

        private void Awake()
        {
            Instance = this;
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetContinuousShake(float intensity)
        {
            continuousIntensity = Mathf.Clamp(intensity, 0f, 0.12f);
        }

        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;

            if (intensity >= impulseIntensity || impulseTimer <= 0f)
            {
                impulseIntensity = intensity;
                impulseDuration = duration;
                impulseTimer = duration;
            }
            else
            {
                impulseTimer = Mathf.Max(impulseTimer, duration * 0.6f);
            }
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0.001f)
            {
                transform.localPosition = baseLocalPosition;
                transform.localRotation = baseLocalRotation;
                return;
            }

            float currentImpulse = 0f;
            if (impulseTimer > 0f)
            {
                impulseTimer -= Time.deltaTime;
                float norm = Mathf.Clamp01(impulseTimer / Mathf.Max(0.01f, impulseDuration));
                currentImpulse = impulseIntensity * norm * norm;
                if (impulseTimer <= 0f)
                {
                    impulseIntensity = 0f;
                }
            }

            float total = Mathf.Min(maxPositionOffset, continuousIntensity + currentImpulse);
            if (total <= 0.0005f)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPosition, 1f - Mathf.Exp(-18f * Time.deltaTime));
                transform.localRotation = Quaternion.Slerp(transform.localRotation, baseLocalRotation, 1f - Mathf.Exp(-18f * Time.deltaTime));
                return;
            }

            float t = Time.time * noiseFrequency;
            float ox = (Mathf.PerlinNoise(t, 0.13f) * 2f - 1f) * total;
            float oy = (Mathf.PerlinNoise(1.47f, t) * 2f - 1f) * total;
            float roll = (Mathf.PerlinNoise(t * 0.85f, 5.2f) * 2f - 1f) * (total / maxPositionOffset) * maxRollAngle;

            transform.localPosition = baseLocalPosition + new Vector3(ox, oy, 0f);
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
