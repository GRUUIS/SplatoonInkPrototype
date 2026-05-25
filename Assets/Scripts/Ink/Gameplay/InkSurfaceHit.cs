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
        public Vector2 UV;
        public float Radius;
        public InkTeam Team;
        public bool IsWall;
    }
}
