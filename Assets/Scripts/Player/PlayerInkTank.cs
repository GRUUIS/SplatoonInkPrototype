using UnityEngine;

namespace SplatoonInkPrototype.Player
{
    /// <summary>
    /// Handles weapon ink consumption and passive refilling.
    /// </summary>
    public class PlayerInkTank : MonoBehaviour
    {
        public event System.Action<float> InkNormalizedChanged;

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
            SetInkAmount(CurrentInk + (baseRefillPerSecond * refillMultiplier * Time.deltaTime));
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

            SetInkAmount(CurrentInk - amount);
            return true;
        }

        public void Configure(PlayerInkState state)
        {
            inkState = state;
        }

        private void SetInkAmount(float amount)
        {
            var previousNormalized = InkNormalized;
            CurrentInk = Mathf.Clamp(amount, 0f, maxInk);

            if (!Mathf.Approximately(previousNormalized, InkNormalized))
            {
                InkNormalizedChanged?.Invoke(InkNormalized);
            }
        }
    }
}
