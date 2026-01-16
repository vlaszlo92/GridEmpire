using UnityEngine;
using UnityEngine.InputSystem;
using GridEmpire.Core;
using GridEmpire.Visuals;
using GridEmpire.Gameplay;

namespace GridEmpire.Input
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        private GameControls _controls;
        private Camera _mainCamera;
        private ICellPresenter _lastSelectedPresenter;
        private PlayerProfile localPlayer;

        // Logikai változók a drag/select szétválasztásához
        private Vector2 _startClickPosition;
        private bool _isPotentialClick;
        [SerializeField] private float _dragThreshold = 10f; // Pixelben mért elmozdulás, ami felett már Drag

        public Vector2 CameraMoveDelta => _controls.Camera.Move.ReadValue<Vector2>();
        public float CameraZoomDelta => _controls.Camera.Zoom.ReadValue<float>();
        public bool IsSelectPressed => _controls.Player.Select.IsPressed();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            _controls = new GameControls();
            _mainCamera = Camera.main;
        }

        private void Start() => localPlayer = GameController.Instance?.GetLocalPlayer();

        private void OnEnable()
        {
            _controls.Enable();
            // Nem a .performed-re, hanem a gomb lenyomására és elengedésére figyelünk
            _controls.Player.Select.started += OnSelectStarted;
            _controls.Player.Select.canceled += OnSelectCanceled;
        }

        private void OnDisable()
        {
            _controls.Disable();
            _controls.Player.Select.started -= OnSelectStarted;
            _controls.Player.Select.canceled -= OnSelectCanceled;
        }

        private void OnSelectStarted(InputAction.CallbackContext context)
        {
            _startClickPosition = _controls.Player.PointerPosition.ReadValue<Vector2>();
            _isPotentialClick = true;
        }

        private void OnSelectCanceled(InputAction.CallbackContext context)
        {
            if (!_isPotentialClick) return;

            Vector2 endPosition = _controls.Player.PointerPosition.ReadValue<Vector2>();

            // Ha az elmozdulás kisebb, mint a küszöb, akkor ez egy érvényes Select
            if (Vector2.Distance(_startClickPosition, endPosition) < _dragThreshold)
            {
                ExecuteSelection(endPosition);
            }

            _isPotentialClick = false;
        }

        private void Update()
        {
            if (localPlayer == null) return;

            // Ha lenyomva tartjuk és mozgatjuk (Drag), akkor már nem lehet Click
            if (_isPotentialClick && CameraMoveDelta.sqrMagnitude > 0.1f)
            {
                Vector2 currentPos = _controls.Player.PointerPosition.ReadValue<Vector2>();
                if (Vector2.Distance(_startClickPosition, currentPos) > _dragThreshold)
                {
                    _isPotentialClick = false; // Innentõl kezdve ez egy Drag, nem lesz kijelölés a végén
                }
            }

            // Gyorsbillentyûk
            if (Keyboard.current.digit1Key.wasPressedThisFrame) RequestSpawn(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) RequestSpawn(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) RequestSpawn(2);
        }

        private void ExecuteSelection(Vector2 screenPosition)
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                ICellPresenter current = hit.collider.GetComponentInParent<ICellPresenter>();
                if (current != null && current is CellVisual visual)
                {
                    if (visual.Data.CurrentVisibility == VisibilityState.Hidden) return;

                    if (current != _lastSelectedPresenter)
                    {
                        if (_lastSelectedPresenter != null) _lastSelectedPresenter.SetSelected(false);
                        current.SetSelected(true);
                        _lastSelectedPresenter = current;
                        localPlayer.SelectedCell = visual.Data;
                        Debug.Log($"[Input] Sikeres kijelölés: {visual.Data.Q},{visual.Data.R}");
                    }
                }
            }
        }

        private void RequestSpawn(int slot) => UnitSpawner.OnRequestUnitSpawn?.Invoke(localPlayer.Id, slot, localPlayer.SelectedCell);
    }
}