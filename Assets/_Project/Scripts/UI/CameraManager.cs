using UnityEngine;
using GridEmpire.Core;
using GridEmpire.Input;
using System.Collections;

namespace GridEmpire.UI
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 15f;
        [SerializeField] private float zoomSpeed = 500f;
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

        [Header("Idle Breath")]
        [SerializeField] private float breathAmplitude = 0.03f;
        [SerializeField] private float breathSpeed = 0.5f;

        [Header("Intro Animation")]
        [SerializeField] private float introStartDistance = 20f;
        [SerializeField] private float introStartHeight = 15f;
        [SerializeField] private float introDuration = 1.5f;
        [SerializeField] private float introDelay = 0.5f;

        private GridManager _gridManager;
        private bool _isIntroPlaying = false;
        private Vector3 _introStartPos;
        private Vector3 _introTargetPos;
        private Quaternion _introStartRot;
        private Quaternion _introTargetRot;
        private float _introElapsed = 0f;
        private float _introDelayElapsed = 0f;

        private float _baseY;
        private float _lastMoveTime;

        private void Awake()
        {
            StartCoroutine(WaitAndFocus());
        }

        private IEnumerator WaitAndFocus()
        {
            yield return new WaitUntil(() =>
                GameController.Instance != null &&
                GameController.Instance.GetLocalPlayer() != null &&
                GameController.Instance.GetLocalPlayer().BaseCell != null &&
                FindFirstObjectByType<GridManager>() != null
            );
            _gridManager = FindFirstObjectByType<GridManager>();
            FocusOnBase();
        }
        private void LateUpdate()
        {
            if (_isIntroPlaying)
            {
                HandleIntro();
                return;
            }

            if (InputManager.Instance == null) return;
            HandleMovement();
            HandleZoom();
            ClampPosition();
            ApplyBreath();
        }

        private void HandleIntro()
        {
            if (_introDelayElapsed < introDelay)
            {
                _introDelayElapsed += Time.deltaTime;
                return;
            }

            _introElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_introElapsed / introDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(_introStartPos, _introTargetPos, smoothT);

            if (t >= 1f)
            {
                transform.position = _introTargetPos;
                transform.rotation = _introTargetRot;
                //Debug.Log($"[Intro] VÉGE előtt pos={transform.position}");
                _isIntroPlaying = false;
                //Debug.Log($"[Intro] VÉGE után pos={transform.position}");
            }
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
            _lastMoveTime = Time.time;
        }

        private void HandleZoom()
        {
            float scroll = InputManager.Instance.CameraZoomDelta;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float minScrollStep = breathAmplitude * 2f + 0.1f;
            float zoomDirection = scroll > 0 ? 1f : -1f;
            Vector3 nextPos = transform.position + transform.forward * zoomDirection * zoomSpeed * Time.deltaTime;

            // Minimum scroll lépés biztosítása
            if (Mathf.Abs(nextPos.y - transform.position.y) < minScrollStep)
                nextPos = transform.position + transform.forward * zoomDirection * minScrollStep;

            if (nextPos.y >= zoomLimits.x + breathAmplitude && nextPos.y <= zoomLimits.y - breathAmplitude)
            {
                transform.position = nextPos;
                _baseY = transform.position.y;
            }
            _lastMoveTime = Time.time;
        }

        private void ClampPosition()
        {
            Vector3 before = transform.position;
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, xLimits.x, xLimits.y);
            pos.z = Mathf.Clamp(pos.z, zLimits.x, zLimits.y);
            pos.y = Mathf.Clamp(pos.y, zoomLimits.x, zoomLimits.y);
            transform.position = pos;
            if (before != transform.position)
                Debug.Log($"[Clamp] Pozíció változott: {before} → {transform.position}");
        }

        public void FocusOnBase()
        {
            var localPlayer = GameController.Instance?.GetLocalPlayer();
            if (_gridManager == null) _gridManager = FindFirstObjectByType<GridManager>();
            Debug.Log($"[Camera] FocusOnBase: localPlayer={localPlayer?.Id}, " +
                      $"baseCell={localPlayer?.BaseCell?.Id}, gridManager={_gridManager != null}");

            if (localPlayer == null || localPlayer.BaseCell == null || _gridManager == null)
                return;

            Vector3 myBasePos = _gridManager.GetWorldPosition(
                localPlayer.BaseCell.Q,
                localPlayer.BaseCell.R
            );

            var allPlayers = GameController.Instance.Players;
            int oppositeId = (localPlayer.Id + 3) % allPlayers.Count;
            PlayerProfile oppositePlayer = null;
            foreach (var p in allPlayers)
                if (p.Id == oppositeId) { oppositePlayer = p; break; }

            Vector3 directionToOpposite;
            if (oppositePlayer != null && oppositePlayer.BaseCell != null)
            {
                Vector3 oppositeBasePos = _gridManager.GetWorldPosition(
                    oppositePlayer.BaseCell.Q,
                    oppositePlayer.BaseCell.R
                );
                directionToOpposite = (oppositeBasePos - myBasePos).normalized;
            }
            else
            {
                directionToOpposite = (Vector3.zero - myBasePos).normalized;
            }

            // Célpozíció – egy picit közelebb mint a max zoom
            Vector3 targetPos = myBasePos - directionToOpposite * (cameraDistance - 2.5f);
            targetPos.y = cameraHeight;

            // Startpozíció – távolabb és magasabb
            Vector3 startPos = myBasePos - directionToOpposite * (cameraDistance + introStartDistance);
            startPos.y = cameraHeight + introStartHeight;

            Quaternion targetRot = Quaternion.LookRotation(directionToOpposite) * Quaternion.Euler(cameraTilt, 0f, 0f);

            // Kamera azonnal a startpozícióba ugrik
            transform.position = startPos;
            transform.rotation = targetRot;

            _introStartRot = targetRot;
            _introStartPos = startPos;
            _introTargetPos = targetPos;
            _introTargetRot = targetRot;
            _introElapsed = 0f;
            _introDelayElapsed = 0f;
            _isIntroPlaying = true;
            _baseY = targetPos.y;
        }
        private void ApplyBreath()
        {
            Vector3 pos = transform.position;
            float breathY = _baseY + Mathf.Sin(Time.time * breathSpeed) * breathAmplitude;
            pos.y = Mathf.Clamp(breathY, zoomLimits.x + breathAmplitude, zoomLimits.y - breathAmplitude);
            transform.position = pos;
        }
    }
}