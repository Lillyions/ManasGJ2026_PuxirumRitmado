using Dypsloom.RhythmTimeline.Core.Managers;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameResultController : MonoBehaviour
{
    [SerializeField] private RhythmDirector rhythmDirector;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private TextMeshProUGUI hudScoreText;
    [SerializeField] private TextMeshProUGUI finalScoreText;

    private bool resultShown;

    private void Awake()
    {
        SetVictoryVisibility(false);
    }

    private void OnEnable()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        rhythmDirector.OnSongPlay += HandleSongStarted;
        rhythmDirector.OnSongEnd += HandleSongEnded;
    }

    private void OnDisable()
    {
        if (rhythmDirector == null)
        {
            return;
        }

        rhythmDirector.OnSongPlay -= HandleSongStarted;
        rhythmDirector.OnSongEnd -= HandleSongEnded;
    }

    private void HandleSongStarted()
    {
        resultShown = false;
        SetVictoryVisibility(false);
    }

    private void HandleSongEnded()
    {
        ShowVictory();
    }

    public void ShowVictory()
    {
        if (resultShown)
        {
            return;
        }

        resultShown = true;
        UpdateFinalScore();
        SetVictoryVisibility(true);
    }

    private void UpdateFinalScore()
    {
        if (hudScoreText == null || finalScoreText == null)
        {
            Debug.LogError("Os textos de pontuacao da HUD e da tela final precisam ser atribuidos no Inspector.", this);
            return;
        }

        finalScoreText.text = hudScoreText.text;
    }

    private void SetVictoryVisibility(bool isVisible)
    {
        if (victoryScreen == null)
        {
            if (isVisible)
            {
                Debug.LogError("A tela de vitoria nao foi atribuida no Inspector.", this);
            }

            return;
        }

        victoryScreen.SetActive(isVisible);
    }

    private bool HasRequiredReferences()
    {
        if (rhythmDirector == null)
        {
            Debug.LogError("O RhythmDirector nao foi atribuido no Inspector.", this);
            return false;
        }

        if (victoryScreen == null)
        {
            Debug.LogError("A tela de vitoria nao foi atribuida no Inspector.", this);
            return false;
        }

        return true;
    }
}
