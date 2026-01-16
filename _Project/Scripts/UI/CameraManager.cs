using UnityEngine;
using GridEmpire.Core;
using GridEmpire.Input;

namespace GridEmpire.UI
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float zoomSpeed = 50f; // Megemelve a jobb érzetért
        [SerializeField] private Vector2 zoomLimits = new Vector2(2f, 35f);
        [SerializeField] private Vector3 offset = new Vector3(0, 18, -12);

        private void Start()
        {
            FocusOnBase();
        }

        private void LateUpdate()
        {
            if (InputManager.Instance == null) return;

            HandleMovement();
            HandleZoom();
        }

        private void HandleMovement()
        {
            // Csak akkor mozgunk, ha le van nyomva a gomb/ujj
            if (InputManager.Instance.IsSelectPressed)
            {
                Vector2 delta = InputManager.Instance.CameraMoveDelta;

                if (delta.sqrMagnitude > 0.01f)
                {
                    // Irányok vetítése a vízszintes síkra
                    Vector3 forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
                    Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

                    // Mozgás kiszámítása
                    Vector3 move = (right * -delta.x + forward * -delta.y) * moveSpeed * Time.deltaTime;
                    transform.position += move;
                }
            }
        }

        private void HandleZoom()
        {
            float scroll = InputManager.Instance.CameraZoomDelta;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Senior tipp: A scroll értéke hardvertõl függõen lehet 0.1 vagy 120 is.
                // Itt egy normalizáltabb megközelítést használunk.
                float zoomDirection = scroll > 0 ? 1f : -1f;

                // A kamera elõre/hátra mozgatása a saját tengelyén
                Vector3 zoomVector = transform.forward * zoomDirection * zoomSpeed * Time.deltaTime;
                Vector3 nextPos = transform.position + zoomVector;

                // Csak akkor mozdulunk, ha a határokon belül maradunk
                if (nextPos.y >= zoomLimits.x && nextPos.y <= zoomLimits.y)
                {
                    transform.position = nextPos;
                }
            }
        }

        public void FocusOnBase()
        {
            var localPlayer = GameController.Instance?.GetLocalPlayer();
            var gridManager = Object.FindFirstObjectByType<GridManager>();

            if (localPlayer != null && localPlayer.BaseCell != null && gridManager != null)
            {
                Vector3 basePos = gridManager.GetWorldPosition(localPlayer.BaseCell.Q, localPlayer.BaseCell.R);
                transform.position = basePos + offset;
                transform.rotation = Quaternion.Euler(60, localPlayer.Id * 180f, 0);
            }
        }
    }
}