using SplatoonInkPrototype.Player;
using SplatoonInkPrototype.Weapons;
using UnityEngine;

namespace SplatoonInkPrototype.UI
{
    /// <summary>
    /// Lightweight debug HUD for aiming, ink amount, and traversal state.
    /// </summary>
    public class PrototypeCombatHud : MonoBehaviour
    {
        [SerializeField] private PlayerInkTank inkTank;
        [SerializeField] private PlayerInkState inkState;
        [SerializeField] private InkWeaponEmitter weaponEmitter;

        private float outOfInkFlashUntil;

        private void OnEnable()
        {
            if (weaponEmitter != null)
            {
                weaponEmitter.ShotResolved += HandleShotResolved;
            }
        }

        private void OnDisable()
        {
            if (weaponEmitter != null)
            {
                weaponEmitter.ShotResolved -= HandleShotResolved;
            }
        }

        public void Configure(PlayerInkTank tank, PlayerInkState state, InkWeaponEmitter emitter)
        {
            if (weaponEmitter != null)
            {
                weaponEmitter.ShotResolved -= HandleShotResolved;
            }

            inkTank = tank;
            inkState = state;
            weaponEmitter = emitter;

            if (weaponEmitter != null)
            {
                weaponEmitter.ShotResolved += HandleShotResolved;
            }
        }

        private void OnGUI()
        {
            DrawCrosshair();
            DrawInkBar();
            DrawStateLabel();
        }

        private void DrawCrosshair()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var lineSize = 12f;
            var gap = 6f;
            var color = Time.time < outOfInkFlashUntil ? new Color(1f, 0.35f, 0.25f, 1f) : Color.white;

            DrawSolidRect(new Rect(center.x - 1f, center.y - gap - lineSize, 2f, lineSize), color);
            DrawSolidRect(new Rect(center.x - 1f, center.y + gap, 2f, lineSize), color);
            DrawSolidRect(new Rect(center.x - gap - lineSize, center.y - 1f, lineSize, 2f), color);
            DrawSolidRect(new Rect(center.x + gap, center.y - 1f, lineSize, 2f), color);
        }

        private void DrawInkBar()
        {
            if (inkTank == null)
            {
                return;
            }

            var width = 220f;
            var height = 18f;
            var x = 24f;
            var y = Screen.height - 48f;
            DrawSolidRect(new Rect(x, y, width, height), new Color(0f, 0f, 0f, 0.45f));
            DrawSolidRect(new Rect(x + 2f, y + 2f, (width - 4f) * inkTank.InkNormalized, height - 4f), Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(0.2f, 0.8f, 1f), inkTank.InkNormalized));
            GUI.Label(new Rect(x, y - 22f, width, 20f), $"Ink {(inkTank.InkNormalized * 100f):0}%");
        }

        private void DrawStateLabel()
        {
            if (inkState == null)
            {
                return;
            }

            GUI.Label(new Rect(24f, 16f, 320f, 24f), $"State: {inkState.CurrentTraversalState}");
        }

        private void HandleShotResolved(InkWeaponEmitter.ShotResult result)
        {
            if (result.ResultType == InkWeaponEmitter.ShotResultType.OutOfInk)
            {
                outOfInkFlashUntil = Time.time + 0.2f;
            }
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
