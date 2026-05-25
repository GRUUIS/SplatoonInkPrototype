using System.Collections.Generic;
using Action = System.Action<UnityEngine.RectInt>;
using UnityEngine;

namespace SplatoonInkPrototype.Ink.Gameplay
{
    /// <summary>
    /// Stores authoritative gameplay ink information for a surface.
    /// In the first pass we keep stamp records in memory.
    /// Later this class can own a RenderTexture-backed ink mask.
    /// </summary>
    public class InkGameplaySurface : MonoBehaviour
    {
        [System.Serializable]
        public struct InkStamp
        {
            public Vector3 worldPoint;
            public Vector2 uv;
            public float radius;
            public InkTeam team;
            public bool isWall;
        }

        [Header("Surface")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private int textureResolution = 256;
        [SerializeField] private bool useWorldSpaceTexelDensity = true;
        [SerializeField] private float texelsPerWorldUnit = 16f;
        [SerializeField] private int maxTextureSize = 1024;

        [Header("Team Colors")]
        [SerializeField] private Color teamAColor = new(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private Color teamBColor = new(1f, 0.35f, 0.8f, 1f);

        [Header("Ink Shape")]
        [SerializeField] private float edgeNoiseScale = 0.08f;
        [SerializeField] private float edgeNoiseStrength = 0.18f;
        [SerializeField] private int autoSplatterCount = 3;
        [SerializeField] private float autoSplatterDistance = 0.18f;
        [SerializeField] private Vector2 autoSplatterRadiusRange = new(0.3f, 0.55f);
        [SerializeField] private int wallDripCount = 3;
        [SerializeField] private float wallDripLength = 0.1f;
        [SerializeField] private int groundSmearCount = 3;
        [SerializeField] private float groundSmearDistance = 0.045f;
        [SerializeField] private int directionalTrailCount = 5;
        [SerializeField] private float directionalTrailLength = 1.15f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private List<InkStamp> stamps = new();

        public IReadOnlyList<InkStamp> Stamps => stamps;
        public Texture2D RuntimeInkMask => paintMap != null ? paintMap.ColorTexture : null;
        public Texture2D RuntimeInkHeight => paintMap != null ? paintMap.HeightTexture : null;
        public Renderer TargetRenderer => targetRenderer;
        public event Action InkTextureChanged;

        private InkSurfacePaintMap paintMap;
        private bool isInitialized;
        private int paintBatchDepth;
        private bool hasPendingPaintChange;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        public void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (targetRenderer == null)
            {
                return;
            }

            var textureSize = ResolvePaintTextureSize();
            paintMap = new InkSurfacePaintMap(
                name,
                textureSize.x,
                textureSize.y,
                teamAColor,
                teamBColor,
                edgeNoiseScale,
                edgeNoiseStrength,
                autoSplatterCount,
                autoSplatterDistance,
                autoSplatterRadiusRange,
                wallDripCount,
                wallDripLength,
                groundSmearCount,
                groundSmearDistance,
                directionalTrailCount,
                directionalTrailLength);

            isInitialized = true;
        }

        public void BeginPaintBatch()
        {
            paintBatchDepth++;
        }

        public void EndPaintBatch()
        {
            paintBatchDepth = Mathf.Max(0, paintBatchDepth - 1);
            if (paintBatchDepth == 0 && hasPendingPaintChange)
            {
                var dirtyRect = paintMap.CommitTexture();
                hasPendingPaintChange = false;
                InkTextureChanged?.Invoke(dirtyRect);
            }
        }

        public void ApplyStamp(InkSurfaceHit hit)
        {
            EnsureInitialized();

            // This lightweight storage lets us validate the gameplay loop
            // before wiring the class into a render texture painting path.
            var stamp = new InkStamp
            {
                worldPoint = hit.WorldPoint,
                uv = hit.UV,
                radius = hit.Radius,
                team = hit.Team,
                isWall = hit.IsWall,
            };

            stamps.Add(stamp);
            var commitImmediately = paintBatchDepth == 0;
            var dirtyRect = paintMap.ApplyStamp(hit, GetPixelsPerWorldUnit(), commitImmediately);
            if (commitImmediately)
            {
                InkTextureChanged?.Invoke(dirtyRect);
                return;
            }

            hasPendingPaintChange = true;
        }

        public InkTeam SampleClosestInk(Vector3 worldPosition, float maxDistance = 1.5f)
        {
            var bestDistance = float.MaxValue;
            var bestTeam = InkTeam.Neutral;

            for (var i = 0; i < stamps.Count; i++)
            {
                var stamp = stamps[i];
                var distance = Vector3.Distance(worldPosition, stamp.worldPoint);

                if (distance > stamp.radius || distance > maxDistance || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestTeam = stamp.team;
            }

            return bestTeam;
        }

        public InkTeam SampleInkAtUV(Vector2 uv)
        {
            if (!TrySampleInkAtUV(uv, out var team))
            {
                return InkTeam.Neutral;
            }

            return team;
        }

        public bool TrySampleInkAtUV(Vector2 uv, out InkTeam team)
        {
            EnsureInitialized();

            team = InkTeam.Neutral;
            if (!isInitialized)
            {
                return false;
            }

            team = paintMap.SampleInkAtUV(uv);
            return true;
        }

        private Vector2 GetPixelsPerWorldUnit()
        {
            if (targetRenderer == null)
            {
                return Vector2.one * textureResolution;
            }

            var surfaceSize = GetPaintSurfaceWorldSize();
            return new Vector2(
                paintMap.Width / Mathf.Max(0.01f, surfaceSize.x),
                paintMap.Height / Mathf.Max(0.01f, surfaceSize.y));
        }

        private Vector2Int ResolvePaintTextureSize()
        {
            if (!useWorldSpaceTexelDensity)
            {
                var fallbackSize = Mathf.Max(32, textureResolution);
                return new Vector2Int(fallbackSize, fallbackSize);
            }

            var surfaceSize = GetPaintSurfaceWorldSize();
            return new Vector2Int(
                Mathf.Clamp(Mathf.CeilToInt(surfaceSize.x * texelsPerWorldUnit), 32, maxTextureSize),
                Mathf.Clamp(Mathf.CeilToInt(surfaceSize.y * texelsPerWorldUnit), 32, maxTextureSize));
        }

        private Vector2 GetPaintSurfaceWorldSize()
        {
            if (targetRenderer == null)
            {
                return Vector2.one;
            }

            var bounds = targetRenderer.bounds.size;
            var secondaryAxis = bounds.y > bounds.z * 1.5f ? bounds.y : bounds.z;
            return new Vector2(
                Mathf.Max(0.01f, bounds.x),
                Mathf.Max(0.01f, secondaryAxis));
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
            {
                return;
            }

            for (var i = 0; i < stamps.Count; i++)
            {
                var stamp = stamps[i];
                Gizmos.color = stamp.team switch
                {
                    InkTeam.TeamA => new Color(0.2f, 0.8f, 1f, 0.5f),
                    InkTeam.TeamB => new Color(1f, 0.35f, 0.8f, 0.5f),
                    _ => new Color(1f, 1f, 1f, 0.25f),
                };

                Gizmos.DrawSphere(stamp.worldPoint, stamp.radius * 0.25f);
            }
        }
    }

    /// <summary>
    /// Owns the authoritative painted texture data for one inkable surface.
    /// Gameplay systems query this map, while visuals can present it however they like.
    /// </summary>
    internal sealed class InkSurfacePaintMap
    {
        private readonly int textureWidth;
        private readonly int textureHeight;
        private readonly Color teamAColor;
        private readonly Color teamBColor;
        private readonly float edgeNoiseScale;
        private readonly float edgeNoiseStrength;
        private readonly int autoSplatterCount;
        private readonly float autoSplatterDistance;
        private readonly Vector2 autoSplatterRadiusRange;
        private readonly int wallDripCount;
        private readonly float wallDripLength;
        private readonly int groundSmearCount;
        private readonly float groundSmearDistance;
        private readonly int directionalTrailCount;
        private readonly float directionalTrailLength;

        private readonly InkTeam[] inkTeamLookup;
        private readonly Color[] inkPixels;
        private readonly Color[] heightPixels;
        private readonly Texture2D colorTexture;
        private readonly Texture2D heightTexture;
        private bool hasDirtyPixels;
        private int dirtyMinX;
        private int dirtyMinY;
        private int dirtyMaxX;
        private int dirtyMaxY;

        public InkSurfacePaintMap(
            string surfaceName,
            int width,
            int height,
            Color teamAColorValue,
            Color teamBColorValue,
            float edgeNoiseScaleValue,
            float edgeNoiseStrengthValue,
            int autoSplatterCountValue,
            float autoSplatterDistanceValue,
            Vector2 autoSplatterRadiusRangeValue,
            int wallDripCountValue,
            float wallDripLengthValue,
            int groundSmearCountValue,
            float groundSmearDistanceValue,
            int directionalTrailCountValue,
            float directionalTrailLengthValue)
        {
            textureWidth = Mathf.Max(32, width);
            textureHeight = Mathf.Max(32, height);
            teamAColor = teamAColorValue;
            teamBColor = teamBColorValue;
            edgeNoiseScale = edgeNoiseScaleValue;
            edgeNoiseStrength = edgeNoiseStrengthValue;
            autoSplatterCount = autoSplatterCountValue;
            autoSplatterDistance = autoSplatterDistanceValue;
            autoSplatterRadiusRange = autoSplatterRadiusRangeValue;
            wallDripCount = wallDripCountValue;
            wallDripLength = wallDripLengthValue;
            groundSmearCount = groundSmearCountValue;
            groundSmearDistance = groundSmearDistanceValue;
            directionalTrailCount = directionalTrailCountValue;
            directionalTrailLength = directionalTrailLengthValue;

            colorTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"{surfaceName}_InkAuthoringTexture"
            };
            heightTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"{surfaceName}_InkHeightTexture"
            };

            inkTeamLookup = new InkTeam[textureWidth * textureHeight];
            inkPixels = new Color[inkTeamLookup.Length];
            heightPixels = new Color[inkTeamLookup.Length];
            Clear();
        }

        public Texture2D ColorTexture => colorTexture;
        public Texture2D HeightTexture => heightTexture;
        public int Width => textureWidth;
        public int Height => textureHeight;

        public InkTeam SampleInkAtUV(Vector2 uv)
        {
            var clampedUv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            var x = Mathf.Clamp(Mathf.RoundToInt(clampedUv.x * (textureWidth - 1)), 0, textureWidth - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(clampedUv.y * (textureHeight - 1)), 0, textureHeight - 1);
            return inkTeamLookup[(y * textureWidth) + x];
        }

        public RectInt ApplyStamp(InkSurfaceHit hit, Vector2 pixelsPerWorldUnit, bool commitTexture)
        {
            var uvPerWorldUnit = new Vector2(
                pixelsPerWorldUnit.x / textureWidth,
                pixelsPerWorldUnit.y / textureHeight);
            var splatterSeed = Mathf.RoundToInt((hit.UV.x + hit.UV.y + hit.Radius) * 10000f);
            var randomState = Random.state;
            Random.InitState(splatterSeed);

            PaintSplatCluster(hit, pixelsPerWorldUnit, uvPerWorldUnit);

            if (hit.IsWall)
            {
                PaintWallDrips(hit, pixelsPerWorldUnit, uvPerWorldUnit);
            }
            else
            {
                PaintGroundSmear(hit, pixelsPerWorldUnit, uvPerWorldUnit);
            }

            for (var i = 0; i < autoSplatterCount; i++)
            {
                var offset = Random.insideUnitCircle * autoSplatterDistance;
                var splatterUv = new Vector2(
                    Mathf.Clamp01(hit.UV.x + (offset.x * uvPerWorldUnit.x)),
                    Mathf.Clamp01(hit.UV.y + (offset.y * uvPerWorldUnit.y)));
                var splatterRadius = hit.Radius * Random.Range(autoSplatterRadiusRange.x, autoSplatterRadiusRange.y);
                PaintStamp(splatterUv, splatterRadius, hit.Team, pixelsPerWorldUnit, false, Random.insideUnitCircle.normalized);
            }

            Random.state = randomState;
            if (commitTexture)
            {
                return CommitTexture();
            }

            return GetDirtyRect();
        }

        private void PaintSplatCluster(InkSurfaceHit hit, Vector2 pixelsPerWorldUnit, Vector2 uvPerWorldUnit)
        {
            PaintStamp(hit.UV, hit.Radius * 1.18f, hit.Team, pixelsPerWorldUnit, true, hit.SurfaceDirection);
            PaintDirectionalTrail(hit, pixelsPerWorldUnit, uvPerWorldUnit);

            var lobeCount = hit.IsWall ? 2 : 3;
            for (var i = 0; i < lobeCount; i++)
            {
                var side = i - ((lobeCount - 1) * 0.5f);
                var forward = -hit.SurfaceDirection * hit.Radius * Random.Range(0.12f, 0.46f);
                var right = new Vector2(-hit.SurfaceDirection.y, hit.SurfaceDirection.x)
                    * side
                    * hit.Radius
                    * Random.Range(0.32f, 0.62f);
                var offset = forward + right;
                var lobeUv = new Vector2(
                    Mathf.Clamp01(hit.UV.x + (offset.x * uvPerWorldUnit.x)),
                    Mathf.Clamp01(hit.UV.y + (offset.y * uvPerWorldUnit.y)));
                var lobeRadius = hit.Radius * Random.Range(0.58f, 0.9f);
                PaintStamp(lobeUv, lobeRadius, hit.Team, pixelsPerWorldUnit, true, hit.SurfaceDirection);
            }
        }

        private void PaintDirectionalTrail(InkSurfaceHit hit, Vector2 pixelsPerWorldUnit, Vector2 uvPerWorldUnit)
        {
            var direction = hit.SurfaceDirection;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            var count = Mathf.Max(0, directionalTrailCount);
            for (var i = 1; i <= count; i++)
            {
                var t = i / (float)count;
                var wobble = new Vector2(-direction.y, direction.x) * Random.Range(-0.18f, 0.18f) * hit.Radius;
                var distance = -direction * (directionalTrailLength * hit.Radius * t);
                var offset = distance + wobble;
                var trailUv = new Vector2(
                    Mathf.Clamp01(hit.UV.x + (offset.x * uvPerWorldUnit.x)),
                    Mathf.Clamp01(hit.UV.y + (offset.y * uvPerWorldUnit.y)));
                var radius = hit.Radius * Mathf.Lerp(0.92f, 0.42f, t);
                PaintStamp(trailUv, radius, hit.Team, pixelsPerWorldUnit, true, direction);
            }
        }

        public RectInt CommitTexture()
        {
            var dirtyRect = GetDirtyRect();
            if (dirtyRect.width <= 0 || dirtyRect.height <= 0)
            {
                return dirtyRect;
            }

            colorTexture.SetPixels(
                dirtyRect.x,
                dirtyRect.y,
                dirtyRect.width,
                dirtyRect.height,
                CopyPixels(dirtyRect));
            heightTexture.SetPixels(
                dirtyRect.x,
                dirtyRect.y,
                dirtyRect.width,
                dirtyRect.height,
                CopyHeightPixels(dirtyRect));
            colorTexture.Apply(false, false);
            heightTexture.Apply(false, false);
            ClearDirtyRect();
            return dirtyRect;
        }

        private void Clear()
        {
            for (var i = 0; i < inkPixels.Length; i++)
            {
                inkPixels[i] = Color.clear;
                heightPixels[i] = Color.clear;
                inkTeamLookup[i] = InkTeam.Neutral;
            }

            colorTexture.SetPixels(inkPixels);
            heightTexture.SetPixels(heightPixels);
            colorTexture.Apply(false, false);
            heightTexture.Apply(false, false);
            ClearDirtyRect();
        }

        private void PaintWallDrips(InkSurfaceHit hit, Vector2 pixelsPerWorldUnit, Vector2 uvPerWorldUnit)
        {
            for (var i = 1; i <= wallDripCount; i++)
            {
                var dripT = i / (float)wallDripCount;
                var dripUv = new Vector2(
                    hit.UV.x + Random.Range(-0.08f, 0.08f) * uvPerWorldUnit.x,
                    Mathf.Clamp01(hit.UV.y - (wallDripLength * dripT * uvPerWorldUnit.y)));
                var dripRadius = hit.Radius * Mathf.Lerp(0.35f, 0.15f, dripT);
                PaintStamp(dripUv, dripRadius, hit.Team, pixelsPerWorldUnit, false, Vector2.down);
            }
        }

        private void PaintGroundSmear(InkSurfaceHit hit, Vector2 pixelsPerWorldUnit, Vector2 uvPerWorldUnit)
        {
            for (var i = 1; i <= groundSmearCount; i++)
            {
                var smearT = i / (float)groundSmearCount;
                var smearUv = new Vector2(
                    Mathf.Clamp01(hit.UV.x + (groundSmearDistance * smearT * uvPerWorldUnit.x)),
                    Mathf.Clamp01(hit.UV.y + Random.Range(-0.08f, 0.08f) * uvPerWorldUnit.y));
                var smearRadius = hit.Radius * Mathf.Lerp(0.9f, 0.45f, smearT);
                PaintStamp(smearUv, smearRadius, hit.Team, pixelsPerWorldUnit, true, hit.SurfaceDirection);
            }
        }

        private void PaintStamp(
            Vector2 uv,
            float radiusInWorldUnits,
            InkTeam team,
            Vector2 pixelsPerWorldUnit,
            bool applyEdgeNoise,
            Vector2 brushDirection)
        {
            if (brushDirection.sqrMagnitude < 0.0001f)
            {
                brushDirection = Vector2.right;
            }

            brushDirection.Normalize();
            var brushNormal = new Vector2(-brushDirection.y, brushDirection.x);
            var clampedUv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            var centerX = Mathf.RoundToInt(clampedUv.x * (textureWidth - 1));
            var centerY = Mathf.RoundToInt(clampedUv.y * (textureHeight - 1));
            var radiusInPixelsX = Mathf.Max(1, Mathf.CeilToInt(radiusInWorldUnits * Mathf.Max(1f, pixelsPerWorldUnit.x)));
            var radiusInPixelsY = Mathf.Max(1, Mathf.CeilToInt(radiusInWorldUnits * Mathf.Max(1f, pixelsPerWorldUnit.y)));
            var color = GetTeamColor(team);

            for (var y = -radiusInPixelsY; y <= radiusInPixelsY; y++)
            {
                var pixelY = centerY + y;
                if (pixelY < 0 || pixelY >= textureHeight)
                {
                    continue;
                }

                for (var x = -radiusInPixelsX; x <= radiusInPixelsX; x++)
                {
                    var pixelX = centerX + x;
                    if (pixelX < 0 || pixelX >= textureWidth)
                    {
                        continue;
                    }

                    var normalizedX = x / Mathf.Max(1f, radiusInPixelsX);
                    var normalizedY = y / Mathf.Max(1f, radiusInPixelsY);
                    var normalizedDistance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                    var allowedRadius = 1f;

                    if (applyEdgeNoise)
                    {
                        var noise = Mathf.PerlinNoise(
                            (pixelX + (uv.x * textureWidth)) * edgeNoiseScale,
                            (pixelY + (uv.y * textureHeight)) * edgeNoiseScale);
                        allowedRadius = Mathf.Lerp(1f - edgeNoiseStrength, 1f + edgeNoiseStrength, noise);
                    }

                    if (normalizedDistance > allowedRadius)
                    {
                        continue;
                    }

                    var remappedDistance = Mathf.Clamp01(normalizedDistance / Mathf.Max(0.01f, allowedRadius));
                    var blend = Mathf.SmoothStep(0f, 1f, 1f - remappedDistance);
                    var alongBrush = (normalizedX * brushDirection.x) + (normalizedY * brushDirection.y);
                    var acrossBrush = (normalizedX * brushNormal.x) + (normalizedY * brushNormal.y);
                    var bristle = Mathf.PerlinNoise(
                        (acrossBrush * 9.5f) + (uv.x * 17f),
                        (alongBrush * 2.2f) + (uv.y * 11f));
                    var bristleRidge = Mathf.SmoothStep(0.48f, 0.92f, bristle) * blend;
                    var pigmentVariation = Mathf.Lerp(0.92f, 1.08f, bristle);

                    var index = (pixelY * textureWidth) + pixelX;
                    var existingPixel = inkPixels[index];
                    var pigmentColor = new Color(
                        Mathf.Clamp01(color.r * pigmentVariation),
                        Mathf.Clamp01(color.g * pigmentVariation),
                        Mathf.Clamp01(color.b * pigmentVariation),
                        1f);
                    var mixedColor = existingPixel.a > 0.01f ? Color.Lerp(existingPixel, pigmentColor, blend) : pigmentColor;
                    inkPixels[index] = new Color(
                        mixedColor.r,
                        mixedColor.g,
                        mixedColor.b,
                        Mathf.Clamp01(existingPixel.a + (blend * (1f - existingPixel.a) * 0.82f)));

                    var softBody = Mathf.SmoothStep(0f, 1f, 1f - remappedDistance) * 0.64f;
                    var raisedEdge = Mathf.SmoothStep(0.62f, 0.88f, remappedDistance)
                        * (1f - Mathf.SmoothStep(0.88f, 1f, remappedDistance))
                        * blend
                        * 0.58f;
                    var brushedImpasto = bristleRidge * 0.28f * (1f - Mathf.SmoothStep(0.82f, 1f, remappedDistance));
                    var pooledHeight = Mathf.Clamp01(softBody + raisedEdge + brushedImpasto);
                    var existingHeight = heightPixels[index].r;
                    var height = Mathf.Clamp01(Mathf.Max(existingHeight, pooledHeight));
                    heightPixels[index] = new Color(height, height, height, Mathf.Max(heightPixels[index].a, blend));

                    if (blend >= 0.18f)
                    {
                        inkTeamLookup[index] = team;
                    }

                    IncludeDirtyPixel(pixelX, pixelY);
                }
            }
        }

        private void IncludeDirtyPixel(int x, int y)
        {
            if (!hasDirtyPixels)
            {
                hasDirtyPixels = true;
                dirtyMinX = x;
                dirtyMinY = y;
                dirtyMaxX = x;
                dirtyMaxY = y;
                return;
            }

            dirtyMinX = Mathf.Min(dirtyMinX, x);
            dirtyMinY = Mathf.Min(dirtyMinY, y);
            dirtyMaxX = Mathf.Max(dirtyMaxX, x);
            dirtyMaxY = Mathf.Max(dirtyMaxY, y);
        }

        private RectInt GetDirtyRect()
        {
            if (!hasDirtyPixels)
            {
                return new RectInt(0, 0, 0, 0);
            }

            return new RectInt(
                dirtyMinX,
                dirtyMinY,
                (dirtyMaxX - dirtyMinX) + 1,
                (dirtyMaxY - dirtyMinY) + 1);
        }

        private Color[] CopyPixels(RectInt rect)
        {
            var pixels = new Color[rect.width * rect.height];
            for (var y = 0; y < rect.height; y++)
            {
                var sourceIndex = ((rect.y + y) * textureWidth) + rect.x;
                var targetIndex = y * rect.width;
                System.Array.Copy(inkPixels, sourceIndex, pixels, targetIndex, rect.width);
            }

            return pixels;
        }

        private Color[] CopyHeightPixels(RectInt rect)
        {
            var pixels = new Color[rect.width * rect.height];
            for (var y = 0; y < rect.height; y++)
            {
                var sourceIndex = ((rect.y + y) * textureWidth) + rect.x;
                var targetIndex = y * rect.width;
                System.Array.Copy(heightPixels, sourceIndex, pixels, targetIndex, rect.width);
            }

            return pixels;
        }

        private void ClearDirtyRect()
        {
            hasDirtyPixels = false;
            dirtyMinX = 0;
            dirtyMinY = 0;
            dirtyMaxX = 0;
            dirtyMaxY = 0;
        }

        private Color GetTeamColor(InkTeam team)
        {
            return team switch
            {
                InkTeam.TeamA => teamAColor,
                InkTeam.TeamB => teamBColor,
                _ => Color.clear,
            };
        }
    }
}
