using UnityEngine;

namespace ZeroAllocSurvival.Definitions
{
    [CreateAssetMenu(menuName = "Zero Alloc Survival/Character", fileName = "Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private CharacterVisualDefinition visual;
        [SerializeField] private CharacterParameters parameters;
        [SerializeField, Min(1)] private int experienceReward = 1;
        [SerializeField, Range(0, 1)] private int avoidancePriority;

        public CharacterVisualDefinition Visual => visual;
        public CharacterParameters Parameters => parameters;
        public int ExperienceReward => Mathf.Max(1, experienceReward);
        public byte AvoidancePriority => (byte)Mathf.Clamp(avoidancePriority, 0, 1);
    }
}
