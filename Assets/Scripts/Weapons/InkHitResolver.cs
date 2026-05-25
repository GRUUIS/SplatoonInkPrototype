using SplatoonInkPrototype.Ink.Gameplay;
using SplatoonInkPrototype.Ink.Surfaces;
using UnityEngine;

namespace SplatoonInkPrototype.Weapons
{
    /// <summary>
    /// Turns a physics hit into a gameplay-friendly ink result.
    /// Keeping this logic separate makes weapon types easier to extend later.
    /// </summary>
    public class InkHitResolver : MonoBehaviour
    {
        public bool TryResolveSurfaceHit(
            RaycastHit raycastHit,
            InkTeam team,
            float radius,
            Vector3 incomingDirection,
            out InkableSurface inkableSurface,
            out InkSurfaceHit surfaceHit)
        {
            inkableSurface = raycastHit.collider.GetComponentInParent<InkableSurface>();

            if (inkableSurface == null || !inkableSurface.Inkable || inkableSurface.GameplaySurface == null)
            {
                surfaceHit = default;
                return false;
            }

            surfaceHit = new InkSurfaceHit
            {
                WorldPoint = raycastHit.point,
                Normal = raycastHit.normal,
                IncomingDirection = incomingDirection,
                SurfaceDirection = inkableSurface.GetSurfaceDirection(incomingDirection),
                UV = ResolveSurfaceUv(raycastHit, inkableSurface),
                Radius = radius,
                Team = team,
                IsWall = inkableSurface.TreatAsWall,
            };

            return true;
        }

        private static Vector2 ResolveSurfaceUv(RaycastHit raycastHit, InkableSurface inkableSurface)
        {
            return inkableSurface.GetSurfaceUv(raycastHit.point);
        }
    }
}
