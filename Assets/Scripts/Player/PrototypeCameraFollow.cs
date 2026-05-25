using UnityEngine;

namespace SplatoonInkPrototype.Player
{
    /// <summary>
    /// Lightweight third-person orbit camera with simple obstruction handling.
    /// </summary>
    public class PrototypeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new(0.55f, 1.35f, 0f);

        [Header("Orbit")]
        [SerializeField] private bool rotateWithMouseMovement = true;
        [SerializeField] private bool lockCursorWhilePlaying = true;
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float pitch = 22f;
        [SerializeField] private float minPitch = 5f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private float rotationSensitivity = 180f;

        [Header("Distance")]
        [SerializeField] private float distance = 7f;
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 9f;
        [SerializeField] private float zoomSpeed = 4f;
        [SerializeField] private float followLerp = 10f;

        [Header("Obstruction")]
        [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float obstructionRadius = 0.25f;
        [SerializeField] private float obstructionPadding = 0.15f;
        [SerializeField] private float obstructionReturnSpeed = 8f;

        private float currentDistance;
        private bool cursorUnlocked;
        private readonly RaycastHit[] obstructionHits = new RaycastHit[16];

        private void Awake()
        {
            currentDistance = distance;
            var euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);

            if (Application.isPlaying && lockCursorWhilePlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !lockCursorWhilePlaying)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
        }

        public Ray GetAimRay()
        {
            var cameraToUse = GetComponent<Camera>();
            return cameraToUse != null
                ? cameraToUse.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position, transform.forward);
        }

        public Vector3 GetPivotPosition()
        {
            if (target == null)
            {
                return transform.position;
            }

            var yawRotation = Quaternion.Euler(0f, yaw, 0f);
            return target.position + (yawRotation * Vector3.right * pivotOffset.x) + (Vector3.up * pivotOffset.y);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            UpdateOrbitInput();

            var pivotPosition = GetPivotPosition();
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            var resolvedDistance = ResolveObstructionDistance(pivotPosition, rotation, desiredDistance);

            currentDistance = Mathf.Lerp(currentDistance, resolvedDistance, obstructionReturnSpeed * Time.deltaTime);

            var desiredPosition = pivotPosition - (rotation * Vector3.forward * currentDistance);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerp * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation((pivotPosition - transform.position).normalized, Vector3.up);
        }

        private void UpdateOrbitInput()
        {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Escape))
            {
                cursorUnlocked = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Application.isPlaying && Input.GetMouseButtonDown(0) && lockCursorWhilePlaying)
            {
                cursorUnlocked = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (rotateWithMouseMovement && !cursorUnlocked)
            {
                yaw += Input.GetAxis("Mouse X") * rotationSensitivity * Time.deltaTime;
                pitch -= Input.GetAxis("Mouse Y") * rotationSensitivity * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance = Mathf.Clamp(distance - (scroll * zoomSpeed), minDistance, maxDistance);
            }
        }

        private float ResolveObstructionDistance(Vector3 pivotPosition, Quaternion rotation, float desiredDistance)
        {
            var cameraDirection = rotation * Vector3.back;
            var hitCount = Physics.SphereCastNonAlloc(
                pivotPosition,
                obstructionRadius,
                cameraDirection,
                obstructionHits,
                desiredDistance + obstructionPadding,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            if (TryGetClosestExternalHit(hitCount, out var hit))
            {
                return Mathf.Clamp(hit.distance - obstructionPadding, minDistance, desiredDistance);
            }

            return desiredDistance;
        }

        private bool TryGetClosestExternalHit(int hitCount, out RaycastHit closestHit)
        {
            closestHit = default;
            var closestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = obstructionHits[i];
                if (hit.collider == null || IsTargetCollider(hit.collider) || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
            }

            return closestDistance < float.MaxValue;
        }

        private bool IsTargetCollider(Collider candidate)
        {
            return target != null && (candidate.transform == target || candidate.transform.IsChildOf(target));
        }

        private static float NormalizePitch(float rawPitch)
        {
            if (rawPitch > 180f)
            {
                rawPitch -= 360f;
            }

            return rawPitch;
        }
    }
}
