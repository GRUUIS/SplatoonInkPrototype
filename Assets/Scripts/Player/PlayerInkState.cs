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
        public enum PlayerTraversalState
        {
            Airborne,
            NeutralGround,
            FriendlyGround,
            EnemyGround,
            Swimming,
            FriendlyWall,
            WallSwimming,
        }

        [Header("Team")]
        [SerializeField] private InkTeam playerTeam = InkTeam.TeamA;

        [Header("Probe")]
        [SerializeField] private Transform probeOrigin;
        [SerializeField] private float probeDistance = 2f;
        [SerializeField] private float wallProbeDistance = 0.85f;
        [SerializeField] private float wallProbeHeight = 0.45f;
        [SerializeField] private bool allowWallSwimWhileGrounded;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        [Header("Movement Modifiers")]
        [SerializeField] private float neutralSpeedMultiplier = 1f;
        [SerializeField] private float friendlySpeedMultiplier = 1.2f;
        [SerializeField] private float enemySpeedMultiplier = 0.8f;
        [SerializeField] private float swimSpeedMultiplier = 1.45f;
        [SerializeField] private float friendlyRefillMultiplier = 1.75f;
        [SerializeField] private float neutralRefillMultiplier = 1f;
        [SerializeField] private float swimRefillMultiplier = 2.4f;

        public InkTeam CurrentSurfaceTeam { get; private set; } = InkTeam.Neutral;
        public float CurrentSpeedMultiplier { get; private set; } = 1f;
        public float CurrentRefillMultiplier { get; private set; } = 1f;
        public PlayerTraversalState CurrentTraversalState { get; private set; } = PlayerTraversalState.Airborne;
        public Vector3 CurrentSurfaceNormal { get; private set; } = Vector3.up;
        public bool IsOnFriendlyInk => CurrentSurfaceTeam == playerTeam;
        public bool IsOnEnemyInk => CurrentSurfaceTeam != InkTeam.Neutral
            && CurrentSurfaceTeam != InkTeam.NonInkable
            && CurrentSurfaceTeam != playerTeam;
        public bool IsSwimming => CurrentTraversalState == PlayerTraversalState.Swimming;
        public bool IsWallSwimming => CurrentTraversalState == PlayerTraversalState.WallSwimming;
        public bool CanSwim => IsOnFriendlyInk && CurrentTraversalState != PlayerTraversalState.Airborne;

        private bool swimRequested;
        private Vector3 wallProbeDirection = Vector3.forward;

        private void Reset()
        {
            probeOrigin = transform;
        }

        public void EvaluateSurfaceState()
        {
            CurrentSurfaceTeam = InkTeam.Neutral;
            CurrentSpeedMultiplier = neutralSpeedMultiplier;
            CurrentRefillMultiplier = neutralRefillMultiplier;
            CurrentTraversalState = PlayerTraversalState.Airborne;
            CurrentSurfaceNormal = Vector3.up;

            if (probeOrigin == null)
            {
                return;
            }

            var hasGroundHit = Physics.Raycast(probeOrigin.position, Vector3.down, out var hit, probeDistance, groundMask);
            if (!hasGroundHit)
            {
                EvaluateWallInkState();
                return;
            }

            CurrentTraversalState = PlayerTraversalState.NeutralGround;
            CurrentSurfaceNormal = hit.normal;

            var surface = hit.collider.GetComponentInParent<InkableSurface>();
            if (surface == null || surface.GameplaySurface == null)
            {
                return;
            }

            var uv = surface.GetSurfaceUv(hit.point);
            CurrentSurfaceTeam = surface.GameplaySurface.SampleInkAtUV(uv);

            if (CurrentSurfaceTeam == InkTeam.Neutral)
            {
                CurrentSurfaceTeam = surface.GameplaySurface.SampleClosestInk(hit.point);
            }

            if (CurrentSurfaceTeam == playerTeam)
            {
                CurrentSpeedMultiplier = friendlySpeedMultiplier;
                CurrentRefillMultiplier = friendlyRefillMultiplier;
                CurrentTraversalState = PlayerTraversalState.FriendlyGround;

                if (swimRequested)
                {
                    CurrentSpeedMultiplier = swimSpeedMultiplier;
                    CurrentRefillMultiplier = swimRefillMultiplier;
                    CurrentTraversalState = PlayerTraversalState.Swimming;
                }
            }
            else if (IsOnEnemyInk)
            {
                CurrentSpeedMultiplier = enemySpeedMultiplier;
                CurrentTraversalState = PlayerTraversalState.EnemyGround;
            }

            if (!IsSwimming && allowWallSwimWhileGrounded)
            {
                EvaluateWallInkState();
            }
        }

        public void SetSwimRequested(bool value)
        {
            swimRequested = value;
        }

        public void SetWallProbeDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            wallProbeDirection = direction.normalized;
        }

        private void EvaluateWallInkState()
        {
            var origin = probeOrigin.position + (Vector3.up * wallProbeHeight);
            if (!Physics.Raycast(origin, wallProbeDirection, out var wallHit, wallProbeDistance, groundMask))
            {
                return;
            }

            var surface = wallHit.collider.GetComponentInParent<InkableSurface>();
            if (surface == null || !surface.TreatAsWall || surface.GameplaySurface == null)
            {
                return;
            }

            var uv = surface.GetSurfaceUv(wallHit.point);
            var sampledTeam = surface.GameplaySurface.SampleInkAtUV(uv);
            if (sampledTeam == InkTeam.Neutral)
            {
                sampledTeam = surface.GameplaySurface.SampleClosestInk(wallHit.point);
            }

            if (sampledTeam != playerTeam)
            {
                return;
            }

            CurrentSurfaceTeam = sampledTeam;
            CurrentSurfaceNormal = wallHit.normal;
            CurrentSpeedMultiplier = friendlySpeedMultiplier;
            CurrentRefillMultiplier = friendlyRefillMultiplier;
            CurrentTraversalState = PlayerTraversalState.FriendlyWall;

            if (swimRequested)
            {
                CurrentSpeedMultiplier = swimSpeedMultiplier;
                CurrentRefillMultiplier = swimRefillMultiplier;
                CurrentTraversalState = PlayerTraversalState.WallSwimming;
            }
        }
    }
}
