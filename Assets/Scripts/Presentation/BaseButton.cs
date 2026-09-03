using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZeroAllocSurvival.Presentation
{
    [DisallowMultipleComponent]
    public class BaseButton : MonoBehaviour, IPointerClickHandler
    {
        private bool _interactable = true;
        private Action<int> _clickHandler;
        private int _clickContext;

        internal bool IsInteractable => _interactable;

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
        }

        internal void SetClickHandler(Action<int> handler, int context = 0)
        {
            _clickHandler = handler;
            _clickContext = context;
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            if (_interactable) _clickHandler?.Invoke(_clickContext);
        }
    }
}
