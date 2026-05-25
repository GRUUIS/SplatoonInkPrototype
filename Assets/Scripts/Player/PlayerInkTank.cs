using UnityEngine;

namespace SplatoonInkPrototype.Player
{
    /// <summary>
    /// Handles weapon ink consumption and passive refilling.
    /// </summary>
    public class PlayerInkTank : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField] private float maxInk = 1f;
        [SerializeField] private float startingInk = 1f;

        [Header("Refill")]
        [SerializeField] private float baseRefillPerSecond = 0.2f;
        [SerializeField] private PlayerInkState inkState;

        public float CurrentInk { get; private set; }
        public float InkNormalized => maxInk <= 0f ? 0f : CurrentInk / maxInk;

        private void Awake()
        {
            CurrentInk = Mathf.Clamp(startingInk, 0f, maxInk);

            if (inkState == null)
            {
                inkState = GetComponent<PlayerInkState>();
            }
        }

        private void Update()
        {
            var refillMultiplier = inkState != null ? inkState.CurrentRefillMultiplier : 1f;
            CurrentInk = Mathf.Clamp(CurrentInk + (baseRefillPerSecond * refillMultiplier * Time.deltaTime), 0f, maxInk);
        }

        public bool TryConsume(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (CurrentInk < amount)
            {
                return false;
            }

            CurrentInk -= amount;
            return true;
        }

        public void Configure(PlayerInkState state)
        {
            inkState = state;
        }
    }
}
