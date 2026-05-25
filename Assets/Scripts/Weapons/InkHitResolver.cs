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
                UV = ResolveSurfaceUv(raycastHit, inkableSurface),
                Radius = radius,
                Team = team,
                IsWall = inkableSurface.TreatAsWall,
            };

            return true;
        }

        private static Vector2 ResolveSurfaceUv(RaycastHit raycastHit, InkableSurface inkableSurface)
        {
            var uv = raycastHit.textureCoord;
            if (uv != Vector2.zero)
            {
                return uv;
            }

            return inkableSurface.GetSurfaceUv(raycastHit.point);
        }
    }
}
