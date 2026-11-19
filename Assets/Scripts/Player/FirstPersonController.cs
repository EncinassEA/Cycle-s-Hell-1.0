using UnityEngine;
using UnityEngine.InputSystem;

namespace CyclesHell.Player
{
    /// <summary>
    /// Controlador en primera persona para "Cycle's Hell" usando exclusivamente el Nuevo Input System.
    /// Incluye movimiento WASD, sprint con Shift, rotación suave con ratón y balanceo ligero (head-bobbing).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform cameraPivot;

        [Header("Movimiento")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintSpeed = 5.6f;
        [SerializeField] private float moveAcceleration = 12f;
        [SerializeField] private float gravity = -18f;

        [Header("Cámara / Ratón")]
        [SerializeField] private float mouseSensitivity = 0.14f;
        [SerializeField] private float lookSmoothing = 22f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool lockCursorOnStart = true;

        [Header("Head-Bobbing")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float walkBobFrequency = 8.5f;
        [SerializeField] private float walkBobAmplitudeY = 0.045f;
        [SerializeField] private float walkBobAmplitudeX = 0.025f;
        [SerializeField] private float sprintBobMultiplier = 1.45f;
        [SerializeField] private float bobReturnSpeed = 8f;

        private CharacterController characterController;

        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction lookAction;
        private InputAction unlockCursorAction;
        private InputAction lockCursorAction;

        private Vector3 currentHorizontalVelocity;
        private float verticalVelocity;

        private float targetYaw;
        private float targetPitch;
        private float currentYaw;
        private float currentPitch;

        private Vector3 defaultPivotLocalPos;
        private float bobTimer;
        private bool isCursorLocked;
        private bool movementPaused;
        private bool lookPaused;
        private bool cursorAutoControlPaused;

        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsInputPaused => movementPaused || lookPaused;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (cameraPivot == null)
            {
                Camera childCam = GetComponentInChildren<Camera>();
                if (childCam != null)
                {
                    cameraPivot = childCam.transform.parent != null && childCam.transform.parent != transform
                        ? childCam.transform.parent
                        : childCam.transform;
                }
            }

            if (cameraPivot != null)
            {
                defaultPivotLocalPos = cameraPivot.localPosition;
                targetPitch = NormalizeAngle(cameraPivot.localEulerAngles.x);
                currentPitch = targetPitch;
            }

            targetYaw = transform.eulerAngles.y;
            currentYaw = targetYaw;

            SetupInputActions();
        }

        private void SetupInputActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            sprintAction = new InputAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Keyboard>/rightShift");

            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");

            unlockCursorAction = new InputAction("UnlockCursor", InputActionType.Button, "<Keyboard>/escape");
            lockCursorAction = new InputAction("LockCursor", InputActionType.Button, "<Mouse>/leftButton");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            sprintAction?.Enable();
            lookAction?.Enable();
            unlockCursorAction?.Enable();
            lockCursorAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            sprintAction?.Disable();
            lookAction?.Disable();
            unlockCursorAction?.Disable();
            lockCursorAction?.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            sprintAction?.Dispose();
            lookAction?.Dispose();
            unlockCursorAction?.Dispose();
            lockCursorAction?.Dispose();
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void Update()
        {
            if (!cursorAutoControlPaused)
            {
                HandleCursorLock();
            }

            if (!lookPaused)
            {
                HandleLook();
            }

            if (!movementPaused)
            {
                HandleMovement();
                HandleHeadBob();
            }
            else
            {
                IsMoving = false;
                IsSprinting = false;
                currentHorizontalVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Pausa o reanuda movimiento, rotación de cámara y control automático del cursor (usado por notas y pantalla de muerte).
        /// </summary>
        public void SetControlPaused(bool pauseMovement, bool pauseLook, bool pauseAutoCursor = false)
        {
            movementPaused = pauseMovement;
            lookPaused = pauseLook;
            cursorAutoControlPaused = pauseAutoCursor;

            if (movementPaused)
            {
                currentHorizontalVelocity = Vector3.zero;
                IsMoving = false;
                IsSprinting = false;
            }
        }

        private void HandleCursorLock()
        {
            if (unlockCursorAction.WasPressedThisFrame())
            {
                SetCursorLocked(false);
            }
            else if (!isCursorLocked && lockCursorAction.WasPressedThisFrame())
            {
                SetCursorLocked(true);
            }
        }

        public void SetCursorLocked(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleLook()
        {
            if (!isCursorLocked || cameraPivot == null)
            {
                return;
            }

            Vector2 lookDelta = lookAction.ReadValue<Vector2>();
            targetYaw += lookDelta.x * mouseSensitivity;
            targetPitch -= lookDelta.y * mouseSensitivity;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

            float smoothFactor = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, smoothFactor);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, smoothFactor);

            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            bool wantsSprint = sprintAction.IsPressed() && moveInput.y > 0.1f;
            float targetSpeed = wantsSprint ? sprintSpeed : walkSpeed;

            Vector3 desiredDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            Vector3 targetVelocity = desiredDirection * targetSpeed;

            float accelFactor = 1f - Mathf.Exp(-moveAcceleration * Time.deltaTime);
            currentHorizontalVelocity = Vector3.Lerp(currentHorizontalVelocity, targetVelocity, accelFactor);

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 finalMotion = currentHorizontalVelocity + Vector3.up * verticalVelocity;
            characterController.Move(finalMotion * Time.deltaTime);

            float planarSpeed = new Vector2(currentHorizontalVelocity.x, currentHorizontalVelocity.z).magnitude;
            IsMoving = characterController.isGrounded && planarSpeed > 0.15f;
            IsSprinting = IsMoving && wantsSprint;
        }

        private void HandleHeadBob()
        {
            if (!enableHeadBob || cameraPivot == null)
            {
                return;
            }

            if (IsMoving)
            {
                float speedRatio = Mathf.Clamp01(currentHorizontalVelocity.magnitude / sprintSpeed);
                float freq = walkBobFrequency * (IsSprinting ? sprintBobMultiplier : 1f);
                float ampY = walkBobAmplitudeY * (IsSprinting ? sprintBobMultiplier : 1f);
                float ampX = walkBobAmplitudeX * (IsSprinting ? sprintBobMultiplier : 1f);

                bobTimer += Time.deltaTime * freq;

                float offsetY = Mathf.Sin(bobTimer) * ampY * Mathf.Lerp(0.6f, 1f, speedRatio);
                float offsetX = Mathf.Cos(bobTimer * 0.5f) * ampX * Mathf.Lerp(0.6f, 1f, speedRatio);

                Vector3 targetLocalPos = defaultPivotLocalPos + new Vector3(offsetX, offsetY, 0f);
                cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, targetLocalPos, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }
            else
            {
                bobTimer = 0f;
                cameraPivot.localPosition = Vector3.Lerp(
                    cameraPivot.localPosition,
                    defaultPivotLocalPos,
                    1f - Mathf.Exp(-bobReturnSpeed * Time.deltaTime));
            }
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
            {
                angle -= 360f;
            }
            return angle;
        }
    }
}
