using SplatoonInkPrototype.Ink.Gameplay;
using UnityEngine;

namespace SplatoonInkPrototype.Ink.Surfaces
{
    /// <summary>
    /// Marks a renderer or collider as a surface that can receive gameplay ink.
    /// </summary>
    public class InkableSurface : MonoBehaviour
    {
        [Header("Surface Rules")]
        [SerializeField] private bool inkable = true;
        [SerializeField] private bool treatAsWall;

        [Header("Surface References")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private InkGameplaySurface gameplaySurface;

        public bool Inkable => inkable;
        public bool TreatAsWall => treatAsWall;
        public Renderer TargetRenderer => targetRenderer;
        public Collider TargetCollider => targetCollider;
        public InkGameplaySurface GameplaySurface => gameplaySurface;

        private void Reset()
        {
            targetRenderer = GetComponentInChildren<Renderer>();
            targetCollider = GetComponent<Collider>();
            gameplaySurface = GetComponent<InkGameplaySurface>();
        }

        private void Awake()
        {
            if (gameplaySurface != null)
            {
                gameplaySurface.EnsureInitialized();
            }
        }

        public InkTeam GetDefaultState()
        {
            return inkable ? InkTeam.Neutral : InkTeam.NonInkable;
        }

        public void Configure(Renderer rendererReference, Collider colliderReference, InkGameplaySurface surfaceReference, bool canInk, bool wallSurface)
        {
            targetRenderer = rendererReference;
            targetCollider = colliderReference;
            gameplaySurface = surfaceReference;
            inkable = canInk;
            treatAsWall = wallSurface;
        }

        public Vector2 GetSurfaceUv(Vector3 worldPoint)
        {
            var referenceTransform = targetRenderer != null ? targetRenderer.transform : transform;
            var localPoint = referenceTransform.InverseTransformPoint(worldPoint);
            var localBounds = GetLocalSurfaceBounds(referenceTransform);

            if (treatAsWall)
            {
                return new Vector2(
                    NormalizeLocalAxis(localPoint.x, localBounds.min.x, localBounds.max.x),
                    NormalizeLocalAxis(localPoint.y, localBounds.min.y, localBounds.max.y));
            }

            return new Vector2(
                NormalizeLocalAxis(localPoint.x, localBounds.min.x, localBounds.max.x),
                NormalizeLocalAxis(localPoint.z, localBounds.min.z, localBounds.max.z));
        }

        public Vector2 GetSurfaceDirection(Vector3 worldDirection)
        {
            var referenceTransform = targetRenderer != null ? targetRenderer.transform : transform;
            var localDirection = referenceTransform.InverseTransformDirection(worldDirection);
            var direction = treatAsWall
                ? new Vector2(localDirection.x, localDirection.y)
                : new Vector2(localDirection.x, localDirection.z);

            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector2.right;
            }

            return direction.normalized;
        }

        private static Bounds GetLocalSurfaceBounds(Transform referenceTransform)
        {
            var meshFilter = referenceTransform.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return meshFilter.sharedMesh.bounds;
            }

            return new Bounds(Vector3.zero, Vector3.one);
        }

        private static float NormalizeLocalAxis(float value, float min, float max)
        {
            var range = Mathf.Max(0.0001f, max - min);
            return Mathf.Clamp01((value - min) / range);
        }
    }
}
