using SplatoonInkPrototype.Ink.Gameplay;
using SplatoonInkPrototype.Ink.Surfaces;
using SplatoonInkPrototype.Player;
using UnityEngine;

namespace SplatoonInkPrototype.Weapons
{
    /// <summary>
    /// Fires simple raycast shots and forwards valid surface hits into the gameplay ink layer.
    /// This is the first-pass weapon loop for prototype validation.
    /// </summary>
    public class InkWeaponEmitter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InkWeaponConfig weaponConfig;
        [SerializeField] private InkHitResolver hitResolver;
        [SerializeField] private Transform firePoint;
        [SerializeField] private PlayerInkTank inkTank;

        [Header("Gameplay")]
        [SerializeField] private InkTeam ownerTeam = InkTeam.TeamA;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float fallbackFireRate = 6f;
        [SerializeField] private float fallbackRange = 20f;
        [SerializeField] private float fallbackSpread = 0.02f;
        [SerializeField] private float fallbackInkRadius = 1.25f;
        [SerializeField] private int fallbackSplashCount = 4;
        [SerializeField] private float fallbackSplashRandomness = 0.45f;
        [SerializeField] private float fallbackInkCost = 0.08f;

        [Header("Debug")]
        [SerializeField] private bool fireOnLeftMouse = true;

        private float nextShotTime;

        private void Reset()
        {
            firePoint = transform;
            hitResolver = GetComponent<InkHitResolver>();
            inkTank = GetComponent<PlayerInkTank>();
        }

        private void Update()
        {
            if (!fireOnLeftMouse || hitResolver == null || firePoint == null)
            {
                return;
            }

            if (!Input.GetMouseButton(0) || Time.time < nextShotTime)
            {
                return;
            }

            Fire();
        }

        public void Fire()
        {
            var fireRate = weaponConfig != null ? weaponConfig.FireRate : fallbackFireRate;
            var range = weaponConfig != null ? weaponConfig.Range : fallbackRange;
            var spread = weaponConfig != null ? weaponConfig.Spread : fallbackSpread;
            var inkRadius = weaponConfig != null ? weaponConfig.InkRadius : fallbackInkRadius;
            var inkCost = weaponConfig != null ? weaponConfig.InkCost : fallbackInkCost;

            if (inkTank != null && !inkTank.TryConsume(inkCost))
            {
                return;
            }

            nextShotTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));

            var direction = firePoint.forward;
            direction += Random.insideUnitSphere * spread;
            direction.Normalize();

            if (!Physics.Raycast(firePoint.position, direction, out var hit, range, hitMask))
            {
                return;
            }

            if (!hitResolver.TryResolveSurfaceHit(
                hit,
                ownerTeam,
                inkRadius,
                out var inkableSurface,
                out var surfaceHit))
            {
                return;
            }

            inkableSurface.GameplaySurface.ApplyStamp(surfaceHit);

            // Secondary splashes are still gameplay-side for now.
            // Later we can split these into gameplay stamps and visual-only splashes.
            SpawnSecondarySplashes(inkableSurface, surfaceHit);
        }

        private void SpawnSecondarySplashes(InkableSurface inkableSurface, InkSurfaceHit originHit)
        {
            var splashCount = weaponConfig != null ? weaponConfig.SplashCount : fallbackSplashCount;
            var splashRandomness = weaponConfig != null ? weaponConfig.SplashRandomness : fallbackSplashRandomness;
            var inkRadius = weaponConfig != null ? weaponConfig.InkRadius : fallbackInkRadius;

            var tangent = Vector3.Cross(originHit.Normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.001f)
            {
                tangent = Vector3.Cross(originHit.Normal, Vector3.right);
            }

            tangent.Normalize();
            var bitangent = Vector3.Cross(originHit.Normal, tangent).normalized;

            for (var i = 0; i < splashCount; i++)
            {
                var offset = Random.insideUnitCircle * splashRandomness;
                var splashHit = originHit;
                splashHit.WorldPoint += (tangent * offset.x) + (bitangent * offset.y);
                splashHit.UV += offset * 0.05f;
                splashHit.UV = new Vector2(Mathf.Clamp01(splashHit.UV.x), Mathf.Clamp01(splashHit.UV.y));
                splashHit.Radius = inkRadius * 0.5f;
                inkableSurface.GameplaySurface.ApplyStamp(splashHit);
            }
        }

        public void Configure(InkHitResolver resolver, Transform point, PlayerInkTank tank)
        {
            hitResolver = resolver;
            firePoint = point;
            inkTank = tank;
        }
    }
}
