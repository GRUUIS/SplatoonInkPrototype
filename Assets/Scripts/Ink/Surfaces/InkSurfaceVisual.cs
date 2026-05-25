using SplatoonInkPrototype.Ink.Gameplay;
using UnityEngine;

namespace SplatoonInkPrototype.Ink.Surfaces
{
    /// <summary>
    /// Presents the ink map through a regular Unity material so the first painting path is pipeline-agnostic.
    /// </summary>
    public class InkSurfaceVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private InkGameplaySurface gameplaySurface;

        [Header("Base Surface")]
        [SerializeField] private Color baseColor = new(0.84f, 0.86f, 0.9f, 1f);
        [SerializeField] private Color baseShadowTint = new(0.66f, 0.7f, 0.76f, 1f);
        [SerializeField] private float tileScale = 6f;
        [SerializeField] private float groutStrength = 0.22f;

        [Header("Ink Display")]
        [SerializeField] private bool useRenderTexturePresentation = true;
        [SerializeField] private float inkBoost = 1.08f;
        [SerializeField] private int inkSoftenRadius = 5;
        [SerializeField] private float metaballBlurRadius = 3.5f;
        [SerializeField] private float metaballThreshold = 0.2f;
        [SerializeField] private float metaballFeather = 0.32f;
        [SerializeField] private float edgeDarkening = 0.08f;
        [SerializeField] private float wetHighlightStrength = 0.26f;
        [SerializeField] private float highlightStreakScale = 28f;
        [SerializeField] private float normalStrength = 3.5f;
        [SerializeField] private float inkSmoothness = 0.86f;
        [SerializeField] private float drySurfaceSmoothness = 0.28f;
        [SerializeField] private bool keepDisplayMaterialBound = true;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int BaseShadowTintId = Shader.PropertyToID("_BaseShadowTint");
        private static readonly int TileScaleId = Shader.PropertyToID("_TileScale");
        private static readonly int GroutStrengthId = Shader.PropertyToID("_GroutStrength");
        private static readonly int InkBoostId = Shader.PropertyToID("_InkBoost");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int EdgeDarkeningId = Shader.PropertyToID("_EdgeDarkening");
        private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
        private static readonly int HighlightScaleId = Shader.PropertyToID("_HighlightScale");

        private Material runtimeMaterial;
        private Material blurMaterial;
        private Material compositeMaterial;
        private Texture2D displayTexture;
        private Texture2D normalTexture;
        private RenderTexture blurTextureA;
        private RenderTexture blurTextureB;
        private RenderTexture compositeTexture;
        private Color[] displayPixels;
        private Color[] normalPixels;
        private Color[] basePixels;
        private int displayWidth;
        private int displayHeight;
        private bool isDisplayDirty;
        private RectInt pendingDirtyRect;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            gameplaySurface = GetComponent<InkGameplaySurface>();
        }

        private void Awake()
        {
            EnsureVisualReady();
        }

        private void OnEnable()
        {
            EnsureVisualReady();
            SubscribeToGameplaySurface();
            RefreshInkPresentation();
        }

        private void OnDisable()
        {
            UnsubscribeFromGameplaySurface();
        }

        private void OnDestroy()
        {
            if (displayTexture != null)
            {
                DestroyImmediate(displayTexture);
            }

            if (normalTexture != null)
            {
                DestroyImmediate(normalTexture);
            }

            ReleaseRenderTextures();
        }

        private void LateUpdate()
        {
            if (isDisplayDirty)
            {
                RefreshInkPresentation(pendingDirtyRect);
                isDisplayDirty = false;
                pendingDirtyRect = default;
            }

            if (!keepDisplayMaterialBound)
            {
                return;
            }

            EnsureVisualReady();
            ApplyMaterialProperties();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureVisualReady();
            RebuildBasePixels();
            RefreshInkPresentation();
        }

        public void Configure(Renderer rendererReference, InkGameplaySurface surfaceReference, Color surfaceBaseColor, Color shadowTint)
        {
            UnsubscribeFromGameplaySurface();
            targetRenderer = rendererReference;
            gameplaySurface = surfaceReference;
            baseColor = surfaceBaseColor;
            baseShadowTint = shadowTint;
            EnsureVisualReady();
            SubscribeToGameplaySurface();
            RefreshInkPresentation();
        }

        public void SetBaseColors(Color surfaceBaseColor, Color shadowTint)
        {
            baseColor = surfaceBaseColor;
            baseShadowTint = shadowTint;
            RebuildBasePixels();
            RefreshInkPresentation();
        }

        private void EnsureVisualReady()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (gameplaySurface == null)
            {
                gameplaySurface = GetComponent<InkGameplaySurface>();
            }

            if (targetRenderer == null || gameplaySurface == null)
            {
                return;
            }

            gameplaySurface.EnsureInitialized();
            EnsureDisplayTexture(gameplaySurface.RuntimeInkMask);
            EnsureRenderTextureMaterials();
            EnsureMaterialAssigned();
            ApplyMaterialProperties();
        }

        private void EnsureMaterialAssigned()
        {
            if (targetRenderer == null)
            {
                return;
            }

            var currentMaterial = targetRenderer.sharedMaterial;
            var needsReplacement = runtimeMaterial == null
                || currentMaterial == null
                || currentMaterial != runtimeMaterial;

            if (!needsReplacement)
            {
                return;
            }

            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            runtimeMaterial = new Material(shader)
            {
                name = $"{targetRenderer.name}_InkRuntimeMaterial"
            };

            targetRenderer.sharedMaterial = runtimeMaterial;
        }

        private void EnsureDisplayTexture(Texture sourceTexture)
        {
            var width = sourceTexture != null ? sourceTexture.width : 256;
            var height = sourceTexture != null ? sourceTexture.height : 256;

            if (displayTexture != null && displayWidth == width && displayHeight == height)
            {
                return;
            }

            if (displayTexture != null)
            {
                DestroyImmediate(displayTexture);
            }

            displayWidth = width;
            displayHeight = height;
            displayPixels = new Color[displayWidth * displayHeight];
            normalPixels = new Color[displayPixels.Length];
            basePixels = new Color[displayPixels.Length];
            displayTexture = new Texture2D(displayWidth, displayHeight, TextureFormat.RGBA32, false, false)
            {
                name = $"{name}_InkDisplayTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            normalTexture = new Texture2D(displayWidth, displayHeight, TextureFormat.RGBA32, false, true)
            {
                name = $"{name}_InkNormalTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            EnsureRenderTextures();
            RebuildBasePixels();
            ResetNormalPixels();
        }

        private void EnsureRenderTextureMaterials()
        {
            if (blurMaterial == null)
            {
                var blurShader = Shader.Find("Hidden/SplatoonInkPrototype/InkMetaballBlur");
                if (blurShader != null)
                {
                    blurMaterial = new Material(blurShader) { name = $"{name}_InkBlurMaterial" };
                }
            }

            if (compositeMaterial == null)
            {
                var compositeShader = Shader.Find("Hidden/SplatoonInkPrototype/InkMetaballComposite");
                if (compositeShader != null)
                {
                    compositeMaterial = new Material(compositeShader) { name = $"{name}_InkCompositeMaterial" };
                }
            }
        }

        private void EnsureRenderTextures()
        {
            if (!useRenderTexturePresentation || displayWidth <= 0 || displayHeight <= 0)
            {
                return;
            }

            if (compositeTexture != null
                && compositeTexture.width == displayWidth
                && compositeTexture.height == displayHeight)
            {
                return;
            }

            ReleaseRenderTextures();
            blurTextureA = CreateRenderTexture("InkBlurA");
            blurTextureB = CreateRenderTexture("InkBlurB");
            compositeTexture = CreateRenderTexture("InkComposite");
        }

        private RenderTexture CreateRenderTexture(string suffix)
        {
            var renderTexture = new RenderTexture(displayWidth, displayHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_{suffix}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
            return renderTexture;
        }

        private void ReleaseRenderTextures()
        {
            ReleaseRenderTexture(ref blurTextureA);
            ReleaseRenderTexture(ref blurTextureB);
            ReleaseRenderTexture(ref compositeTexture);
        }

        private static void ReleaseRenderTexture(ref RenderTexture renderTexture)
        {
            if (renderTexture == null)
            {
                return;
            }

            renderTexture.Release();
            DestroyImmediate(renderTexture);
            renderTexture = null;
        }

        private void RefreshInkPresentation()
        {
            RefreshInkPresentation(new RectInt(0, 0, displayWidth, displayHeight));
        }

        private void RefreshInkPresentation(RectInt dirtyRect)
        {
            if (gameplaySurface == null)
            {
                return;
            }

            var sourceTexture = gameplaySurface.RuntimeInkMask;
            var heightTexture = gameplaySurface.RuntimeInkHeight;
            EnsureDisplayTexture(sourceTexture);
            EnsureRenderTextureMaterials();

            if (useRenderTexturePresentation
                && sourceTexture != null
                && blurMaterial != null
                && compositeMaterial != null)
            {
                RefreshRenderTexturePresentation(sourceTexture);
                return;
            }

            if (displayTexture == null || displayPixels == null || basePixels == null)
            {
                return;
            }

            if (dirtyRect.width <= 0 || dirtyRect.height <= 0)
            {
                return;
            }

            var updateRect = ExpandRect(dirtyRect, Mathf.Max(0, inkSoftenRadius + 1));
            var inkPixels = sourceTexture != null
                ? sourceTexture.GetPixels(updateRect.x, updateRect.y, updateRect.width, updateRect.height)
                : null;
            var heightPixels = heightTexture != null
                ? heightTexture.GetPixels(updateRect.x, updateRect.y, updateRect.width, updateRect.height)
                : null;
            var changedPixels = new Color[updateRect.width * updateRect.height];
            var changedNormals = new Color[updateRect.width * updateRect.height];

            for (var y = 0; y < updateRect.height; y++)
            {
                for (var x = 0; x < updateRect.width; x++)
                {
                    var pixelX = updateRect.x + x;
                    var pixelY = updateRect.y + y;
                    var index = (pixelY * displayWidth) + pixelX;
                    var changedIndex = (y * updateRect.width) + x;

                    var surfaceColor = basePixels[index];
                    if (inkPixels != null)
                    {
                        var ink = SampleMetaballInk(inkPixels, updateRect, pixelX, pixelY, out var coverage, out var edge);
                        var inkColor = new Color(
                            Mathf.Clamp01(ink.r * inkBoost),
                            Mathf.Clamp01(ink.g * inkBoost),
                            Mathf.Clamp01(ink.b * inkBoost),
                            1f);
                        inkColor = Color.Lerp(inkColor, inkColor * (1f - edgeDarkening), edge);
                        inkColor = ApplyWetHighlight(inkColor, pixelX, pixelY, coverage, edge);
                        surfaceColor = Color.Lerp(surfaceColor, inkColor, coverage);
                    }

                    displayPixels[index] = surfaceColor;
                    changedPixels[changedIndex] = surfaceColor;
                    var normal = heightPixels != null
                        ? BuildNormalPixel(heightPixels, updateRect, pixelX, pixelY)
                        : new Color(0.5f, 0.5f, 1f, 0.5f);
                    normalPixels[index] = normal;
                    changedNormals[changedIndex] = normal;
                }
            }

            displayTexture.SetPixels(updateRect.x, updateRect.y, updateRect.width, updateRect.height, changedPixels);
            normalTexture.SetPixels(updateRect.x, updateRect.y, updateRect.width, updateRect.height, changedNormals);
            displayTexture.Apply(false, false);
            normalTexture.Apply(false, false);
            ApplyMaterialProperties();
        }

        private void RefreshRenderTexturePresentation(Texture sourceTexture)
        {
            EnsureRenderTextures();
            if (blurTextureA == null || blurTextureB == null || compositeTexture == null)
            {
                return;
            }

            blurMaterial.SetFloat(BlurRadiusId, metaballBlurRadius);
            blurMaterial.SetVector(BlurDirectionId, new Vector4(1f, 0f, 0f, 0f));
            Graphics.Blit(sourceTexture, blurTextureA, blurMaterial);
            blurMaterial.SetVector(BlurDirectionId, new Vector4(0f, 1f, 0f, 0f));
            Graphics.Blit(blurTextureA, blurTextureB, blurMaterial);

            compositeMaterial.SetColor(BaseColorId, baseColor);
            compositeMaterial.SetColor(BaseShadowTintId, baseShadowTint);
            compositeMaterial.SetFloat(TileScaleId, tileScale);
            compositeMaterial.SetFloat(GroutStrengthId, groutStrength);
            compositeMaterial.SetFloat(InkBoostId, inkBoost);
            compositeMaterial.SetFloat(ThresholdId, metaballThreshold);
            compositeMaterial.SetFloat(FeatherId, metaballFeather);
            compositeMaterial.SetFloat(EdgeDarkeningId, edgeDarkening);
            compositeMaterial.SetFloat(HighlightStrengthId, wetHighlightStrength);
            compositeMaterial.SetFloat(HighlightScaleId, highlightStreakScale);
            Graphics.Blit(blurTextureB, compositeTexture, compositeMaterial);
            ApplyMaterialProperties();
        }

        private void RebuildBasePixels()
        {
            if (basePixels == null)
            {
                return;
            }

            for (var y = 0; y < displayHeight; y++)
            {
                for (var x = 0; x < displayWidth; x++)
                {
                    var uv = new Vector2(
                        x / Mathf.Max(1f, displayWidth - 1f),
                        y / Mathf.Max(1f, displayHeight - 1f));
                    var index = (y * displayWidth) + x;
                    basePixels[index] = EvaluateBaseSurface(uv);
                    displayPixels[index] = basePixels[index];
                }
            }
        }

        private void ResetNormalPixels()
        {
            if (normalPixels == null || normalTexture == null)
            {
                return;
            }

            for (var i = 0; i < normalPixels.Length; i++)
            {
                normalPixels[i] = new Color(0.5f, 0.5f, 1f, 0.5f);
            }

            normalTexture.SetPixels(normalPixels);
            normalTexture.Apply(false, false);
        }

        private Color SampleMetaballInk(Color[] inkPixels, RectInt sourceRect, int centerX, int centerY, out float coverage, out float edge)
        {
            var weightedColor = Color.clear;
            var totalWeight = 0f;
            var field = 0f;
            var radius = Mathf.Max(0, inkSoftenRadius);

            for (var y = -radius; y <= radius; y++)
            {
                var sampleY = Mathf.Clamp(centerY + y, sourceRect.y, sourceRect.yMax - 1);
                for (var x = -radius; x <= radius; x++)
                {
                    var sampleX = Mathf.Clamp(centerX + x, sourceRect.x, sourceRect.xMax - 1);
                    var distance = Mathf.Sqrt((x * x) + (y * y));
                    var normalizedDistance = distance / Mathf.Max(1f, radius + 1f);
                    var weight = Mathf.Exp(-normalizedDistance * normalizedDistance * 3.2f);
                    var sample = inkPixels[((sampleY - sourceRect.y) * sourceRect.width) + (sampleX - sourceRect.x)];

                    weightedColor += sample * (sample.a * weight);
                    totalWeight += sample.a * weight;
                    field += sample.a * weight;
                }
            }

            if (totalWeight > 0.0001f)
            {
                weightedColor /= totalWeight;
            }

            var normalizedField = Mathf.Clamp01(field / Mathf.Max(1f, (radius + 1) * (radius + 1) * 0.72f));
            coverage = Mathf.SmoothStep(metaballThreshold, metaballThreshold + Mathf.Max(0.001f, metaballFeather), normalizedField);
            var innerCoverage = Mathf.SmoothStep(
                metaballThreshold + (metaballFeather * 0.45f),
                metaballThreshold + metaballFeather,
                normalizedField);
            edge = Mathf.Clamp01(coverage - innerCoverage);
            weightedColor.a = 1f;
            return weightedColor;
        }

        private RectInt ExpandRect(RectInt rect, int padding)
        {
            var xMin = Mathf.Clamp(rect.xMin - padding, 0, displayWidth - 1);
            var yMin = Mathf.Clamp(rect.yMin - padding, 0, displayHeight - 1);
            var xMax = Mathf.Clamp(rect.xMax + padding, 0, displayWidth);
            var yMax = Mathf.Clamp(rect.yMax + padding, 0, displayHeight);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        private Color EvaluateBaseSurface(Vector2 uv)
        {
            var grid = new Vector2(
                Mathf.Abs(Mathf.Repeat(uv.x * tileScale, 1f) - 0.5f),
                Mathf.Abs(Mathf.Repeat(uv.y * tileScale, 1f) - 0.5f));
            var grout = 1f - Mathf.SmoothStep(0.44f, 0.5f, Mathf.Min(grid.x, grid.y) * 2f);
            var surfaceColor = Color.Lerp(baseShadowTint, baseColor, 0.78f);
            surfaceColor = Color.Lerp(surfaceColor, baseShadowTint, grout * groutStrength);
            surfaceColor.a = 1f;
            return surfaceColor;
        }

        private Color ApplyWetHighlight(Color inkColor, int pixelX, int pixelY, float coverage, float edge)
        {
            var u = pixelX / Mathf.Max(1f, displayWidth - 1f);
            var v = pixelY / Mathf.Max(1f, displayHeight - 1f);
            var streak = Mathf.PerlinNoise((u * highlightStreakScale * 0.22f) + (v * 1.4f), v * 0.9f);
            streak = Mathf.SmoothStep(0.86f, 0.99f, streak);
            var satinSheen = Mathf.SmoothStep(0.35f, 0.95f, coverage) * (1f - edge * 0.65f);
            var highlight = Mathf.Clamp01((streak * 0.28f + satinSheen * 0.12f) * wetHighlightStrength * coverage);
            return Color.Lerp(inkColor, Color.white, highlight);
        }

        private Color BuildNormalPixel(Color[] heightPixels, RectInt sourceRect, int pixelX, int pixelY)
        {
            var left = SampleHeight(heightPixels, sourceRect, pixelX - 1, pixelY);
            var right = SampleHeight(heightPixels, sourceRect, pixelX + 1, pixelY);
            var down = SampleHeight(heightPixels, sourceRect, pixelX, pixelY - 1);
            var up = SampleHeight(heightPixels, sourceRect, pixelX, pixelY + 1);
            var dx = (left - right) * normalStrength;
            var dy = (down - up) * normalStrength;
            var normal = new Vector3(dx, dy, 1f).normalized;
            return new Color(
                normal.x * 0.5f + 0.5f,
                normal.y * 0.5f + 0.5f,
                normal.z * 0.5f + 0.5f,
                normal.x * 0.5f + 0.5f);
        }

        private static float SampleHeight(Color[] heightPixels, RectInt sourceRect, int pixelX, int pixelY)
        {
            var x = Mathf.Clamp(pixelX, sourceRect.x, sourceRect.xMax - 1);
            var y = Mathf.Clamp(pixelY, sourceRect.y, sourceRect.yMax - 1);
            return heightPixels[((y - sourceRect.y) * sourceRect.width) + (x - sourceRect.x)].r;
        }

        private void ApplyMaterialProperties()
        {
            var mainTexture = useRenderTexturePresentation && compositeTexture != null
                ? (Texture)compositeTexture
                : displayTexture;

            if (runtimeMaterial == null || mainTexture == null)
            {
                return;
            }

            if (runtimeMaterial.HasProperty(BaseMapId))
            {
                runtimeMaterial.SetTexture(BaseMapId, mainTexture);
            }

            if (runtimeMaterial.HasProperty(MainTexId))
            {
                runtimeMaterial.SetTexture(MainTexId, mainTexture);
            }

            if (runtimeMaterial.HasProperty(BumpMapId) && normalTexture != null)
            {
                runtimeMaterial.EnableKeyword("_NORMALMAP");
                runtimeMaterial.SetTexture(BumpMapId, normalTexture);
            }

            if (runtimeMaterial.HasProperty(BumpScaleId))
            {
                runtimeMaterial.SetFloat(BumpScaleId, 1f);
            }

            if (runtimeMaterial.HasProperty(BaseColorId))
            {
                runtimeMaterial.SetColor(BaseColorId, Color.white);
            }

            if (runtimeMaterial.HasProperty(ColorId))
            {
                runtimeMaterial.SetColor(ColorId, Color.white);
            }

            if (runtimeMaterial.HasProperty(SmoothnessId))
            {
                runtimeMaterial.SetFloat(SmoothnessId, Mathf.Lerp(drySurfaceSmoothness, inkSmoothness, 0.72f));
            }

            if (runtimeMaterial.HasProperty(GlossinessId))
            {
                runtimeMaterial.SetFloat(GlossinessId, Mathf.Lerp(drySurfaceSmoothness, inkSmoothness, 0.72f));
            }
        }

        private void SubscribeToGameplaySurface()
        {
            if (gameplaySurface == null)
            {
                return;
            }

            gameplaySurface.InkTextureChanged -= HandleInkTextureChanged;
            gameplaySurface.InkTextureChanged += HandleInkTextureChanged;
        }

        private void UnsubscribeFromGameplaySurface()
        {
            if (gameplaySurface == null)
            {
                return;
            }

            gameplaySurface.InkTextureChanged -= HandleInkTextureChanged;
        }

        private void HandleInkTextureChanged(RectInt dirtyRect)
        {
            pendingDirtyRect = isDisplayDirty ? CombineRects(pendingDirtyRect, dirtyRect) : dirtyRect;
            isDisplayDirty = true;
        }

        private static RectInt CombineRects(RectInt a, RectInt b)
        {
            if (a.width <= 0 || a.height <= 0)
            {
                return b;
            }

            if (b.width <= 0 || b.height <= 0)
            {
                return a;
            }

            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
