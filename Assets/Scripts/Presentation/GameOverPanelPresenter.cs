using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZeroAllocSurvival.Presentation
{
    public sealed class GameOverPanelPresenter : BasePanelPresenter
    {
        private static readonly Action<int> RetryHandler = OnRetryClicked;
        [SerializeField] private TextButton retryButton;

        private void Start()
        {
            retryButton.SetClickHandler(RetryHandler);
            SetVisible(false);
        }

        private static void OnRetryClicked(int _) => ReloadScene();
        private static void ReloadScene() => SceneManager.LoadScene(0);
    }
}