using SplatoonInkPrototype.Ink.Gameplay;
using SplatoonInkPrototype.Ink.Surfaces;
using SplatoonInkPrototype.Player;
using SplatoonInkPrototype.Weapons;
using UnityEngine;

namespace SplatoonInkPrototype.Core
{
    /// <summary>
    /// Creates a minimal playable prototype arena in the sample scene.
    /// </summary>
    [ExecuteAlways]
    public class PrototypeSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private bool autoBuildScene = true;

        private void OnEnable()
        {
            if (!autoBuildScene)
            {
                return;
            }

            BuildSceneIfNeeded();
        }

        private void BuildSceneIfNeeded()
        {
            var root = EnsureChild("PrototypeRoot");
            var floor = EnsureSurface(root, "InkFloor", new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 14f), false);
            var wall = EnsureSurface(root, "InkWall", new Vector3(0f, 2f, 6f), new Vector3(14f, 4f, 1f), true);
            var player = EnsurePlayer(root);

            ConfigureCamera(player.transform);

            floor.gameObject.SetActive(true);
            wall.gameObject.SetActive(true);
            player.SetActive(true);
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
            Vector3 localScale,
            bool treatAsWall)
        {
            var existing = root.Find(objectName);
            GameObject surfaceObject;

            if (existing == null)
            {
                surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surfaceObject.name = objectName;
                surfaceObject.transform.SetParent(root, false);
            }
            else
            {
                surfaceObject = existing.gameObject;
            }

            surfaceObject.transform.localPosition = localPosition;
            surfaceObject.transform.localScale = localScale;

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

            inkableSurface.Configure(
                surfaceObject.GetComponent<Renderer>(),
                surfaceObject.GetComponent<Collider>(),
                gameplaySurface,
                true,
                treatAsWall);

            gameplaySurface.EnsureInitialized();
            return inkableSurface;
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
            var movement = playerObject.GetComponent<PrototypePlayerController>() ?? playerObject.AddComponent<PrototypePlayerController>();

            var firePoint = playerObject.transform.Find("FirePoint");
            if (firePoint == null)
            {
                firePoint = new GameObject("FirePoint").transform;
                firePoint.SetParent(playerObject.transform, false);
            }

            firePoint.localPosition = new Vector3(0f, 1.1f, 0.75f);
            firePoint.localRotation = Quaternion.identity;

            inkTank.Configure(inkState);
            emitter.Configure(hitResolver, firePoint, inkTank);
            movement.Configure(controller, inkState, Camera.main != null ? Camera.main.transform : null);

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
        }
    }
}
