using UnityEngine;
using UnityEngine.UI;

namespace ZeroAllocSurvival.Presentation
{
    public sealed class GaugePresenter : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        private float _appliedFill = -1f;

        internal void SetProgress(float current, float max)
        {
            var value = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            if (Mathf.Approximately(value, _appliedFill)) return;
            _appliedFill = value;
            _fill.fillAmount = value;
        }
    }
}