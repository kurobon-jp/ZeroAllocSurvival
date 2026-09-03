using UnityEngine;

namespace ZeroAllocSurvival.Definitions
{
    [CreateAssetMenu(menuName = "Zero Alloc Survival/Experience Visual", fileName = "ExperienceVisual")]
    public sealed class ExperienceVisualDefinition : ScriptableObject
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;
        [SerializeField, Min(2)] private int mediumValue = 2;
        [SerializeField] private Color mediumColor = new(0f, .8f, 1f, 1f);
        [SerializeField, Min(3)] private int highValue = 3;
        [SerializeField] private Color highColor = new(1f, .75f, .05f, 1f);
        [SerializeField] private Vector3 scale = Vector3.one;
        [SerializeField] private float depthOffset = -.25f;

        public Sprite Sprite => sprite;
        public Color Color => color;
        public Vector3 Scale => scale;
        public float DepthOffset => depthOffset;

        public Color ColorForValue(int value)
        {
            if (value >= Mathf.Max(mediumValue + 1, highValue)) return highColor;
            if (value >= Mathf.Max(2, mediumValue)) return mediumColor;
            return color;
        }
    }
}
