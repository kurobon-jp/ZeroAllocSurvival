using UnityEngine;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Presentation
{
    /// <summary>A touch-positioned floating stick whose center follows an overextended drag.</summary>
    public sealed class VirtualPadPresenter : MonoBehaviour
    {
        [SerializeField, Min(32f)] private float baseDiameter = 180f;
        [SerializeField, Range(0f, .95f)] private float deadZone = .08f;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _baseRect;
        [SerializeField] private RectTransform _knobRect;

        private VirtualStickInputState _state;
        private RectTransform _coordinateRect;
        private Camera _uiCamera;
        private Vector2 _center;
        private bool _tracking;
        private int _touchId = -1;

        internal void Initialize(VirtualStickInputState state)
        {
            _state = state;
            _coordinateRect = _baseRect.parent as RectTransform;
            var canvas = _baseRect.GetComponentInParent<Canvas>()?.rootCanvas;
            _uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            SetVisible(false);
        }

        private void Update()
        {
            if (_state == null) return;
            if (Time.timeScale <= 0f)
            {
                Release();
                return;
            }

            var touchCount = Input.touchCount;
            for (var i = 0; i < touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (_tracking)
                {
                    if (touch.fingerId != _touchId) continue;
                    if (touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                        EndPointer(touch.fingerId);
                    else
                        DragPointer(touch.fingerId, touch.position);
                    return;
                }

                if (touch.phase != TouchPhase.Began) continue;
                BeginPointer(touch.fingerId, touch.position);
                return;
            }

            // A canceled touch may disappear from the legacy touch list without another sample.
            if (_tracking) Release();
        }

        private void BeginPointer(int pointerId, Vector2 position)
        {
            if (_state == null || _tracking) return;
            if (!TryScreenToPadPosition(position, out var localPosition)) return;
            _tracking = true;
            _touchId = pointerId;
            _center = localPosition;
            _baseRect.localPosition = _center;
            _knobRect.localPosition = Vector2.zero;
            _state.Set(Vector2.zero);
            SetVisible(true);
        }

        private void DragPointer(int pointerId, Vector2 position)
        {
            if (!_tracking || pointerId != _touchId) return;
            if (!TryScreenToPadPosition(position, out var localPosition)) return;

            var radius = baseDiameter * .5f;
            var offset = localPosition - _center;
            var distance = offset.magnitude;
            if (distance > radius)
            {
                offset = offset / distance * radius;
                distance = radius;
            }

            _knobRect.localPosition = offset;
            var normalized = radius > 0f ? offset / radius : Vector2.zero;
            var magnitude = normalized.magnitude;
            if (magnitude <= deadZone)
                normalized = Vector2.zero;
            else
                normalized = normalized.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);
            _state.Set(normalized);
        }

        private bool TryScreenToPadPosition(Vector2 screenPosition, out Vector2 localPosition)
        {
            localPosition = default;
            return _coordinateRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _coordinateRect, screenPosition, _uiCamera, out localPosition);
        }

        private void EndPointer(int pointerId)
        {
            if (!_tracking || pointerId != _touchId) return;
            Release();
        }

        private void OnDisable() => Release();

        private void Release()
        {
            if (!_tracking && _state is not { IsActive: true }) return;
            _tracking = false;
            _touchId = -1;
            _state?.Release();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
