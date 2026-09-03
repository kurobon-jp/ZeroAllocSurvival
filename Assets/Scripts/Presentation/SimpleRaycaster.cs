using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZeroAllocSurvival.Presentation
{
    [DisallowMultipleComponent, DefaultExecutionOrder(int.MaxValue - 1)]
    public class SimpleRaycaster : BaseRaycaster
    {
        private Canvas _canvas;

        [SerializeField] private RectTransform[] targets;

        public override Camera eventCamera
        {
            get
            {
                var renderMode = _canvas.renderMode;
                if (renderMode == RenderMode.ScreenSpaceOverlay
                    || (renderMode == RenderMode.ScreenSpaceCamera && _canvas.worldCamera == null))
                    return null;

                return _canvas.worldCamera ?? Camera.main;
            }
        }

        protected override void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (targets == null) return;
            for (var i = targets.Length - 1; i >= 0; i--)
            {
                var target = targets[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        target, eventData.position, eventCamera))
                    continue;

                resultAppendList.Add(new RaycastResult
                {
                    gameObject = target.gameObject,
                    module = this,
                    distance = 0f,
                    index = resultAppendList.Count,
                    depth = i,
                    sortingLayer = _canvas.sortingLayerID,
                    sortingOrder = _canvas.sortingOrder,
                    screenPosition = eventData.position
                });
            }
        }
    }
}