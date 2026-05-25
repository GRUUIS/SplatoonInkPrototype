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
            var scale = referenceTransform.lossyScale;
            scale.x = Mathf.Max(0.01f, scale.x);
            scale.y = Mathf.Max(0.01f, scale.y);
            scale.z = Mathf.Max(0.01f, scale.z);

            if (treatAsWall)
            {
                return new Vector2(
                    Mathf.Clamp01(localPoint.x / scale.x + 0.5f),
                    Mathf.Clamp01(localPoint.y / scale.y + 0.5f));
            }

            return new Vector2(
                Mathf.Clamp01(localPoint.x / scale.x + 0.5f),
                Mathf.Clamp01(localPoint.z / scale.z + 0.5f));
        }
    }
}
