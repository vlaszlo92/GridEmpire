using UnityEngine;
using GridEmpire.Core;
using GridEmpire.Input;

namespace GridEmpire.UI
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private Vector2 zoomLimits = new Vector2(5f, 40f);

        [Header("Position Clamp")]
        [SerializeField] private Vector2 xLimits = new Vector2(-80f, 80f);
        [SerializeField] private Vector2 zLimits = new Vector2(-80f, 80f);

        [Header("Drag Settings")]
        [SerializeField] private float maxDeltaPerFrame = 5f;

        [Header("Camera Focus")]
        [SerializeField] private float cameraHeight = 15f;
        [SerializeField] private float cameraDistance = 10f;
        [SerializeField] private float cameraTilt = 45f;

        private bool _isPlayerSpawned = false;

        private void OnEnable()
        {
            GameController.OnLocalPlayerReady += FocusOnBase;
        }

        private void OnDisable()
        {
            GameController.OnLocalPlayerReady -= FocusOnBase;
        }
        private void LateUpdate()
        {
            if (!_isPlayerSpawned)
            {
                FocusOnBase();
                return;
            }

            if (InputManager.Instance == null) return;

            HandleMovement();
            HandleZoom();
            ClampPosition();
        }

        private void HandleMovement()
        {
            if (!InputManager.Instance.IsSelectPressed) return;

            Vector2 delta = InputManager.Instance.CameraMoveDelta;
            if (delta.sqrMagnitude < 0.01f) return;

            delta = Vector2.ClampMagnitude(delta, maxDeltaPerFrame);

            Vector3 forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 move = (right * -delta.x + forward * -delta.y) * moveSpeed * Time.deltaTime;

            transform.position += move;
        }

        private void HandleZoom()
        {
            float scroll = InputManager.Instance.CameraZoomDelta;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float zoomDirection = scroll > 0 ? 1f : -1f;
            Vector3 nextPos = transform.position + transform.forward * zoomDirection * zoomSpeed * Time.deltaTime;

            if (nextPos.y >= zoomLimits.x && nextPos.y <= zoomLimits.y)
                transform.position = nextPos;
        }

        private void ClampPosition()
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, xLimits.x, xLimits.y);
            pos.z = Mathf.Clamp(pos.z, zLimits.x, zLimits.y);
            pos.y = Mathf.Clamp(pos.y, zoomLimits.x, zoomLimits.y);
            transform.position = pos;
        }

        public void FocusOnBase()
        {
            var localPlayer = GameController.Instance?.GetLocalPlayer();
            var gridManager = FindFirstObjectByType<GridManager>();
            if (localPlayer == null || localPlayer.BaseCell == null || gridManager == null)
                return;

            Debug.Log("CameraManager: FocusOnBase called. LocalPlayer: " + (localPlayer.Id) + " " + localPlayer.BaseCell + ", GridManager: " + (gridManager != null));
            // Saját bázis pozíciója
            Vector3 myBasePos = gridManager.GetWorldPosition(
                localPlayer.BaseCell.Q,
                localPlayer.BaseCell.R
            );

            // Átlós ellenfél: (id + 3) % playerCount
            var allPlayers = GameController.Instance.Players;
            int oppositeId = (localPlayer.Id + 3) % allPlayers.Count;
            PlayerProfile oppositePlayer = null;

            foreach (var p in allPlayers)
            {
                if (p.Id == oppositeId) { oppositePlayer = p; break; }
            }

            Vector3 directionToOpposite;

            if (oppositePlayer != null && oppositePlayer.BaseCell != null)
            {
                Vector3 oppositeBasePos = gridManager.GetWorldPosition(
                    oppositePlayer.BaseCell.Q,
                    oppositePlayer.BaseCell.R
                );
                directionToOpposite = (oppositeBasePos - myBasePos).normalized;
            }
            else
            {
                // Fallback: pálya közepe felé
                directionToOpposite = (Vector3.zero - myBasePos).normalized;
            }

            // Kamera: bázis mögött, ellentétes irányban, megadott magasságban
            Vector3 camPos = myBasePos - directionToOpposite * cameraDistance;
            camPos.y = cameraHeight;
            transform.position = camPos;

            // Forgatás: előre néz az ellenfél bázisa felé, dőlésszöggel
            Vector3 lookDir = directionToOpposite;
            lookDir.y = 0f;
            Quaternion horizontalRot = Quaternion.LookRotation(lookDir);
            transform.rotation = horizontalRot * Quaternion.Euler(cameraTilt, 0f, 0f);

            _isPlayerSpawned = true;
        }
    }
}