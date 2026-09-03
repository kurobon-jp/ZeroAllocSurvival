using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;

namespace ZeroAllocSurvival.Presentation
{
    [DisallowMultipleComponent, DefaultExecutionOrder(int.MaxValue)]
    public sealed class SimpleInputModule : BaseInputModule
    {
        private const int MousePointerId = -1;
        private PointerEventData _pointer;
        private GameObject _pressed;
        private GameObject _pressedClickHandler;
        private GameObject _raycastClickHandler;
        private int _activeTouchId = -1;
        private bool _mousePressed;

        protected override void OnEnable()
        {
            base.OnEnable();
            WarmupFrameworkInternals();
        }

        private void WarmupFrameworkInternals()
        {
            _pointer ??= new PointerEventData(eventSystem);
            ExecuteEvents.ExecuteHierarchy(gameObject, _pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(gameObject, _pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(gameObject, _pointer, ExecuteEvents.pointerClickHandler);
            RectTransformUtility.CalculateRelativeRectTransformBounds(gameObject.transform);
            var handlers = ListPool<IEventSystemHandler>.Get();
            var components = ListPool<Component>.Get();
            m_RaycastResultCache = new List<RaycastResult>(16);
            for (var i = 0; i < 16; i++)
            {
                handlers.Add(new EventSystemHandler());
                components.Add(this);
                m_RaycastResultCache.Add(default);
            }

            ListPool<IEventSystemHandler>.Release(handlers);
            ListPool<Component>.Release(components);
            m_RaycastResultCache.Sort(static (_, _) => 0);
            m_RaycastResultCache.Clear();
        }

        public override void Process()
        {
            if (Application.isMobilePlatform)
                ProcessTouches();
            else
                ProcessMouse();
        }

        private void ProcessMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _mousePressed = true;
                Press(MousePointerId, Input.mousePosition);
            }

            if (!_mousePressed || !Input.GetMouseButtonUp(0)) return;
            _mousePressed = false;
            Release(Input.mousePosition, false);
        }

        private void ProcessTouches()
        {
            var touchCount = Input.touchCount;
            if (_activeTouchId >= 0)
            {
                for (var i = 0; i < touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _activeTouchId) continue;
                    if (touch.phase == TouchPhase.Ended)
                        Release(touch.position, false);
                    else if (touch.phase == TouchPhase.Canceled)
                        Release(touch.position, true);
                    return;
                }

                Release(_pointer.position, true);
                return;
            }

            for (var i = 0; i < touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                _activeTouchId = touch.fingerId;
                Press(touch.fingerId, touch.position);
                return;
            }
        }

        private void Press(int pointerId, Vector2 position)
        {
            _pointer.Reset();
            _pointer.pointerId = pointerId;
            _pointer.position = position;
            _pointer.button = PointerEventData.InputButton.Left;
            _pointer.eligibleForClick = true;
            Raycast(position);

            var target = _pointer.pointerCurrentRaycast.gameObject;
            _pressedClickHandler = _raycastClickHandler;
            _pressed = ExecuteEvents.ExecuteHierarchy(target, _pointer, ExecuteEvents.pointerDownHandler);
            if (_pressed == null) _pressed = _pressedClickHandler;
            _pointer.pointerPress = _pressed;
            _pointer.rawPointerPress = target;
        }

        private void Release(Vector2 position, bool canceled)
        {
            _pointer.position = position;
            Raycast(position);

            if (_pressed != null)
                ExecuteEvents.Execute(_pressed, _pointer, ExecuteEvents.pointerUpHandler);

            var releasedClickHandler = canceled ? null : _raycastClickHandler;
            if (_pointer.eligibleForClick && _pressedClickHandler != null &&
                _pressedClickHandler == releasedClickHandler)
                ExecuteEvents.Execute(_pressedClickHandler, _pointer, ExecuteEvents.pointerClickHandler);

            _pointer.eligibleForClick = false;
            _pointer.pointerPress = null;
            _pointer.rawPointerPress = null;
            _pressed = null;
            _pressedClickHandler = null;
            _activeTouchId = -1;
        }

        private void Raycast(Vector2 position)
        {
            _pointer.position = position;
            m_RaycastResultCache.Clear();
            eventSystem.RaycastAll(_pointer, m_RaycastResultCache);
            _pointer.pointerCurrentRaycast = default;
            _raycastClickHandler = null;
            for (var i = 0; i < m_RaycastResultCache.Count; i++)
            {
                var result = m_RaycastResultCache[i];
                if (result.gameObject == null) continue;
                _pointer.pointerCurrentRaycast = result;
                _raycastClickHandler = result.gameObject;
                break;
            }

            m_RaycastResultCache.Clear();
        }

        private class EventSystemHandler : IEventSystemHandler
        {
        }
    }
}