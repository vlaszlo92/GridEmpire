using UnityEngine;
using UnityEngine.InputSystem;
using GridEmpire.Core;
using GridEmpire.Visuals;
using GridEmpire.Gameplay;
using System;

namespace GridEmpire.Input
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [SerializeField] private GameControls _controls;
        private Camera _mainCamera;
        private ICellPresenter _lastSelectedPresenter;
        private PlayerProfile localPlayer;

        [Header("Selection Prefabs")]
        [SerializeField] private GameObject cellSelectionPrefab; // A mezõ kijelölõ kerete
        [SerializeField] private GameObject unitSelectionPrefab; // A nyíl az egységhez

        private GameObject _activeCellSelection;
        private GameObject _activeUnitSelection;
        private GameObject _activeArrow; 
        
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
            if (_controls == null)
            {
                _controls = new GameControls();
            }
            _controls.Enable();
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
            if (Keyboard.current.digit4Key.wasPressedThisFrame) RequestSpawn(3);
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

                    // --- 1. ÚJ MEZÕRE KATTINTOTTUNK ---
                    if (current != _lastSelectedPresenter)
                    {
                        ClearAllSelection();

                        _lastSelectedPresenter = current;
                        localPlayer.SelectedCell = visual.Data;

                        // Mezõ kijelölõ mozgatása és bekapcsolása
                        ShowCellSelection(visual.transform);
                        return;
                    }

                    // --- 2. UGYANARRA A MEZÕRE KATTINTOTTUNK ---
                    UnitController unitOnCell = visual.Data.GetFirstOccupier() as UnitController;

                    if (GameController.Instance.SelectedUnit == null) // Eddig a mezõ volt kijelölve
                    {
                        if (unitOnCell != null)
                        {
                            // Váltunk az egységre: Mezõ keret KI, Nyíl BE
                            HideCellSelection();
                            SelectUnit(unitOnCell);
                        }
                        else
                        {
                            // Nincs egység: Mindent KI
                            ClearAllSelection();
                        }
                    }
                    else // Eddig az egység volt kijelölve
                    {
                        ClearAllSelection();
                    }
                }
                else
                {
                    ClearAllSelection();
                }
            }
        }

        private void ShowCellSelection(Transform cellTransform)
        {
            if (_activeCellSelection == null)
                _activeCellSelection = Instantiate(cellSelectionPrefab);

            _activeCellSelection.SetActive(true);

            _activeCellSelection.transform.SetParent(cellTransform, false);
            _activeCellSelection.transform.localRotation = Quaternion.identity;
        }

        private void HideCellSelection() => _activeCellSelection?.SetActive(false);

        private void SelectUnit(UnitController unit)
        {
            GameController.Instance.SelectedUnit = unit;
            if (_activeUnitSelection == null) _activeUnitSelection = Instantiate(unitSelectionPrefab);
            _activeUnitSelection.SetActive(true);
            _activeUnitSelection.transform.SetParent(unit.transform, false);
            _activeUnitSelection.transform.localPosition = new Vector3(0, 0, 0);
        }

        private void DeselectUnit()
        {
            GameController.Instance.SelectedUnit = null;
            if (_activeUnitSelection != null)
            {
                _activeUnitSelection.SetActive(false);
                _activeUnitSelection.transform.SetParent(null); // Ne törlõdjön, ha az egység meghal
            }
        }

        private void ClearAllSelection()
        {
            _lastSelectedPresenter = null;
            localPlayer.SelectedCell = null;
            HideCellSelection();
            DeselectUnit();
        }
        private void RequestSpawn(int slot) => UnitSpawner.OnRequestUnitSpawn?.Invoke(localPlayer.Id, slot, localPlayer.SelectedCell);
    }
}