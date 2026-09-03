using UnityEngine;

namespace ZeroAllocSurvival.Presentation
{
    public abstract class BasePanelPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private SimpleRaycaster raycaster;
        public bool IsVisible { get; private set; } = true;

        public void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;
            IsVisible = visible;
            canvasGroup.alpha = visible ? 1f : 0f;
            raycaster.enabled = visible;
        }
    }
}