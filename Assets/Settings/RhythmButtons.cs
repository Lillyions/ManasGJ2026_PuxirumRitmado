using UnityEngine;
using UnityEngine.InputSystem;

public class RhythmInputDebug : MonoBehaviour
{
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
}