using SplatoonInkPrototype.Ink.Gameplay;
using SplatoonInkPrototype.Ink.Surfaces;
using UnityEngine;

namespace SplatoonInkPrototype.Player
{
    /// <summary>
    /// Samples the ink under the player and exposes a simple movement state.
    /// This is where swim, refill, and enemy slowdown rules will grow from.
    /// </summary>
    public class PlayerInkState : MonoBehaviour
    {
        [Header("Team")]
        [SerializeField] private InkTeam playerTeam = InkTeam.TeamA;

        [Header("Probe")]
        [SerializeField] private Transform probeOrigin;
        [SerializeField] private float probeDistance = 2f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        [Header("Movement Modifiers")]
        [SerializeField] private float neutralSpeedMultiplier = 1f;
        [SerializeField] private float friendlySpeedMultiplier = 1.2f;
        [SerializeField] private float enemySpeedMultiplier = 0.8f;
        [SerializeField] private float friendlyRefillMultiplier = 1.75f;
        [SerializeField] private float neutralRefillMultiplier = 1f;

        public InkTeam CurrentSurfaceTeam { get; private set; } = InkTeam.Neutral;
        public float CurrentSpeedMultiplier { get; private set; } = 1f;
        public float CurrentRefillMultiplier { get; private set; } = 1f;
        public bool IsOnFriendlyInk => CurrentSurfaceTeam == playerTeam;
        public bool IsOnEnemyInk => CurrentSurfaceTeam != InkTeam.Neutral
            && CurrentSurfaceTeam != InkTeam.NonInkable
            && CurrentSurfaceTeam != playerTeam;

        private void Reset()
        {
            probeOrigin = transform;
        }

        private void Update()
        {
            EvaluateSurfaceState();
        }

        public void EvaluateSurfaceState()
        {
            CurrentSurfaceTeam = InkTeam.Neutral;
            CurrentSpeedMultiplier = neutralSpeedMultiplier;
            CurrentRefillMultiplier = neutralRefillMultiplier;

            if (probeOrigin == null)
            {
                return;
            }

            if (!Physics.Raycast(probeOrigin.position, Vector3.down, out var hit, probeDistance, groundMask))
            {
                return;
            }

            var surface = hit.collider.GetComponentInParent<InkableSurface>();
            if (surface == null || surface.GameplaySurface == null)
            {
                return;
            }

            var uv = hit.textureCoord;
            if (uv == Vector2.zero)
            {
                uv = surface.GetSurfaceUv(hit.point);
            }

            CurrentSurfaceTeam = surface.GameplaySurface.SampleInkAtUV(uv);

            if (CurrentSurfaceTeam == InkTeam.Neutral)
            {
                CurrentSurfaceTeam = surface.GameplaySurface.SampleClosestInk(hit.point);
            }

            if (CurrentSurfaceTeam == playerTeam)
            {
                CurrentSpeedMultiplier = friendlySpeedMultiplier;
                CurrentRefillMultiplier = friendlyRefillMultiplier;
            }
            else if (IsOnEnemyInk)
            {
                CurrentSpeedMultiplier = enemySpeedMultiplier;
            }
        }
    }
}
