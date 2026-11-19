using UnityEngine;
using UnityEngine.AI;

namespace CyclesHell.AI
{
    /// <summary>
    /// Controlador de animación para "Sujeto_04":
    /// - Sincroniza un Animator real (Speed, IsFleeing, Attack, Flee) con NavMeshAgent.velocity.magnitude.
    /// - Añade espasmos sutiles de torso/cabeza, retroceso al Flash Morado y embestida al atacar.
    /// - Respaldo procedural automático si no hay Animator asignado (nunca se desliza estático).
    /// </summary>
    public class EnemyProceduralAnimator : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator modelAnimator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform headPivot;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform leftLegPivot;
        [SerializeField] private Transform rightLegPivot;

        [Header("Ajustes")]
        [SerializeField] private float speedDampTime = 0.12f;
        [SerializeField] private float swayAmplitude = 0.035f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsFleeingHash = Animator.StringToHash("IsFleeing");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int FleeHash = Animator.StringToHash("Flee");

        private Vector3 defaultVisualLocalPos;
        private Quaternion defaultVisualLocalRot;
        private Quaternion defaultHeadLocalRot;

        private float lungeTimer;
        private float recoilTimer;
        private float smoothedSpeed;
        private float walkCycle;

        private void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (modelAnimator == null) modelAnimator = GetComponentInChildren<Animator>();

            if (modelAnimator != null)
            {
                // El NavMeshAgent controla el desplazamiento; el Animator reproduce el ciclo real de caminar/correr/atacar
                modelAnimator.applyRootMotion = false;
            }

            if (visualRoot != null)
            {
                defaultVisualLocalPos = visualRoot.localPosition;
                defaultVisualLocalRot = visualRoot.localRotation;
            }

            if (headPivot != null)
            {
                defaultHeadLocalRot = headPivot.localRotation;
            }
        }

        public void SetAnimationState(float horizontalSpeed, bool isChasing, bool isFleeing)
        {
            float dt = Time.deltaTime;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, horizontalSpeed, 1f - Mathf.Exp(-(1f / Mathf.Max(0.02f, speedDampTime)) * dt));

            if (modelAnimator != null && modelAnimator.isActiveAndEnabled && modelAnimator.runtimeAnimatorController != null)
            {
                modelAnimator.SetFloat(SpeedHash, smoothedSpeed);
                modelAnimator.SetBool(IsFleeingHash, isFleeing);
            }

            UpdateProceduralMotion(smoothedSpeed, isChasing, isFleeing);
        }

        public void TriggerAttack()
        {
            lungeTimer = 0.32f;
            if (modelAnimator != null && modelAnimator.isActiveAndEnabled && modelAnimator.runtimeAnimatorController != null)
            {
                modelAnimator.ResetTrigger(AttackHash);
                modelAnimator.SetTrigger(AttackHash);
            }
        }

        public void TriggerFleeRecoil()
        {
            recoilTimer = 0.60f;
            if (modelAnimator != null && modelAnimator.isActiveAndEnabled && modelAnimator.runtimeAnimatorController != null)
            {
                modelAnimator.ResetTrigger(FleeHash);
                modelAnimator.SetTrigger(FleeHash);
            }
        }

        private void UpdateProceduralMotion(float speed, bool isChasing, bool isFleeing)
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            float cycleRate = speed > 0.1f ? Mathf.Lerp(4.2f, 10.5f, Mathf.Clamp01(speed / 4.5f)) : 1.5f;
            walkCycle += dt * cycleRate;

            if (visualRoot != null)
            {
                float moveFactor = Mathf.Clamp01(speed / 2.0f);
                float bobY = Mathf.Abs(Mathf.Sin(walkCycle)) * swayAmplitude * (0.25f + moveFactor);
                float tiltZ = Mathf.Sin(walkCycle * 0.5f) * (isChasing ? 3.8f : 1.8f) * moveFactor;

                float lungeZ = 0f;
                if (lungeTimer > 0f)
                {
                    lungeTimer -= dt;
                    lungeZ = Mathf.Sin(Mathf.Clamp01(lungeTimer / 0.32f) * Mathf.PI) * 0.36f;
                }

                float recoilPitch = 0f;
                float recoilShakeX = 0f;
                if (recoilTimer > 0f)
                {
                    recoilTimer -= dt;
                    float rNorm = Mathf.Clamp01(recoilTimer / 0.60f);
                    recoilPitch = -22f * Mathf.Sin(rNorm * Mathf.PI);
                    recoilShakeX = Mathf.Sin(t * 52f) * 0.09f * rNorm;
                }

                float leanPitch = isFleeing ? 12f : (isChasing ? 8f : 2f);
                visualRoot.localPosition = defaultVisualLocalPos + new Vector3(recoilShakeX, bobY, lungeZ);
                visualRoot.localRotation = defaultVisualLocalRot * Quaternion.Euler(leanPitch + recoilPitch, 0f, tiltZ);
            }

            // Respaldo de extremidades si no existe un Animator activo con huesos
            bool useFallbackLimbs = modelAnimator == null || modelAnimator.runtimeAnimatorController == null;
            if (useFallbackLimbs)
            {
                float swing = Mathf.Sin(walkCycle) * 28f * Mathf.Clamp01(speed / 2.5f);
                if (leftLegPivot != null) leftLegPivot.localRotation = Quaternion.Euler(swing, 0f, 0f);
                if (rightLegPivot != null) rightLegPivot.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                if (leftArmPivot != null) leftArmPivot.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 5f);
                if (rightArmPivot != null) rightArmPivot.localRotation = Quaternion.Euler(swing * 0.8f, 0f, -5f);
            }

            if (headPivot != null)
            {
                float twitch = Mathf.PerlinNoise(t * (isChasing ? 8f : 2.8f), 0.71f) > 0.78f
                    ? (Mathf.PerlinNoise(t * 28f, 3.1f) * 2f - 1f) * 16f
                    : Mathf.Sin(t * 1.4f) * 5f;
                headPivot.localRotation = defaultHeadLocalRot * Quaternion.Euler(0f, 0f, twitch);
            }
        }
    }
}
