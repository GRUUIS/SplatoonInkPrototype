using SplatoonInkPrototype.Ink.Gameplay;
using SplatoonInkPrototype.Ink.Surfaces;
using SplatoonInkPrototype.Player;
using SplatoonInkPrototype.UI;
using SplatoonInkPrototype.Weapons;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SplatoonInkPrototype.Core
{
    /// <summary>
    /// Creates a minimal playable prototype arena in the sample scene.
    /// </summary>
    [ExecuteAlways]
    public class PrototypeSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private bool autoBuildScene = true;
        [SerializeField] private Vector3 floorPosition = new(0f, -0.5f, 0f);
        [SerializeField] private Vector3 floorSize = new(14f, 1f, 14f);
        [SerializeField] private Vector3 wallPosition = new(0f, 2f, 6f);
        [SerializeField] private Vector3 wallSize = new(14f, 4f, 1f);
        [SerializeField] private Color floorBaseColor = new(0.58f, 0.66f, 0.78f, 1f);
        [SerializeField] private Color floorShadowColor = new(0.33f, 0.39f, 0.5f, 1f);
        [SerializeField] private Color wallBaseColor = new(0.82f, 0.84f, 0.88f, 1f);
        [SerializeField] private Color wallShadowColor = new(0.56f, 0.61f, 0.7f, 1f);

#if UNITY_EDITOR
        private bool editorBuildQueued;
#endif

        private void OnEnable()
        {
            if (!autoBuildScene)
            {
                return;
            }

            BuildSceneIfNeeded();
        }

        private void OnValidate()
        {
            floorSize = SanitizeSize(floorSize);
            wallSize = SanitizeSize(wallSize);

            if (!autoBuildScene || Application.isPlaying)
            {
                return;
            }

            QueueEditorBuildScene();
        }

#if UNITY_EDITOR
        private void QueueEditorBuildScene()
        {
            if (editorBuildQueued)
            {
                return;
            }

            editorBuildQueued = true;
            EditorApplication.delayCall += RunQueuedEditorBuildScene;
        }

        private void RunQueuedEditorBuildScene()
        {
            editorBuildQueued = false;

            if (this == null || !autoBuildScene || Application.isPlaying)
            {
                return;
            }

            BuildSceneIfNeeded();
        }
#else
        private void QueueEditorBuildScene()
        {
        }
#endif

        private void BuildSceneIfNeeded()
        {
            var root = EnsureChild("PrototypeRoot");
            var floor = EnsureSurface(root, "InkFloor", floorPosition, floorSize, false);
            var wall = EnsureSurface(root, "InkWall", wallPosition, wallSize, true);
            var player = EnsurePlayer(root);

            ConfigureCamera(player.transform);

            floor.gameObject.SetActive(true);
            wall.gameObject.SetActive(true);
            player.SetActive(true);
        }

        private static Vector3 SanitizeSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(0.1f, size.x),
                Mathf.Max(0.1f, size.y),
                Mathf.Max(0.1f, size.z));
        }

        private Transform EnsureChild(string childName)
        {
            var existing = transform.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }

        private InkableSurface EnsureSurface(
            Transform root,
            string objectName,
            Vector3 localPosition,
            Vector3 surfaceSize,
            bool treatAsWall)
        {
            var existing = root.Find(objectName);
            GameObject surfaceObject;

            if (existing == null)
            {
                surfaceObject = new GameObject(objectName);
                surfaceObject.name = objectName;
                surfaceObject.transform.SetParent(root, false);
            }
            else
            {
                surfaceObject = existing.gameObject;
            }

            surfaceObject.transform.localPosition = localPosition;
            surfaceObject.transform.localRotation = Quaternion.identity;
            surfaceObject.transform.localScale = Vector3.one;

            ConfigureSurfaceGeometry(surfaceObject, surfaceSize, treatAsWall);

            var gameplaySurface = surfaceObject.GetComponent<InkGameplaySurface>();
            if (gameplaySurface == null)
            {
                gameplaySurface = surfaceObject.AddComponent<InkGameplaySurface>();
            }

            var inkableSurface = surfaceObject.GetComponent<InkableSurface>();
            if (inkableSurface == null)
            {
                inkableSurface = surfaceObject.AddComponent<InkableSurface>();
            }

            var visualSurface = surfaceObject.GetComponent<InkSurfaceVisual>();
            if (visualSurface == null)
            {
                visualSurface = surfaceObject.AddComponent<InkSurfaceVisual>();
            }

            inkableSurface.Configure(
                surfaceObject.GetComponent<Renderer>(),
                surfaceObject.GetComponent<BoxCollider>(),
                gameplaySurface,
                true,
                treatAsWall);

            gameplaySurface.EnsureInitialized();
            visualSurface.Configure(
                surfaceObject.GetComponent<Renderer>(),
                gameplaySurface,
                treatAsWall ? wallBaseColor : floorBaseColor,
                treatAsWall ? wallShadowColor : floorShadowColor);
            return inkableSurface;
        }

        private static void ConfigureSurfaceGeometry(GameObject surfaceObject, Vector3 surfaceSize, bool treatAsWall)
        {
            var meshFilter = surfaceObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = surfaceObject.AddComponent<MeshFilter>();
            }

            if (surfaceObject.GetComponent<MeshRenderer>() == null)
            {
                surfaceObject.AddComponent<MeshRenderer>();
            }

            meshFilter.sharedMesh = CreateSurfaceBoxMesh(surfaceObject.name, surfaceSize, treatAsWall);

            var meshCollider = surfaceObject.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(meshCollider);
                }
                else
                {
                    DestroyImmediate(meshCollider);
                }
            }

            var boxCollider = surfaceObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = surfaceObject.AddComponent<BoxCollider>();
            }

            if (treatAsWall)
            {
                boxCollider.size = surfaceSize;
                boxCollider.center = Vector3.zero;
            }
            else
            {
                boxCollider.size = surfaceSize;
                boxCollider.center = Vector3.zero;
            }
        }

        private static Mesh CreateSurfaceBoxMesh(string surfaceName, Vector3 surfaceSize, bool treatAsWall)
        {
            var halfWidth = surfaceSize.x * 0.5f;
            var halfHeight = surfaceSize.y * 0.5f;
            var halfDepth = surfaceSize.z * 0.5f;
            var mesh = new Mesh
            {
                name = $"{surfaceName}_PaintBoxMesh"
            };

            var vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, -halfDepth),
                new Vector3(halfWidth, -halfHeight, -halfDepth),
                new Vector3(-halfWidth, halfHeight, -halfDepth),
                new Vector3(halfWidth, halfHeight, -halfDepth),
                new Vector3(halfWidth, -halfHeight, -halfDepth),
                new Vector3(halfWidth, -halfHeight, halfDepth),
                new Vector3(halfWidth, halfHeight, -halfDepth),
                new Vector3(halfWidth, halfHeight, halfDepth),
                new Vector3(halfWidth, -halfHeight, halfDepth),
                new Vector3(-halfWidth, -halfHeight, halfDepth),
                new Vector3(halfWidth, halfHeight, halfDepth),
                new Vector3(-halfWidth, halfHeight, halfDepth),
                new Vector3(-halfWidth, -halfHeight, halfDepth),
                new Vector3(-halfWidth, -halfHeight, -halfDepth),
                new Vector3(-halfWidth, halfHeight, halfDepth),
                new Vector3(-halfWidth, halfHeight, -halfDepth),
                new Vector3(-halfWidth, halfHeight, -halfDepth),
                new Vector3(halfWidth, halfHeight, -halfDepth),
                new Vector3(-halfWidth, halfHeight, halfDepth),
                new Vector3(halfWidth, halfHeight, halfDepth),
                new Vector3(-halfWidth, -halfHeight, halfDepth),
                new Vector3(halfWidth, -halfHeight, halfDepth),
                new Vector3(-halfWidth, -halfHeight, -halfDepth),
                new Vector3(halfWidth, -halfHeight, -halfDepth),
            };

            mesh.vertices = vertices;
            mesh.triangles = new[]
            {
                0, 2, 1, 1, 2, 3,
                4, 6, 5, 5, 6, 7,
                8, 10, 9, 9, 10, 11,
                12, 14, 13, 13, 14, 15,
                16, 18, 17, 17, 18, 19,
                20, 22, 21, 21, 22, 23,
            };

            mesh.uv = CreateBoxUvs(vertices.Length);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector2[] CreateBoxUvs(int vertexCount)
        {
            var uvs = new Vector2[vertexCount];
            for (var i = 0; i < vertexCount; i += 4)
            {
                uvs[i] = new Vector2(0f, 0f);
                uvs[i + 1] = new Vector2(1f, 0f);
                uvs[i + 2] = new Vector2(0f, 1f);
                uvs[i + 3] = new Vector2(1f, 1f);
            }

            return uvs;
        }

        private GameObject EnsurePlayer(Transform root)
        {
            var existing = root.Find("Player");
            GameObject playerObject;

            if (existing == null)
            {
                playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerObject.name = "Player";
                playerObject.transform.SetParent(root, false);
            }
            else
            {
                playerObject = existing.gameObject;
            }

            playerObject.transform.localPosition = new Vector3(0f, 1f, -4f);
            playerObject.transform.localRotation = Quaternion.identity;
            playerObject.transform.localScale = Vector3.one;

            var collider = playerObject.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var controller = playerObject.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = playerObject.AddComponent<CharacterController>();
            }

            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;

            var inkState = playerObject.GetComponent<PlayerInkState>() ?? playerObject.AddComponent<PlayerInkState>();
            var inkTank = playerObject.GetComponent<PlayerInkTank>() ?? playerObject.AddComponent<PlayerInkTank>();
            var hitResolver = playerObject.GetComponent<InkHitResolver>() ?? playerObject.AddComponent<InkHitResolver>();
            var emitter = playerObject.GetComponent<InkWeaponEmitter>() ?? playerObject.AddComponent<InkWeaponEmitter>();
            var feedbackPool = playerObject.GetComponent<InkWeaponFeedbackPool>() ?? playerObject.AddComponent<InkWeaponFeedbackPool>();
            var movement = playerObject.GetComponent<PrototypePlayerController>() ?? playerObject.AddComponent<PrototypePlayerController>();
            var playerRenderer = playerObject.GetComponentInChildren<Renderer>();

            var firePoint = playerObject.transform.Find("FirePoint");
            if (firePoint == null)
            {
                firePoint = new GameObject("FirePoint").transform;
                firePoint.SetParent(playerObject.transform, false);
            }

            firePoint.localPosition = new Vector3(0f, 1.1f, 0.75f);
            firePoint.localRotation = Quaternion.identity;

            inkTank.Configure(inkState);
            emitter.Configure(hitResolver, firePoint, inkTank, Camera.main, feedbackPool);
            movement.Configure(controller, inkState, Camera.main != null ? Camera.main.transform : null, emitter, playerRenderer);

            return playerObject;
        }

        private void ConfigureCamera(Transform player)
        {
            if (Camera.main == null)
            {
                return;
            }

            var follow = Camera.main.GetComponent<PrototypeCameraFollow>();
            if (follow == null)
            {
                follow = Camera.main.gameObject.AddComponent<PrototypeCameraFollow>();
            }

            follow.SetTarget(player);

            var hud = Camera.main.GetComponent<PrototypeCombatHud>();
            if (hud == null)
            {
                hud = Camera.main.gameObject.AddComponent<PrototypeCombatHud>();
            }

            var tank = player.GetComponent<PlayerInkTank>();
            var state = player.GetComponent<PlayerInkState>();
            var emitter = player.GetComponent<InkWeaponEmitter>();
            hud.Configure(tank, state, emitter);
        }
    }
}
