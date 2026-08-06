using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace GridEmpire.UI
{
    public class UnitSpawnButtonTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler
    {
        [SerializeField] private int unitIndex;

        private float _holdTimer = 0f;
        private bool _isPointerDown = false;
        private bool _hasTriggeredHold = false;
        private const float HoldThreshold = 0.35f; 

        public static UnityAction<int> OnSpawnRequested;
        public static UnityAction<int> OnUnitDescriptionRequested;

        private void Update()
        {
            if (_isPointerDown && !_hasTriggeredHold)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= HoldThreshold)
                {
                    _hasTriggeredHold = true;
                    OnUnitDescriptionRequested?.Invoke(unitIndex);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UnityEngine.Input.mousePresent && !UnityEngine.Input.touchSupported)
            {
                OnUnitDescriptionRequested?.Invoke(unitIndex);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPointerDown = true;
            _hasTriggeredHold = false;
            _holdTimer = 0f;

            if (UnityEngine.Input.mousePresent && !UnityEngine.Input.touchSupported)
            {
                OnUnitDescriptionRequested?.Invoke(unitIndex);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isPointerDown && !_hasTriggeredHold)
            {
                OnSpawnRequested?.Invoke(unitIndex);
            }

            _isPointerDown = false;
            _hasTriggeredHold = false;
        }
    }
}