using UnityEngine;

namespace SplatoonInkPrototype.Ink.Gameplay
{
    /// <summary>
    /// A normalized description of where a shot touched an inkable surface.
    /// </summary>
    public struct InkSurfaceHit
    {
        public Vector3 WorldPoint;
        public Vector3 Normal;
        public Vector3 IncomingDirection;
        public Vector2 SurfaceDirection;
        public Vector2 UV;
        public float Radius;
        public InkTeam Team;
        public bool IsWall;
    }
}
