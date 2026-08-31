using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameButtons : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string menuSceneName = "GameMenu";

    public void OnHitLeft(InputAction.CallbackContext context)
    {
        if (context.performed)
            Debug.Log("HitLeft pressionado");
    }

    public void OnHitDown(InputAction.CallbackContext context)
    {
        if (context.performed)
            Debug.Log("HitDown pressionado");
    }

    public void OnHitUp(InputAction.CallbackContext context)
    {
        if (context.performed)
            Debug.Log("HitUp pressionado");
    }

    public void OnHitRight(InputAction.CallbackContext context)
    {
        if (context.performed)
            Debug.Log("HitRight pressionado");
    }

    public void RestartGame()
    {
        LoadScene(gameSceneName);
    }

    public void ReturnToMenu()
    {
        LoadScene(menuSceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"A cena '{sceneName}' nao esta disponivel. Adicione-a ao Build Profile.",
                this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
