using UnityEngine;
using UnityEngine.AI;
using CyclesHell.Player;
using CyclesHell.Camcorder;
using CyclesHell.Survival;

namespace CyclesHell.AI
{
    public enum EnemyState
    {
        Patrol,
        Investigate,
        Chase,
        Attack
    }

    /// <summary>
    /// IA de "Sujeto_04" sobre NavMesh (Unity 6) con máquina de estados:
    /// PATROL -> INVESTIGATE -> CHASE -> ATTACK.
    /// Detecta al jugador por cono de visión + Raycast (mayor rango con visión nocturna o al correr)
    /// y por proximidad/sonido al correr.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private CamcorderController playerCamcorder;
        [SerializeField] private Transform[] patrolWaypoints;

        [Header("Visuales Procedurales")]
        [SerializeField] private Transform bodyVisualRoot;
        [SerializeField] private Transform headPivot;
        [SerializeField] private float eyeHeight = 1.92f;

        [Header("Velocidades")]
        [SerializeField] private float patrolSpeed = 1.65f;
        [SerializeField] private float investigateSpeed = 2.45f;
        [SerializeField] private float chaseSpeed = 4.35f;

        [Header("Percepción (Visión y Sonido)")]
        [SerializeField] private float baseSightRange = 11.5f;
        [SerializeField] private float nightVisionSightBonus = 5.5f;
        [SerializeField] private float sprintSightBonus = 3.5f;
        [SerializeField] private float visionHalfAngle = 54f;
        [SerializeField] private float hearingSprintRange = 11.0f;
        [SerializeField] private float hearingWalkRange = 3.2f;
        [SerializeField] private float awarenessToChaseTime = 0.45f;
        [SerializeField] private float loseSightDuration = 5.0f;
        [SerializeField] private LayerMask visionObstacleMask = ~0;

        [Header("Patrulla e Investigación")]
        [SerializeField] private float waypointArrivalDistance = 0.65f;
        [SerializeField] private float waypointPauseTime = 1.1f;
        [SerializeField] private float investigateWaitTime = 3.2f;

        [Header("Combate")]
        [SerializeField] private float attackRange = 1.85f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float attackDamage = 35f;
        [SerializeField] private float attackShakeIntensity = 0.18f;
        [SerializeField] private float attackShakeDuration = 0.30f;

        private NavMeshAgent agent;
        private EnemyState currentState = EnemyState.Patrol;

        private int currentWaypointIndex;
        private float waypointWaitTimer;
        private float investigateTimer;
        private float awarenessTimer;
        private float timeSinceLastSeen;
        private float attackCooldownTimer;
        private float lungeAnimTimer;

        private Vector3 lastKnownPlayerPos;
        private Vector3 defaultBodyLocalPos;
        private Quaternion defaultHeadLocalRot;

        public EnemyState CurrentState => currentState;
        public bool IsChasing => currentState == EnemyState.Chase || currentState == EnemyState.Attack;
        public float DistanceToPlayer => playerTransform != null
            ? Vector3.Distance(transform.position, playerTransform.position)
            : float.MaxValue;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            if (playerTransform == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null) playerTransform = playerGo.transform;
            }

            if (playerTransform != null)
            {
                if (playerController == null) playerController = playerTransform.GetComponent<FirstPersonController>();
                if (playerHealth == null) playerHealth = playerTransform.GetComponent<PlayerHealth>();
                if (playerCamcorder == null) playerCamcorder = playerTransform.GetComponentInChildren<CamcorderController>();
            }

            if (bodyVisualRoot != null) defaultBodyLocalPos = bodyVisualRoot.localPosition;
            if (headPivot != null) defaultHeadLocalRot = headPivot.localRotation;
        }

        private void Start()
        {
            if (agent != null && !agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            EnterState(EnemyState.Patrol);
        }

        private void Update()
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            bool playerAlive = playerTransform != null && (playerHealth == null || !playerHealth.IsDead);
            bool canSeePlayer = false;
            bool heardPlayer = false;

            if (playerAlive)
            {
                canSeePlayer = EvaluateVision(out bool isCloseOrClear);
                heardPlayer = EvaluateHearing();

                if (canSeePlayer)
                {
                    lastKnownPlayerPos = playerTransform.position;
                    timeSinceLastSeen = 0f;

                    if (currentState == EnemyState.Patrol || currentState == EnemyState.Investigate)
                    {
                        awarenessTimer += Time.deltaTime * (isCloseOrClear ? 2.2f : 1.0f);
                        if (awarenessTimer >= awarenessToChaseTime)
                        {
                            EnterState(EnemyState.Chase);
                        }
                        else if (currentState == EnemyState.Patrol && awarenessTimer > 0.12f)
                        {
                            EnterState(EnemyState.Investigate);
                        }
                    }
                }
                else
                {
                    awarenessTimer = Mathf.Max(0f, awarenessTimer - Time.deltaTime * 0.6f);

                    if (heardPlayer && (currentState == EnemyState.Patrol || currentState == EnemyState.Investigate))
                    {
                        lastKnownPlayerPos = playerTransform.position;
                        EnterState(EnemyState.Investigate);
                    }
                }
            }
            else if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
            {
                EnterState(EnemyState.Patrol);
            }

            switch (currentState)
            {
                case EnemyState.Patrol:
                    UpdatePatrol();
                    break;
                case EnemyState.Investigate:
                    UpdateInvestigate();
                    break;
                case EnemyState.Chase:
                    UpdateChase(canSeePlayer);
                    break;
                case EnemyState.Attack:
                    UpdateAttack(playerAlive);
                    break;
            }

            UpdateProceduralMotion();
        }

        private void EnterState(EnemyState nextState)
        {
            currentState = nextState;

            switch (currentState)
            {
                case EnemyState.Patrol:
                    agent.isStopped = false;
                    agent.speed = patrolSpeed;
                    agent.autoBraking = false;
                    waypointWaitTimer = 0f;
                    awarenessTimer = 0f;
                    MoveToCurrentWaypoint();
                    break;

                case EnemyState.Investigate:
                    agent.isStopped = false;
                    agent.speed = investigateSpeed;
                    agent.autoBraking = true;
                    investigateTimer = 0f;
                    SetDestinationOnNavMesh(lastKnownPlayerPos);
                    break;

                case EnemyState.Chase:
                    agent.isStopped = false;
                    agent.speed = chaseSpeed;
                    agent.autoBraking = false;
                    timeSinceLastSeen = 0f;
                    if (playerTransform != null)
                    {
                        lastKnownPlayerPos = playerTransform.position;
                        SetDestinationOnNavMesh(lastKnownPlayerPos);
                    }
                    break;

                case EnemyState.Attack:
                    agent.isStopped = true;
                    break;
            }
        }

        private void UpdatePatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= waypointArrivalDistance)
            {
                waypointWaitTimer += Time.deltaTime;
                if (waypointWaitTimer >= waypointPauseTime)
                {
                    waypointWaitTimer = 0f;
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                    MoveToCurrentWaypoint();
                }
            }
        }

        private void UpdateInvestigate()
        {
            if (!agent.pathPending && agent.remainingDistance <= waypointArrivalDistance + 0.25f)
            {
                investigateTimer += Time.deltaTime;
                // Rotación inquietante buscando alrededor
                transform.Rotate(Vector3.up, Mathf.Sin(investigateTimer * 2.4f) * 55f * Time.deltaTime);

                if (investigateTimer >= investigateWaitTime)
                {
                    SelectNearestWaypoint();
                    EnterState(EnemyState.Patrol);
                }
            }
        }

        private void UpdateChase(bool canSeePlayer)
        {
            if (playerTransform == null)
            {
                EnterState(EnemyState.Patrol);
                return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (canSeePlayer)
            {
                lastKnownPlayerPos = playerTransform.position;
                SetDestinationOnNavMesh(lastKnownPlayerPos);

                if (dist <= attackRange)
                {
                    EnterState(EnemyState.Attack);
                    return;
                }
            }
            else
            {
                timeSinceLastSeen += Time.deltaTime;
                SetDestinationOnNavMesh(lastKnownPlayerPos);

                if (timeSinceLastSeen >= loseSightDuration)
                {
                    // Perdió de vista al jugador por ~5s: busca brevemente en la última posición y vuelve a patrullar
                    EnterState(EnemyState.Investigate);
                }
            }
        }

        private void UpdateAttack(bool playerAlive)
        {
            if (!playerAlive || playerTransform == null)
            {
                EnterState(EnemyState.Patrol);
                return;
            }

            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            bool clearLine = HasLineOfSightToPlayer(attackRange + 0.8f);

            if (dist > attackRange + 0.45f || !clearLine)
            {
                EnterState(EnemyState.Chase);
                return;
            }

            if (attackCooldownTimer <= 0f)
            {
                PerformAttack();
            }
        }

        private void PerformAttack()
        {
            attackCooldownTimer = attackCooldown;
            lungeAnimTimer = 0.28f;

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }

            if (playerController != null)
            {
                playerController.TriggerCameraShake(attackShakeIntensity, attackShakeDuration);
            }
        }

        private bool EvaluateVision(out bool isCloseOrClear)
        {
            isCloseOrClear = false;
            if (playerTransform == null) return false;

            float effectiveRange = baseSightRange;
            float effectiveHalfAngle = visionHalfAngle;

            if (playerCamcorder != null && playerCamcorder.IsNightVisionOn)
            {
                effectiveRange += nightVisionSightBonus;
                effectiveHalfAngle += 10f;
            }
            else if (playerCamcorder != null && playerCamcorder.IsLampOn)
            {
                effectiveRange += nightVisionSightBonus * 0.85f;
                effectiveHalfAngle += 8f;
            }

            if (playerController != null && playerController.IsSprinting)
            {
                effectiveRange += sprintSightBonus;
            }

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPos = playerTransform.position + Vector3.up * 1.40f;
            Vector3 toPlayer = targetPos - eyePos;
            float dist = toPlayer.magnitude;

            if (dist > effectiveRange)
            {
                return false;
            }

            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > effectiveHalfAngle && dist > 2.2f)
            {
                return false;
            }

            if (!HasLineOfSightToPlayer(effectiveRange))
            {
                return false;
            }

            isCloseOrClear = dist < 7.5f || angle < 26f;
            return true;
        }

        private bool HasLineOfSightToPlayer(float maxDist)
        {
            if (playerTransform == null) return false;

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPos = playerTransform.position + Vector3.up * 1.40f;
            Vector3 dir = (targetPos - eyePos).normalized;
            float dist = Mathf.Min(Vector3.Distance(eyePos, targetPos), maxDist);

            // Evita colisionar con el propio collider del enemigo desplazando ligeramente el origen
            Vector3 rayOrigin = eyePos + dir * 0.42f;
            float rayLength = Mathf.Max(0.1f, dist - 0.42f);

            if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, rayLength, visionObstacleMask, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform);
            }

            return true;
        }

        private bool EvaluateHearing()
        {
            if (playerTransform == null || playerController == null)
            {
                return false;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (playerController.IsSprinting && dist <= hearingSprintRange)
            {
                return true;
            }

            if (playerController.IsMoving && dist <= hearingWalkRange)
            {
                return true;
            }

            return false;
        }

        private void MoveToCurrentWaypoint()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            Transform wp = patrolWaypoints[currentWaypointIndex];
            if (wp != null)
            {
                SetDestinationOnNavMesh(wp.position);
            }
        }

        private void SelectNearestWaypoint()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            float bestDist = float.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] == null) continue;
                float d = Vector3.SqrMagnitude(patrolWaypoints[i].position - transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }
            currentWaypointIndex = bestIdx;
        }

        private void SetDestinationOnNavMesh(Vector3 targetPos)
        {
            if (!agent.isOnNavMesh) return;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                agent.SetDestination(targetPos);
            }
        }

        private void UpdateProceduralMotion()
        {
            float speedMag = agent.velocity.magnitude;
            float t = Time.time;

            if (bodyVisualRoot != null)
            {
                float swayFreq = IsChasing ? 9.5f : 4.2f;
                float swayAmp = speedMag > 0.1f ? (IsChasing ? 0.055f : 0.03f) : 0.008f;
                float bobY = Mathf.Abs(Mathf.Sin(t * swayFreq)) * swayAmp;
                float tiltZ = Mathf.Sin(t * swayFreq * 0.5f) * (IsChasing ? 4.5f : 2.2f);

                float lungeZ = 0f;
                if (lungeAnimTimer > 0f)
                {
                    lungeAnimTimer -= Time.deltaTime;
                    lungeZ = Mathf.Sin(Mathf.Clamp01(lungeAnimTimer / 0.28f) * Mathf.PI) * 0.35f;
                }

                bodyVisualRoot.localPosition = defaultBodyLocalPos + new Vector3(0f, bobY, lungeZ);
                bodyVisualRoot.localRotation = Quaternion.Euler(IsChasing ? 10f : 3f, 0f, tiltZ);
            }

            if (headPivot != null)
            {
                // Ligero tic perturbador en la cabeza estilo terror psicológico
                float twitch = Mathf.PerlinNoise(t * (IsChasing ? 7f : 2.5f), 0.71f) > 0.78f
                    ? (Mathf.PerlinNoise(t * 28f, 3.1f) * 2f - 1f) * 18f
                    : Mathf.Sin(t * 1.4f) * 6f;
                headPivot.localRotation = defaultHeadLocalRot * Quaternion.Euler(0f, 0f, twitch);
            }
        }
    }
}
