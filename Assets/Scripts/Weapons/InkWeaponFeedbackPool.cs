using System.Collections.Generic;
using SplatoonInkPrototype.Ink.Gameplay;
using UnityEngine;

namespace SplatoonInkPrototype.Weapons
{
    /// <summary>
    /// Pools simple tracer and impact objects so weapon feedback stays lightweight.
    /// </summary>
    public class InkWeaponFeedbackPool : MonoBehaviour
    {
        private sealed class TimedInstance
        {
            public GameObject GameObject;
            public float ExpireTime;
            public Renderer Renderer;
            public LineRenderer LineRenderer;
        }

        [SerializeField] private int tracerPoolSize = 8;
        [SerializeField] private int impactPoolSize = 12;

        private readonly List<TimedInstance> tracers = new();
        private readonly List<TimedInstance> impacts = new();
        private Material feedbackMaterial;

        private void Awake()
        {
            EnsurePoolInitialized();
        }

        private void Update()
        {
            UpdatePool(tracers);
            UpdatePool(impacts);
        }

        public void PlayTracer(Vector3 origin, Vector3 target, InkTeam team, float width, float duration)
        {
            EnsurePoolInitialized();
            var tracer = GetAvailableInstance(tracers);
            tracer.GameObject.SetActive(true);
            tracer.ExpireTime = Time.time + duration;

            tracer.LineRenderer.widthMultiplier = width;
            tracer.LineRenderer.positionCount = 2;
            tracer.LineRenderer.SetPosition(0, origin);
            tracer.LineRenderer.SetPosition(1, target);
            tracer.LineRenderer.startColor = GetTeamColor(team, 0.95f);
            tracer.LineRenderer.endColor = GetTeamColor(team, 0.05f);
        }

        public void PlayImpact(Vector3 position, Vector3 normal, InkTeam team, float scale, bool isInkHit)
        {
            EnsurePoolInitialized();
            var impact = GetAvailableInstance(impacts);
            impact.GameObject.SetActive(true);
            impact.ExpireTime = Time.time + (isInkHit ? 0.18f : 0.12f);
            impact.GameObject.transform.position = position + (normal * 0.03f);
            impact.GameObject.transform.localScale = Vector3.one * scale;
            impact.GameObject.transform.rotation = Quaternion.LookRotation(normal);
            impact.Renderer.sharedMaterial.color = GetTeamColor(team, isInkHit ? 0.85f : 0.45f);
        }

        private void EnsurePoolInitialized()
        {
            if (feedbackMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                feedbackMaterial = new Material(shader)
                {
                    name = "PrototypeShotFeedbackMaterial"
                };
            }

            while (tracers.Count < tracerPoolSize)
            {
                tracers.Add(CreateTracerInstance(tracers.Count));
            }

            while (impacts.Count < impactPoolSize)
            {
                impacts.Add(CreateImpactInstance(impacts.Count));
            }
        }

        private TimedInstance CreateTracerInstance(int index)
        {
            var tracerObject = new GameObject($"Tracer_{index}");
            tracerObject.transform.SetParent(transform, false);
            tracerObject.SetActive(false);

            var lineRenderer = tracerObject.AddComponent<LineRenderer>();
            lineRenderer.material = feedbackMaterial;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.numCapVertices = 4;
            lineRenderer.alignment = LineAlignment.View;

            return new TimedInstance
            {
                GameObject = tracerObject,
                LineRenderer = lineRenderer,
            };
        }

        private TimedInstance CreateImpactInstance(int index)
        {
            var impactObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impactObject.name = $"Impact_{index}";
            impactObject.transform.SetParent(transform, false);
            impactObject.SetActive(false);

            var collider = impactObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = impactObject.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(feedbackMaterial);

            return new TimedInstance
            {
                GameObject = impactObject,
                Renderer = renderer,
            };
        }

        private TimedInstance GetAvailableInstance(List<TimedInstance> pool)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (!pool[i].GameObject.activeSelf)
                {
                    return pool[i];
                }
            }

            return pool[0];
        }

        private void UpdatePool(List<TimedInstance> pool)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var instance = pool[i];
                if (!instance.GameObject.activeSelf)
                {
                    continue;
                }

                if (Time.time >= instance.ExpireTime)
                {
                    instance.GameObject.SetActive(false);
                }
            }
        }

        private static Color GetTeamColor(InkTeam team, float alpha)
        {
            var color = team switch
            {
                InkTeam.TeamA => new Color(0.2f, 0.8f, 1f, alpha),
                InkTeam.TeamB => new Color(1f, 0.35f, 0.8f, alpha),
                _ => new Color(1f, 1f, 1f, alpha),
            };

            return color;
        }
    }
}
