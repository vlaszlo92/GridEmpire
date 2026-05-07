using UnityEngine;
using UnityEngine.InputSystem;
using GridEmpire.Core;
using GridEmpire.Visuals;
using GridEmpire.Gameplay;
using System.Collections;

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
        [SerializeField] private GameObject cellSelectionPrefab;
        [SerializeField] private GameObject unitSelectionPrefab;

        private GameObject _activeCellSelection;
        private GameObject _activeUnitSelection;

        private Vector2 _startClickPosition;
        private bool _isPotentialClick;
        private bool _clickPending;
        [SerializeField] private float _dragThreshold = 10f;

        public Vector2 CameraMoveDelta => _controls.Camera.Move.ReadValue<Vector2>();
        public float CameraZoomDelta => _controls.Camera.Zoom.ReadValue<float>();
        public bool IsSelectPressed => _controls.Player.Select.IsPressed();
        public bool isFieldSelection;

        private Coroutine _clearRoutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            _controls = new GameControls();
            _mainCamera = Camera.main;
            isFieldSelection = true;
        }

        private void OnEnable()
        {
            if (_controls == null) _controls = new GameControls();
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
            // IsPointerOverGameObject itt nem megbízható – az OnSelectCanceled-ben ellenõrzünk
            _startClickPosition = _controls.Player.PointerPosition.ReadValue<Vector2>();
            _isPotentialClick = true;
        }

        private void OnSelectCanceled(InputAction.CallbackContext context)
        {
            if (!_isPotentialClick) return;
            _isPotentialClick = false;

            Vector2 endPosition = _controls.Player.PointerPosition.ReadValue<Vector2>();
            if (Vector2.Distance(_startClickPosition, endPosition) < _dragThreshold)
                _clickPending = true;
        }

        private void Update()
        {
            if (localPlayer == null)
            {
                localPlayer = GameController.Instance?.GetLocalPlayer();
                return;
            }

            // Drag detektálás
            if (_isPotentialClick && Vector2.Distance(_startClickPosition, _controls.Player.PointerPosition.ReadValue<Vector2>()) > _dragThreshold)
                _isPotentialClick = false;

            // Kattintás feldolgozása Update-bõl – itt már megbízható az IsPointerOverGameObject
            if (_clickPending)
            {
                _clickPending = false;

                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return; // UI felett volt – ne csináljunk semmit

                ExecuteSelection(_controls.Player.PointerPosition.ReadValue<Vector2>());
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame) RequestSpawn(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) RequestSpawn(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) RequestSpawn(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) RequestSpawn(3);
        }

        private void ExecuteSelection(Vector2 screenPosition)
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                // Sem UI, sem grid – ne csinálj semmit (törölt: RequestClearWithDelay)
                return;
            }

            CellVisual visual = hit.collider.GetComponentInParent<CellVisual>();
            if (visual == null || visual.Data.CurrentVisibility == VisibilityState.Hidden)
            {
                ClearAllSelection();
                return;
            }

            UnitController unitOnCell = visual.Data.GetOccupier() as UnitController;
            ProcessSelectionLogic(visual, unitOnCell);
        }

        private void ProcessSelectionLogic(CellVisual visual, UnitController unit)
        {            
            // --- UNIT SELECTION MODE ---
            if (!isFieldSelection)
            {
                if (unit != null && unit.OwnerId == localPlayer.Id)
                {
                    SelectUnit(unit);
                    localPlayer.SelectedCell = visual.Data;
                    _lastSelectedPresenter = visual;
                }
                else
                {
                    ClearAllSelection();
                }
                return;
            }

            // --- FIELD SELECTION MODE ---
                        
            // A: Új mezõ kijelölése
            if (_lastSelectedPresenter == null && GameController.Instance.SelectedUnit == null)
            {
                ClearAllSelection();
                _lastSelectedPresenter = visual;
                localPlayer.SelectedCell = visual.Data;
                ShowCellSelection(visual.transform);
                return;
            }

            // B: Más mezõre kattintás -> törlés
            if (!ReferenceEquals(visual, _lastSelectedPresenter) || GameController.Instance.SelectedUnit != null)
            {
                ClearAllSelection();
                return;
            }

            // C: Dupla kattintás ugyanarra a mezõre – váltás az egységre
            if (unit != null && GameController.Instance.SelectedUnit == null && unit.OwnerId == localPlayer.Id)
            {
                HideCellSelection();
                _lastSelectedPresenter = null;
                SelectUnit(unit);
                return;
            }

            // D: Minden más esetben törlés
            ClearAllSelection();
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
            if (_activeUnitSelection == null)
                _activeUnitSelection = Instantiate(unitSelectionPrefab);

            _activeUnitSelection.SetActive(true);
            _activeUnitSelection.transform.SetParent(unit.transform, false);
            _activeUnitSelection.transform.localPosition = Vector3.zero;
        }

        private void DeselectUnit()
        {
            GameController.Instance.SelectedUnit = null;
            if (_activeUnitSelection != null)
            {
                _activeUnitSelection.SetActive(false);
                _activeUnitSelection.transform.SetParent(null);
            }
        }

        private void ClearAllSelection()
        {
            _lastSelectedPresenter = null;
            if (localPlayer != null) localPlayer.SelectedCell = null;
            HideCellSelection();
            DeselectUnit();
        }

        private void RequestSpawn(int slot)
        {
            if (localPlayer == null) return;

            var spawner = GameController.Instance?.GetSpawnerByPlayerId(localPlayer.Id) as UnitSpawner;
            if (spawner != null && spawner.GetQueue().Count >= UnitSpawner.MaxQueueSize) return;

            var targetCell = localPlayer.SelectedCell ?? localPlayer.BaseCell;
            if (targetCell != null)
                UnitSpawner.OnRequestUnitSpawn?.Invoke(localPlayer.Id, slot, targetCell);
            else
                Debug.LogWarning("[InputManager] Nem sikerült: Nincs cella!");
        }

        public void SetSelectionType(bool isField)
        {
            isFieldSelection = isField;
            ClearAllSelection();
        }
    }
}