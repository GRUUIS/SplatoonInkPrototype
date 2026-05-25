using UnityEngine;

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

        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotationSpeed = 12f;

        private float verticalVelocity;

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

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            var speedMultiplier = inkState != null ? inkState.CurrentSpeedMultiplier : 1f;
            var horizontalMovement = moveDirection.normalized * (baseMoveSpeed * speedMultiplier);

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            var movement = new Vector3(horizontalMovement.x, verticalVelocity, horizontalMovement.z);
            characterController.Move(movement * Time.deltaTime);
        }

        public void Configure(CharacterController controller, PlayerInkState state, Transform cameraTransform)
        {
            characterController = controller;
            inkState = state;
            cameraReference = cameraTransform;
        }
    }
}
