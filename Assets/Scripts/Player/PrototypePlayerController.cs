using UnityEngine;
using SplatoonInkPrototype.Weapons;

namespace SplatoonInkPrototype.Player
{
    /// <summary>
    /// Simple prototype controller for movement and facing.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PrototypePlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInkState inkState;
        [SerializeField] private Transform cameraReference;
        [SerializeField] private InkWeaponEmitter weaponEmitter;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private Transform swimVisualProxy;

        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.8f;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private KeyCode swimKey = KeyCode.LeftShift;
        [SerializeField] private float wallStickSpeed = 2f;
        [SerializeField] private float wallClimbSpeed = 5.2f;
        [SerializeField] private float wallStrafeSpeed = 4.3f;
        [SerializeField] private float wallSlideSpeed = 2.8f;
        [SerializeField] private bool enableAutoLedgeMantle = false;
        [SerializeField] private float wallTopProbeHeight = 1.2f;
        [SerializeField] private float ledgeMantleForwardDistance = 0.7f;
        [SerializeField] private float ledgeMantleUpDistance = 0.45f;
        [SerializeField] private float ledgeMantleProbeHeight = 1.5f;
        [SerializeField] private float wallJumpUpVelocity = 6.5f;
        [SerializeField] private float wallJumpAwayVelocity = 5f;
        [SerializeField] private float wallDetachCooldown = 0.28f;
        [SerializeField] private float externalVelocityDamping = 10f;
        [SerializeField] private float groundedControllerHeight = 2f;
        [SerializeField] private float groundedControllerRadius = 0.45f;
        [SerializeField] private float swimControllerHeight = 1.2f;
        [SerializeField] private float swimControllerRadius = 0.45f;
        [SerializeField] private Vector3 groundedControllerCenter = Vector3.zero;
        [SerializeField] private Vector3 swimControllerCenter = new(0f, -0.4f, 0f);

        private float verticalVelocity;
        private float wallInteractionSuppressedUntil;
        private Vector3 externalHorizontalVelocity;
        private Material swimProxyMaterial;

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
            inkState = GetComponent<PlayerInkState>();
        }

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (inkState == null)
            {
                inkState = GetComponent<PlayerInkState>();
            }

            if (weaponEmitter == null)
            {
                weaponEmitter = GetComponent<InkWeaponEmitter>();
            }

            if (visualRenderer == null)
            {
                visualRenderer = GetComponentInChildren<Renderer>();
            }

            EnsureSwimVisualProxy();

            if (cameraReference == null && Camera.main != null)
            {
                cameraReference = Camera.main.transform;
            }
        }

        private void Update()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);

            var moveDirection = input;
            if (cameraReference != null)
            {
                var cameraForward = cameraReference.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                var cameraRight = cameraReference.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                moveDirection = (cameraForward * input.z) + (cameraRight * input.x);
            }

            if (inkState != null)
            {
                inkState.SetSwimRequested(Input.GetKey(swimKey));
                inkState.SetWallProbeDirection(moveDirection.sqrMagnitude > 0.001f ? moveDirection : transform.forward);
                inkState.EvaluateSurfaceState();
            }

            var facingDirection = moveDirection;
            if (weaponEmitter != null && weaponEmitter.IsActivelyAiming)
            {
                facingDirection = weaponEmitter.GetFlatAimDirection();
            }

            if (facingDirection.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            var speedMultiplier = inkState != null ? inkState.CurrentSpeedMultiplier : 1f;
            externalHorizontalVelocity = Vector3.MoveTowards(
                externalHorizontalVelocity,
                Vector3.zero,
                externalVelocityDamping * Time.deltaTime);

            var horizontalMovement = moveDirection.normalized * (baseMoveSpeed * speedMultiplier);
            ApplyTraversalPresentation();

            if (ShouldUseWallSwimming())
            {
                if (Input.GetKeyDown(jumpKey))
                {
                    JumpFromInkWall(input);
                    UpdateSwimVisualProxy(false);
                    return;
                }

                MoveAlongInkWall(input);
                UpdateSwimVisualProxy(true);
                return;
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (characterController.isGrounded && Input.GetKeyDown(jumpKey))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            var movement = new Vector3(horizontalMovement.x, verticalVelocity, horizontalMovement.z)
                + externalHorizontalVelocity;
            characterController.Move(movement * Time.deltaTime);
            UpdateSwimVisualProxy(inkState != null && inkState.IsSwimming);
        }

        public void Configure(CharacterController controller, PlayerInkState state, Transform cameraTransform)
        {
            characterController = controller;
            inkState = state;
            cameraReference = cameraTransform;
        }

        public void Configure(CharacterController controller, PlayerInkState state, Transform cameraTransform, InkWeaponEmitter emitter, Renderer rendererReference)
        {
            characterController = controller;
            inkState = state;
            cameraReference = cameraTransform;
            weaponEmitter = emitter;
            visualRenderer = rendererReference;
        }

        private void ApplyTraversalPresentation()
        {
            if (characterController == null || inkState == null)
            {
                return;
            }

            if (inkState.IsSwimming)
            {
                characterController.height = swimControllerHeight;
                characterController.radius = swimControllerRadius;
                characterController.center = swimControllerCenter;

                if (visualRenderer != null)
                {
                    visualRenderer.enabled = false;
                }

                return;
            }

            if (ShouldUseWallSwimming())
            {
                characterController.height = swimControllerHeight;
                characterController.radius = swimControllerRadius;
                characterController.center = swimControllerCenter;

                if (visualRenderer != null)
                {
                    visualRenderer.enabled = false;
                }

                return;
            }

            characterController.height = groundedControllerHeight;
            characterController.radius = groundedControllerRadius;
            characterController.center = groundedControllerCenter;

            if (visualRenderer != null)
            {
                visualRenderer.enabled = true;
            }
        }

        private void MoveAlongInkWall(Vector3 input)
        {
            var wallNormal = ResolveFlatWallNormal();
            var wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
            var verticalSpeed = input.z >= 0f ? wallClimbSpeed : wallSlideSpeed;
            var vertical = Vector3.up * (input.z * verticalSpeed);
            var lateral = wallRight * (input.x * wallStrafeSpeed);
            var stickToWall = -wallNormal * wallStickSpeed;
            var movement = vertical + lateral + stickToWall;

            verticalVelocity = 0f;
            characterController.Move(movement * Time.deltaTime);

            if (enableAutoLedgeMantle && input.z > 0.1f && IsAtInkWallTop(wallNormal))
            {
                MantleOntoWallTop(wallNormal);
            }
        }

        private bool ShouldUseWallSwimming()
        {
            return inkState != null
                && inkState.IsWallSwimming
                && Time.time >= wallInteractionSuppressedUntil;
        }

        private bool IsAtInkWallTop(Vector3 wallNormal)
        {
            var probeOrigin = transform.position + (Vector3.up * wallTopProbeHeight);
            return !Physics.Raycast(probeOrigin, -wallNormal, wallStickSpeed * Time.deltaTime + 0.65f)
                && TryResolveMantleTarget(wallNormal, out _);
        }

        private void MantleOntoWallTop(Vector3 wallNormal)
        {
            wallInteractionSuppressedUntil = Time.time + wallDetachCooldown;
            verticalVelocity = -2f;
            externalHorizontalVelocity = Vector3.zero;

            if (!TryResolveMantleTarget(wallNormal, out var mantleTarget))
            {
                JumpAwayFromWall(wallNormal);
                return;
            }

            var mantleMove = mantleTarget - transform.position;
            characterController.Move(mantleMove);
            UpdateSwimVisualProxy(false);
        }

        private bool TryResolveMantleTarget(Vector3 wallNormal, out Vector3 mantleTarget)
        {
            mantleTarget = transform.position
                + (-wallNormal * ledgeMantleForwardDistance)
                + (Vector3.up * ledgeMantleUpDistance);

            var probeOrigin = mantleTarget + (Vector3.up * ledgeMantleProbeHeight);
            if (!Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out var topHit,
                ledgeMantleProbeHeight + ledgeMantleUpDistance + 0.75f)
                || topHit.normal.y <= 0.55f)
            {
                return false;
            }

            mantleTarget.y = topHit.point.y
                - characterController.center.y
                + (characterController.height * 0.5f)
                + 0.05f;
            return true;
        }

        private void JumpFromInkWall(Vector3 input)
        {
            var wallNormal = ResolveFlatWallNormal();
            if (input.z > -0.2f && IsAtInkWallTop(wallNormal))
            {
                MantleOntoWallTop(wallNormal);
                return;
            }

            JumpAwayFromWall(wallNormal);
        }

        private void JumpAwayFromWall()
        {
            JumpAwayFromWall(ResolveFlatWallNormal());
        }

        private void JumpAwayFromWall(Vector3 wallNormal)
        {
            wallInteractionSuppressedUntil = Time.time + wallDetachCooldown;
            externalHorizontalVelocity = wallNormal * wallJumpAwayVelocity;
            verticalVelocity = wallJumpUpVelocity;
        }

        private Vector3 ResolveFlatWallNormal()
        {
            var wallNormal = inkState != null ? inkState.CurrentSurfaceNormal : -transform.forward;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude < 0.001f)
            {
                wallNormal = -transform.forward;
            }

            wallNormal.Normalize();
            return wallNormal;
        }

        private void EnsureSwimVisualProxy()
        {
            if (swimVisualProxy != null)
            {
                swimVisualProxy.gameObject.SetActive(false);
                return;
            }

            var proxyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxyObject.name = "SwimVisualProxy";
            proxyObject.transform.SetParent(transform, false);
            swimVisualProxy = proxyObject.transform;

            var collider = proxyObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = proxyObject.GetComponent<Renderer>();
            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            swimProxyMaterial = new Material(shader)
            {
                name = "SwimProxyInkMaterial",
                color = new Color(0.1f, 0.85f, 1f, 0.95f)
            };
            renderer.sharedMaterial = swimProxyMaterial;
            proxyObject.SetActive(false);
        }

        private void UpdateSwimVisualProxy(bool shouldShow)
        {
            EnsureSwimVisualProxy();
            if (swimVisualProxy == null)
            {
                return;
            }

            swimVisualProxy.gameObject.SetActive(shouldShow);
            if (!shouldShow || inkState == null)
            {
                return;
            }

            if (inkState.IsWallSwimming)
            {
                var wallNormal = inkState.CurrentSurfaceNormal.normalized;
                swimVisualProxy.position = transform.position - (wallNormal * 0.43f) + (Vector3.up * 0.05f);
                swimVisualProxy.rotation = Quaternion.LookRotation(-wallNormal, Vector3.up);
                swimVisualProxy.localScale = new Vector3(0.85f, 0.18f, 1.05f);
                return;
            }

            swimVisualProxy.localPosition = new Vector3(0f, -0.82f, 0.05f);
            swimVisualProxy.localRotation = Quaternion.identity;
            swimVisualProxy.localScale = new Vector3(0.85f, 0.16f, 1.15f);
        }
    }
}
