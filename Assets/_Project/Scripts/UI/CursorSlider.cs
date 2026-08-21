using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CursorSlider : MonoBehaviour
{
    [SerializeField] private Slider cursorScaleSlider;

    private Coroutine updateCoroutine;
    private float pendingValue;
    private bool isDragging;

    private void Start()
    {
        if (cursorScaleSlider == null || CursorManager.Instance == null)
            return;

        cursorScaleSlider.minValue = 0.5f;
        cursorScaleSlider.maxValue = 3f;

        cursorScaleSlider.SetValueWithoutNotify(
            CursorManager.Instance.GetCursorScale());

        cursorScaleSlider.onValueChanged.AddListener(OnValueChanged);

        var eventTrigger = cursorScaleSlider.gameObject.GetComponent<EventTrigger>();

        if (eventTrigger == null)
            eventTrigger = cursorScaleSlider.gameObject.AddComponent<EventTrigger>();

        var pointerUpEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };

        pointerUpEntry.callback.AddListener(_ => OnPointerUp());

        eventTrigger.triggers.Add(pointerUpEntry);
    }

    private void OnValueChanged(float value)
    {
        pendingValue = value;
        isDragging = true;

        if (updateCoroutine == null)
            updateCoroutine = StartCoroutine(UpdateCursorScale());
    }

    private IEnumerator UpdateCursorScale()
    {
        while (isDragging)
        {
            yield return new WaitForSeconds(0.25f);

            if (isDragging)
                CursorManager.Instance?.SetCursorScale(pendingValue);
        }

        updateCoroutine = null;
    }

    private void OnPointerUp()
    {
        isDragging = false;

        CursorManager.Instance?.SetCursorScale(
            cursorScaleSlider.value);

        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
            updateCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (cursorScaleSlider != null)
            cursorScaleSlider.onValueChanged.RemoveListener(OnValueChanged);

        if (updateCoroutine != null)
            StopCoroutine(updateCoroutine);
    }
}