using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameMenuController : MonoBehaviour
{
    [SerializeField] private GameObject creditsScreen;
    [SerializeField] private string gameSceneName = "GameScene";

    private void Start()
    {
        SetCreditsVisibility(false);
    }

    private void Update()
    {
        if (creditsScreen == null || !creditsScreen.activeSelf || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HideCredits();
        }
    }

    public void StartGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                $"A cena '{gameSceneName}' nao esta disponivel. Adicione-a ao Build Profile.",
                this);
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void ShowCredits()
    {
        SetCreditsVisibility(true);
    }

    public void HideCredits()
    {
        SetCreditsVisibility(false);
    }

    private void SetCreditsVisibility(bool isVisible)
    {
        if (creditsScreen == null)
        {
            Debug.LogError("A tela de creditos nao foi atribuida no Inspector.", this);
            return;
        }

        creditsScreen.SetActive(isVisible);
    }
}
