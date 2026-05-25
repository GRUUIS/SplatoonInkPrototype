using UnityEngine;

namespace SplatoonInkPrototype.Weapons
{
    /// <summary>
    /// Data-only description of one weapon's ink behavior.
    /// Designers can tweak this asset without changing code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InkWeaponConfig",
        menuName = "Splatoon Ink Prototype/Weapons/Ink Weapon Config")]
    public class InkWeaponConfig : ScriptableObject
    {
        public enum WeaponType
        {
            Shooter,
            Roller,
            Charger,
            Slosher,
            Blaster,
        }

        [Header("Core")]
        [SerializeField] private WeaponType weaponType = WeaponType.Shooter;
        [SerializeField] private float fireRate = 6f;
        [SerializeField] private float inkCost = 0.08f;
        [SerializeField] private float damage = 25f;

        [Header("Ballistics")]
        [SerializeField] private float range = 20f;
        [SerializeField] private float spread = 0.02f;
        [SerializeField] private float projectileSpeed = 28f;
        [SerializeField] private float projectileGravity = 18f;
        [SerializeField, Range(0f, 1f)] private float projectileArcPreference = 0.2f;

        [Header("Ink")]
        [SerializeField] private float inkRadius = 1.25f;
        [SerializeField] private int splashCount = 4;
        [SerializeField] private float splashRandomness = 0.45f;

        [Header("Feedback")]
        [SerializeField] private float tracerWidth = 0.12f;
        [SerializeField] private float tracerDuration = 0.07f;
        [SerializeField] private float impactScale = 0.4f;

        public WeaponType Type => weaponType;
        public float FireRate => fireRate;
        public float InkCost => inkCost;
        public float Damage => damage;
        public float Range => range;
        public float Spread => spread;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileGravity => projectileGravity;
        public float ProjectileArcPreference => projectileArcPreference;
        public float InkRadius => inkRadius;
        public int SplashCount => splashCount;
        public float SplashRandomness => splashRandomness;
        public float TracerWidth => tracerWidth;
        public float TracerDuration => tracerDuration;
        public float ImpactScale => impactScale;
    }
}
