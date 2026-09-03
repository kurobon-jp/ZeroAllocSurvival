using System;
using System.Collections.Generic;
using LitheEcs;
using TMPro;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Extensions;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Presentation
{
    public sealed class UpgradePanelPresenter : BasePanelPresenter
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private List<TextButton> buttons;
        [SerializeField, Min(1)] private int maximumCachedLevel = 9999;

        private readonly UpgradeChoice[] _choices = new UpgradeChoice[3];
        private string[] _levelTexts;
        private string[] _weaponUnlockLabels;
        private string[] _weaponLevelLabels;
        private string[] _statLevelLabels;
        private World _world;
        private Entity _levelUpEvent;
        private Action<int> _buttonClickHandler;

        internal void Initialize(World world, WeaponRegistry weapons)
        {
            _world = world;
            _buttonClickHandler = Select;
            levelText.Warmup();

            _weaponUnlockLabels = new string[weapons.Count];
            _weaponLevelLabels = new string[weapons.Count * 6];
            for (var weapon = 0; weapon < weapons.Count; weapon++)
            {
                var displayName = weapons.Definition(weapon).DisplayName;
                _weaponUnlockLabels[weapon] = "Unlock " + displayName;
                for (var level = 1; level <= 5; level++)
                    _weaponLevelLabels[weapon * 6 + level] = displayName + "  Lv" + level;
            }

            _statLevelLabels = new string[5 * 6];
            for (var stat = 0; stat < 5; stat++)
            for (var level = 1; level <= 5; level++)
                _statLevelLabels[stat * 6 + level] = StatName((PlayerStatKind)stat) + "  Lv" + level;
            _levelTexts = new string[maximumCachedLevel + 1];
            for (var level = 0; level < _levelTexts.Length; level++) _levelTexts[level] = "Level " + level;
            levelText.text = _levelTexts[maximumCachedLevel];

            for (var i = 0; i < buttons.Count; i++)
                if (buttons[i] != null)
                {
                    buttons[i].SetClickHandler(_buttonClickHandler, i);
                }

            SetVisible(false);
        }

        internal void Show(in Entity entity, int level, UpgradeChoice first, UpgradeChoice second,
            UpgradeChoice third, int choiceCount)
        {
            _levelUpEvent = entity;
            _choices[0] = first;
            _choices[1] = second;
            _choices[2] = third;
            SetVisible(true);
            for (var i = 0; i < 3; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].SetInteractable(i < choiceCount);
                if (i < choiceCount) buttons[i].SetText(ChoiceLabel(_choices[i]));
            }

            levelText.text = _levelTexts[Mathf.Clamp(level, 0, maximumCachedLevel)];
        }

        private string ChoiceLabel(in UpgradeChoice choice)
        {
            if (choice.Kind == UpgradeKind.UnlockWeapon)
                return _weaponUnlockLabels[choice.Index];
            if (choice.Kind == UpgradeKind.WeaponLevel)
                return _weaponLevelLabels[choice.Index * 6 + choice.NextLevel];
            return _statLevelLabels[choice.Index * 6 + choice.NextLevel];
        }

        private static string StatName(PlayerStatKind kind) => kind switch
        {
            PlayerStatKind.MoveSpeed => "Move Speed",
            PlayerStatKind.WeaponInterval => "Attack Interval",
            PlayerStatKind.MaxHealth => "Max HP",
            PlayerStatKind.AttackPower => "Attack Power",
            PlayerStatKind.ExperienceGain => "Experience Gain",
            _ => "Stat"
        };

        private void Select(int index)
        {
            if (_world == null || !IsVisible || !_levelUpEvent.IsAlive) return;
            _levelUpEvent.Add(new UpgradeSelected { Choice = _choices[index] });
            SetVisible(false);
        }
    }
}
