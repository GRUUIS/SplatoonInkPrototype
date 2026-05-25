using SplatoonInkPrototype.Ink.Gameplay;
using SplatoonInkPrototype.Ink.Surfaces;
using SplatoonInkPrototype.Player;
using System.Collections.Generic;
using UnityEngine;

namespace SplatoonInkPrototype.Weapons
{
    /// <summary>
    /// Fires visible ink projectiles and forwards projectile impacts into the gameplay ink layer.
    /// </summary>
    public class InkWeaponEmitter : MonoBehaviour
    {
        private sealed class ActiveProjectile
        {
            public GameObject Visual;
            public Vector3 PreviousPosition;
            public Vector3 Velocity;
            public float DistanceTravelled;
            public float MaxDistance;
            public float InkRadius;
            public int SplashCount;
            public float SplashRandomness;
            public float ImpactScale;
            public InkTeam Team;
        }

        private struct FirePointSample
        {
            public Vector3 Position;
            public float Time;
        }

        public enum ShotResultType
        {
            InkedSurface,
            HitBlockedSurface,
            Missed,
            OutOfInk,
        }

        public struct ShotResult
        {
            public ShotResultType ResultType;
            public Vector3 Origin;
            public Vector3 EndPoint;
            public Vector3 Normal;
            public InkTeam Team;
        }

        [Header("References")]
        [SerializeField] private InkWeaponConfig weaponConfig;
        [SerializeField] private InkHitResolver hitResolver;
        [SerializeField] private Transform firePoint;
        [SerializeField] private PlayerInkTank inkTank;
        [SerializeField] private PlayerInkState inkState;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private InkWeaponFeedbackPool feedbackPool;

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
        [SerializeField] private float fallbackTracerWidth = 0.12f;
        [SerializeField] private float fallbackTracerDuration = 0.07f;
        [SerializeField] private float fallbackImpactScale = 0.4f;
        [SerializeField] private float fallbackProjectileSpeed = 28f;
        [SerializeField] private float fallbackProjectileGravity = 18f;
        [SerializeField, Range(0f, 1f)] private float fallbackProjectileArcPreference = 0.2f;
        [SerializeField] private float fallbackProjectileScale = 0.18f;
        [SerializeField] private bool solveBallisticToCrosshair = false;
        [SerializeField] private int projectilePoolPrewarmCount = 16;

        [Header("Launch Kinematics")]
        [SerializeField] private bool usePreviousFrameFireOrigin = true;
        [SerializeField, Range(0, 5)] private int fireOriginFrameLag = 1;
        [SerializeField, Range(0f, 1f)] private float inheritedFirePointVelocityScale = 1f;

        [Header("Debug")]
        [SerializeField] private bool fireOnLeftMouse = true;
        [SerializeField] private bool drawBallisticDebug = true;
        [SerializeField] private int debugTrajectorySteps = 20;

        public event System.Action<ShotResult> ShotResolved;

        private readonly List<ActiveProjectile> projectiles = new();
        private readonly List<GameObject> projectileVisualPool = new();
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private readonly FirePointSample[] firePointHistory = new FirePointSample[8];
        private float nextShotTime;
        private int firePointHistoryCount;
        private int firePointHistoryWriteIndex;
        private Vector3 lastAimDirection = Vector3.forward;
        private Vector3 lastAimTarget;
        private Vector3 lastShotOrigin;
        private Vector3 lastLaunchVelocity;
        private float lastShotTime;
        private bool hasLastTrajectory;
        private Material projectileMaterial;

        public bool IsActivelyAiming => Time.time - lastShotTime < 0.15f;

        private void Awake()
        {
            RecordFirePointSample(Time.time);
            PrewarmProjectilePool();
        }

        private void OnEnable()
        {
            ResetFirePointHistory();
            RecordFirePointSample(Time.time);
        }

        private void Reset()
        {
            firePoint = transform;
            hitResolver = GetComponent<InkHitResolver>();
            inkTank = GetComponent<PlayerInkTank>();
            inkState = GetComponent<PlayerInkState>();
            aimCamera = Camera.main;
            feedbackPool = GetComponent<InkWeaponFeedbackPool>();
        }

        private void Update()
        {
            UpdateProjectiles();

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

        private void LateUpdate()
        {
            RecordFirePointSample(Time.time);
        }

        public void Fire()
        {
            if (IsInkAttackBlockedByTraversal())
            {
                return;
            }

            var fireRate = weaponConfig != null ? weaponConfig.FireRate : fallbackFireRate;
            var range = weaponConfig != null ? weaponConfig.Range : fallbackRange;
            var spread = weaponConfig != null ? weaponConfig.Spread : fallbackSpread;
            var inkRadius = weaponConfig != null ? weaponConfig.InkRadius : fallbackInkRadius;
            var inkCost = weaponConfig != null ? weaponConfig.InkCost : fallbackInkCost;
            var tracerWidth = weaponConfig != null ? weaponConfig.TracerWidth : fallbackTracerWidth;
            var tracerDuration = weaponConfig != null ? weaponConfig.TracerDuration : fallbackTracerDuration;
            var impactScale = weaponConfig != null ? weaponConfig.ImpactScale : fallbackImpactScale;
            var projectileSpeed = weaponConfig != null ? weaponConfig.ProjectileSpeed : fallbackProjectileSpeed;
            var projectileGravity = weaponConfig != null ? weaponConfig.ProjectileGravity : fallbackProjectileGravity;
            var projectileArcPreference = weaponConfig != null ? weaponConfig.ProjectileArcPreference : fallbackProjectileArcPreference;
            var shotOrigin = ResolveFireOrigin(out var inheritedFirePointVelocity);

            if (inkTank != null && !inkTank.TryConsume(inkCost))
            {
                var noInkResult = new ShotResult
                {
                    ResultType = ShotResultType.OutOfInk,
                    Origin = shotOrigin,
                    EndPoint = shotOrigin,
                    Normal = Vector3.up,
                    Team = ownerTeam,
                };

                ShotResolved?.Invoke(noInkResult);
                return;
            }

            nextShotTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));
            lastShotTime = Time.time;

            var aimDirection = ResolveAimDirection();
            aimDirection = (aimDirection + GetSpreadOffset(spread)).normalized;
            var aimPoint = shotOrigin + (aimDirection * range);

            var launchVelocity = solveBallisticToCrosshair
                ? ResolveLaunchVelocity(shotOrigin, aimPoint, projectileSpeed, projectileGravity, projectileArcPreference)
                : aimDirection * Mathf.Max(0.01f, projectileSpeed);
            launchVelocity += inheritedFirePointVelocity * inheritedFirePointVelocityScale;
            var direction = launchVelocity.sqrMagnitude > 0.001f ? launchVelocity.normalized : firePoint.forward;
            lastAimDirection = direction;
            lastAimTarget = aimPoint;
            lastShotOrigin = shotOrigin;
            lastLaunchVelocity = launchVelocity;
            hasLastTrajectory = true;

            LaunchProjectile(
                shotOrigin,
                launchVelocity,
                range,
                inkRadius,
                splashCount: weaponConfig != null ? weaponConfig.SplashCount : fallbackSplashCount,
                splashRandomness: weaponConfig != null ? weaponConfig.SplashRandomness : fallbackSplashRandomness,
                impactScale,
                tracerWidth,
                tracerDuration);
        }

        private void LaunchProjectile(
            Vector3 origin,
            Vector3 launchVelocity,
            float range,
            float inkRadius,
            int splashCount,
            float splashRandomness,
            float impactScale,
            float tracerWidth,
            float tracerDuration)
        {
            var direction = launchVelocity.sqrMagnitude > 0.001f ? launchVelocity.normalized : firePoint.forward;
            var projectile = new ActiveProjectile
            {
                Visual = CreateProjectileVisual(),
                PreviousPosition = origin,
                Velocity = launchVelocity,
                MaxDistance = range,
                InkRadius = inkRadius,
                SplashCount = splashCount,
                SplashRandomness = splashRandomness,
                ImpactScale = impactScale,
                Team = ownerTeam,
            };

            projectile.Visual.transform.position = origin;
            projectile.Visual.transform.localScale = Vector3.one * fallbackProjectileScale;
            projectile.Visual.SetActive(true);
            projectiles.Add(projectile);

            PlayFeedback(origin, origin + (direction * 0.45f), direction, ownerTeam, tracerWidth, tracerDuration, impactScale, false);
        }

        private void UpdateProjectiles()
        {
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                var previousPosition = projectile.Visual.transform.position;
                var projectileGravity = weaponConfig != null ? weaponConfig.ProjectileGravity : fallbackProjectileGravity;
                projectile.Velocity += Vector3.down * (projectileGravity * Time.deltaTime);
                var nextPosition = previousPosition + (projectile.Velocity * Time.deltaTime);
                var segment = nextPosition - previousPosition;
                var segmentDistance = segment.magnitude;

                if (segmentDistance > 0.0001f
                    && TryRaycastIgnoringOwner(previousPosition, segment / segmentDistance, segmentDistance, out var hit))
                {
                    ResolveProjectileImpact(projectile, hit);
                    RecycleProjectile(i);
                    continue;
                }

                projectile.Visual.transform.position = nextPosition;
                projectile.PreviousPosition = previousPosition;
                projectile.DistanceTravelled += segmentDistance;

                if (projectile.DistanceTravelled >= projectile.MaxDistance)
                {
                    ShotResolved?.Invoke(new ShotResult
                    {
                        ResultType = ShotResultType.Missed,
                        Origin = previousPosition,
                        EndPoint = nextPosition,
                        Normal = Vector3.up,
                        Team = projectile.Team,
                    });
                    RecycleProjectile(i);
                }
            }
        }

        private void ResolveProjectileImpact(ActiveProjectile projectile, RaycastHit hit)
        {
            if (!hitResolver.TryResolveSurfaceHit(
                hit,
                projectile.Team,
                projectile.InkRadius,
                projectile.Velocity.normalized,
                out var inkableSurface,
                out var surfaceHit))
            {
                PlayFeedback(projectile.PreviousPosition, hit.point, hit.normal, projectile.Team, fallbackTracerWidth, fallbackTracerDuration, projectile.ImpactScale, false);
                ShotResolved?.Invoke(new ShotResult
                {
                    ResultType = ShotResultType.HitBlockedSurface,
                    Origin = projectile.PreviousPosition,
                    EndPoint = hit.point,
                    Normal = hit.normal,
                    Team = projectile.Team,
                });
                return;
            }

            inkableSurface.GameplaySurface.BeginPaintBatch();
            try
            {
                inkableSurface.GameplaySurface.ApplyStamp(surfaceHit);
                SpawnSecondarySplashes(inkableSurface, surfaceHit, projectile.SplashCount, projectile.SplashRandomness, projectile.InkRadius);
            }
            finally
            {
                inkableSurface.GameplaySurface.EndPaintBatch();
            }
            PlayFeedback(projectile.PreviousPosition, hit.point, hit.normal, projectile.Team, fallbackTracerWidth, fallbackTracerDuration, projectile.ImpactScale, true);
            ShotResolved?.Invoke(new ShotResult
            {
                ResultType = ShotResultType.InkedSurface,
                Origin = projectile.PreviousPosition,
                EndPoint = hit.point,
                Normal = hit.normal,
                Team = projectile.Team,
            });
        }

        private void RecycleProjectile(int index)
        {
            var visual = projectiles[index].Visual;
            visual.SetActive(false);
            projectileVisualPool.Add(visual);
            projectiles.RemoveAt(index);
        }

        private GameObject CreateProjectileVisual()
        {
            for (var i = 0; i < projectileVisualPool.Count; i++)
            {
                var pooledVisual = projectileVisualPool[i];
                if (pooledVisual == null)
                {
                    continue;
                }

                projectileVisualPool.RemoveAt(i);
                return pooledVisual;
            }

            return CreateNewProjectileVisual();
        }

        private void PrewarmProjectilePool()
        {
            for (var i = projectileVisualPool.Count; i < projectilePoolPrewarmCount; i++)
            {
                var projectileObject = CreateNewProjectileVisual();
                projectileObject.SetActive(false);
                projectileVisualPool.Add(projectileObject);
            }
        }

        private GameObject CreateNewProjectileVisual()
        {
            EnsureProjectileMaterial();
            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "InkProjectile";
            projectileObject.transform.SetParent(transform, false);

            var collider = projectileObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = projectileObject.GetComponent<Renderer>();
            renderer.sharedMaterial = projectileMaterial;
            return projectileObject;
        }

        private void EnsureProjectileMaterial()
        {
            if (projectileMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            projectileMaterial = new Material(shader)
            {
                name = "InkProjectileMaterial",
                color = new Color(0.2f, 0.8f, 1f, 1f)
            };
        }

        private void SpawnSecondarySplashes(InkableSurface inkableSurface, InkSurfaceHit originHit, int splashCount, float splashRandomness, float inkRadius)
        {
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
                splashHit.UV = inkableSurface.GetSurfaceUv(splashHit.WorldPoint);
                splashHit.Radius = inkRadius * 0.5f;
                inkableSurface.GameplaySurface.ApplyStamp(splashHit);
            }
        }

        public void Configure(InkHitResolver resolver, Transform point, PlayerInkTank tank)
        {
            hitResolver = resolver;
            firePoint = point;
            inkTank = tank;
            inkState = tank != null ? tank.GetComponent<PlayerInkState>() : GetComponent<PlayerInkState>();
            aimCamera = Camera.main;
        }

        public void Configure(InkHitResolver resolver, Transform point, PlayerInkTank tank, Camera cameraReference, InkWeaponFeedbackPool pool)
        {
            hitResolver = resolver;
            firePoint = point;
            inkTank = tank;
            inkState = tank != null ? tank.GetComponent<PlayerInkState>() : GetComponent<PlayerInkState>();
            aimCamera = cameraReference;
            feedbackPool = pool;
            ResetFirePointHistory();
            RecordFirePointSample(Time.time);
        }

        public Vector3 GetFlatAimDirection()
        {
            var flat = aimCamera != null ? aimCamera.transform.forward : lastAimDirection;
            flat.y = 0f;
            return flat.sqrMagnitude < 0.001f ? transform.forward : flat.normalized;
        }

        private Vector3 ResolveAimDirection()
        {
            if (aimCamera == null && Camera.main != null)
            {
                aimCamera = Camera.main;
            }

            if (aimCamera == null)
            {
                return firePoint.forward;
            }

            return aimCamera.transform.forward.normalized;
        }

        private bool IsInkAttackBlockedByTraversal()
        {
            if (inkState == null)
            {
                inkState = GetComponent<PlayerInkState>();
            }

            return inkState != null && (inkState.IsSwimming || inkState.IsWallSwimming);
        }

        private Vector3 ResolveFireOrigin(out Vector3 inheritedVelocity)
        {
            inheritedVelocity = ResolveFirePointVelocity();

            if (!usePreviousFrameFireOrigin || firePointHistoryCount == 0)
            {
                return firePoint.position;
            }

            var lag = Mathf.Clamp(fireOriginFrameLag, 0, firePointHistoryCount - 1);
            return GetFirePointHistorySample(lag).Position;
        }

        private Vector3 ResolveFirePointVelocity()
        {
            if (firePointHistoryCount == 0)
            {
                return Vector3.zero;
            }

            var previousSample = GetFirePointHistorySample(0);
            var deltaTime = Time.time - previousSample.Time;
            if (deltaTime <= 0.0001f)
            {
                return Vector3.zero;
            }

            return (firePoint.position - previousSample.Position) / deltaTime;
        }

        private void RecordFirePointSample(float sampleTime)
        {
            if (firePoint == null)
            {
                return;
            }

            firePointHistory[firePointHistoryWriteIndex] = new FirePointSample
            {
                Position = firePoint.position,
                Time = sampleTime,
            };

            firePointHistoryWriteIndex = (firePointHistoryWriteIndex + 1) % firePointHistory.Length;
            firePointHistoryCount = Mathf.Min(firePointHistoryCount + 1, firePointHistory.Length);
        }

        private FirePointSample GetFirePointHistorySample(int framesAgo)
        {
            var clampedFramesAgo = Mathf.Clamp(framesAgo, 0, firePointHistoryCount - 1);
            var index = firePointHistoryWriteIndex - 1 - clampedFramesAgo;
            while (index < 0)
            {
                index += firePointHistory.Length;
            }

            return firePointHistory[index % firePointHistory.Length];
        }

        private void ResetFirePointHistory()
        {
            firePointHistoryCount = 0;
            firePointHistoryWriteIndex = 0;
        }

        private Vector3 ResolveLaunchVelocity(
            Vector3 origin,
            Vector3 target,
            float projectileSpeed,
            float projectileGravity,
            float arcPreference)
        {
            var speed = Mathf.Max(0.01f, projectileSpeed);
            if (projectileGravity <= 0.001f)
            {
                return (target - origin).normalized * speed;
            }

            var delta = target - origin;
            var horizontalDelta = new Vector3(delta.x, 0f, delta.z);
            var horizontalDistance = horizontalDelta.magnitude;

            if (horizontalDistance < 0.001f)
            {
                return delta.normalized * speed;
            }

            var gravity = projectileGravity;
            var speedSquared = speed * speed;
            var discriminant = (speedSquared * speedSquared)
                - gravity * ((gravity * horizontalDistance * horizontalDistance) + (2f * delta.y * speedSquared));

            if (discriminant < 0f)
            {
                return delta.normalized * speed;
            }

            var root = Mathf.Sqrt(discriminant);
            var lowAngleTan = (speedSquared - root) / (gravity * horizontalDistance);
            var highAngleTan = (speedSquared + root) / (gravity * horizontalDistance);
            var angleTan = Mathf.Lerp(lowAngleTan, highAngleTan, Mathf.Clamp01(arcPreference));
            var cos = 1f / Mathf.Sqrt(1f + (angleTan * angleTan));
            var sin = angleTan * cos;

            return (horizontalDelta / horizontalDistance * speed * cos) + (Vector3.up * speed * sin);
        }

        private Vector3 GetSpreadOffset(float spread)
        {
            if (aimCamera == null)
            {
                return Random.insideUnitSphere * spread;
            }

            return (aimCamera.transform.right * Random.Range(-spread, spread))
                + (aimCamera.transform.up * Random.Range(-spread, spread));
        }

        private bool TryRaycastIgnoringOwner(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            var hitCount = Physics.RaycastNonAlloc(origin, direction, hitBuffer, distance, hitMask, QueryTriggerInteraction.Ignore);
            return TryGetClosestExternalHit(hitCount, out closestHit);
        }

        private bool TryGetClosestExternalHit(int hitCount, out RaycastHit closestHit)
        {
            closestHit = default;
            var closestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];
                if (hit.collider == null || IsOwnCollider(hit.collider) || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
            }

            return closestDistance < float.MaxValue;
        }

        private bool IsOwnCollider(Collider candidate)
        {
            return candidate.transform == transform || candidate.transform.IsChildOf(transform);
        }

        private void PlayFeedback(
            Vector3 origin,
            Vector3 endPoint,
            Vector3 normal,
            InkTeam team,
            float tracerWidth,
            float tracerDuration,
            float impactScale,
            bool inkedSurface)
        {
            if (feedbackPool == null)
            {
                return;
            }

            feedbackPool.PlayTracer(origin, endPoint, team, tracerWidth, tracerDuration);
            feedbackPool.PlayImpact(endPoint, normal, inkedSurface ? team : InkTeam.Neutral, impactScale, inkedSurface);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawBallisticDebug || !hasLastTrajectory)
            {
                return;
            }

            var projectileGravity = weaponConfig != null ? weaponConfig.ProjectileGravity : fallbackProjectileGravity;
            var range = weaponConfig != null ? weaponConfig.Range : fallbackRange;
            var speed = Mathf.Max(0.01f, lastLaunchVelocity.magnitude);
            var duration = Mathf.Min(2f, range / speed);
            var steps = Mathf.Max(2, debugTrajectorySteps);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastAimTarget, 0.18f);

            Gizmos.color = Color.cyan;
            var previous = lastShotOrigin;
            for (var i = 1; i <= steps; i++)
            {
                var t = duration * i / steps;
                var current = lastShotOrigin
                    + (lastLaunchVelocity * t)
                    + (Vector3.down * (0.5f * projectileGravity * t * t));
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
