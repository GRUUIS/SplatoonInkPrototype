using System.Collections.Generic;
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
        [SerializeField] private string[] colorTexturePropertyNames = { "_BaseMap", "_MainTex" };

        [Header("Team Colors")]
        [SerializeField] private Color neutralColor = new(0.82f, 0.82f, 0.82f, 1f);
        [SerializeField] private Color teamAColor = new(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private Color teamBColor = new(1f, 0.35f, 0.8f, 1f);

        [Header("Ink Shape")]
        [SerializeField] private float edgeNoiseScale = 0.12f;
        [SerializeField] private float edgeNoiseStrength = 0.35f;
        [SerializeField] private int autoSplatterCount = 6;
        [SerializeField] private float autoSplatterDistance = 0.35f;
        [SerializeField] private Vector2 autoSplatterRadiusRange = new(0.12f, 0.28f);

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private List<InkStamp> stamps = new();

        public IReadOnlyList<InkStamp> Stamps => stamps;

        private Texture2D runtimeInkTexture;
        private InkTeam[] inkTeamLookup;
        private Color[] inkPixels;
        private Material runtimeMaterial;
        private bool isInitialized;

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

            textureResolution = Mathf.Max(32, textureResolution);
            runtimeInkTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"{name}_InkRuntimeTexture"
            };

            inkTeamLookup = new InkTeam[textureResolution * textureResolution];
            inkPixels = new Color[inkTeamLookup.Length];

            for (var i = 0; i < inkPixels.Length; i++)
            {
                inkPixels[i] = neutralColor;
                inkTeamLookup[i] = InkTeam.Neutral;
            }

            runtimeInkTexture.SetPixels(inkPixels);
            runtimeInkTexture.Apply(false, false);

            if (Application.isPlaying)
            {
                runtimeMaterial = targetRenderer.material;
            }
            else
            {
                var sourceMaterial = targetRenderer.sharedMaterial;
                runtimeMaterial = sourceMaterial != null ? new Material(sourceMaterial) : targetRenderer.material;
                runtimeMaterial.name = $"{targetRenderer.name}_InkRuntimeMaterial";
                targetRenderer.sharedMaterial = runtimeMaterial;
            }

            for (var i = 0; i < colorTexturePropertyNames.Length; i++)
            {
                var propertyName = colorTexturePropertyNames[i];
                if (runtimeMaterial.HasProperty(propertyName))
                {
                    runtimeMaterial.SetTexture(propertyName, runtimeInkTexture);
                }
            }

            isInitialized = true;
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
            PaintInkPattern(hit.UV, hit.Radius, hit.Team);
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

            var clampedUv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            var x = Mathf.Clamp(Mathf.RoundToInt(clampedUv.x * (textureResolution - 1)), 0, textureResolution - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(clampedUv.y * (textureResolution - 1)), 0, textureResolution - 1);
            team = inkTeamLookup[(y * textureResolution) + x];
            return true;
        }

        private void PaintInkPattern(Vector2 uv, float radiusInWorldUnits, InkTeam team)
        {
            PaintStamp(uv, radiusInWorldUnits, team, true);

            // Add a few smaller off-center droplets so one shot feels less like a perfect decal.
            var splatterSeed = Mathf.RoundToInt((uv.x + uv.y + radiusInWorldUnits) * 10000f);
            var randomState = Random.state;
            Random.InitState(splatterSeed);

            for (var i = 0; i < autoSplatterCount; i++)
            {
                var offset = Random.insideUnitCircle * autoSplatterDistance;
                var splatterUv = new Vector2(
                    Mathf.Clamp01(uv.x + offset.x),
                    Mathf.Clamp01(uv.y + offset.y));
                var splatterRadius = radiusInWorldUnits * Random.Range(autoSplatterRadiusRange.x, autoSplatterRadiusRange.y);
                PaintStamp(splatterUv, splatterRadius, team, false);
            }

            Random.state = randomState;
        }

        private void PaintStamp(Vector2 uv, float radiusInWorldUnits, InkTeam team, bool applyEdgeNoise)
        {
            if (!isInitialized)
            {
                return;
            }

            var clampedUv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            var centerX = Mathf.RoundToInt(clampedUv.x * (textureResolution - 1));
            var centerY = Mathf.RoundToInt(clampedUv.y * (textureResolution - 1));
            var radiusInPixels = Mathf.Max(1, Mathf.CeilToInt(radiusInWorldUnits * GetPixelsPerWorldUnit()));
            var color = GetTeamColor(team);

            for (var y = -radiusInPixels; y <= radiusInPixels; y++)
            {
                var pixelY = centerY + y;
                if (pixelY < 0 || pixelY >= textureResolution)
                {
                    continue;
                }

                for (var x = -radiusInPixels; x <= radiusInPixels; x++)
                {
                    var pixelX = centerX + x;
                    if (pixelX < 0 || pixelX >= textureResolution)
                    {
                        continue;
                    }

                    var distance = Mathf.Sqrt((x * x) + (y * y));
                    var normalizedDistance = distance / radiusInPixels;
                    var allowedRadius = 1f;

                    if (applyEdgeNoise)
                    {
                        var noise = Mathf.PerlinNoise(
                            (pixelX + (uv.x * textureResolution)) * edgeNoiseScale,
                            (pixelY + (uv.y * textureResolution)) * edgeNoiseScale);
                        allowedRadius = Mathf.Lerp(1f - edgeNoiseStrength, 1f + edgeNoiseStrength, noise);
                    }

                    if (normalizedDistance > allowedRadius)
                    {
                        continue;
                    }

                    var remappedDistance = Mathf.Clamp01(normalizedDistance / Mathf.Max(0.01f, allowedRadius));
                    var blend = 1f - remappedDistance;
                    blend *= blend;

                    var index = (pixelY * textureResolution) + pixelX;
                    inkPixels[index] = Color.Lerp(inkPixels[index], color, blend);

                    if (blend >= 0.2f)
                    {
                        inkTeamLookup[index] = team;
                    }
                }
            }

            runtimeInkTexture.SetPixels(inkPixels);
            runtimeInkTexture.Apply(false, false);
        }

        private float GetPixelsPerWorldUnit()
        {
            if (targetRenderer == null)
            {
                return textureResolution;
            }

            var bounds = targetRenderer.bounds.size;
            var surfaceExtent = Mathf.Max(bounds.x, bounds.y, bounds.z);
            surfaceExtent = Mathf.Max(surfaceExtent, 0.01f);
            return textureResolution / surfaceExtent;
        }

        private Color GetTeamColor(InkTeam team)
        {
            return team switch
            {
                InkTeam.TeamA => teamAColor,
                InkTeam.TeamB => teamBColor,
                _ => neutralColor,
            };
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
}
