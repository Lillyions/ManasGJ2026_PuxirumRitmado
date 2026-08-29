namespace Dypsloom.RhythmTimeline.Core.Input
{
    using System;
    using UnityEngine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    /// <summary>
    /// A simple abstraction to allow either key or button input.
    /// </summary>
    [Serializable]
    public class SimpleInputActionKey
    {
#if ENABLE_INPUT_SYSTEM
        [Header("New Input System")]
        [SerializeField] private Key m_NewKey;
        [SerializeField] private InputAction m_InputAction;
#endif
        
#if ENABLE_LEGACY_INPUT_MANAGER
        [Header("Old Input Manager")]
        [SerializeField] private KeyCode m_Key;
        [SerializeField] private string m_Button;
#endif

        public SimpleInputActionKey()
        {
            
        }
        
        public SimpleInputActionKey(string button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            m_Button = button;
#endif
        }

        public void Enable()
        {
#if ENABLE_INPUT_SYSTEM
            if(m_InputAction == null){ return; }
            m_InputAction.Enable();
#endif
        }
        
        public void Disable()
        {
#if ENABLE_INPUT_SYSTEM
            if(m_InputAction == null){ return; }
            m_InputAction.Disable();
#endif
        }

        
        public bool GetInputDown()
        {
            var input = false;
            
#if ENABLE_INPUT_SYSTEM
            if (m_NewKey != Key.None) {
                input |=  Keyboard.current[m_NewKey].wasPressedThisFrame;
            }

            if (m_InputAction != null) {
                input |= m_InputAction.WasPerformedThisFrame();
            }
#endif
            
#if ENABLE_LEGACY_INPUT_MANAGER
            if (m_Key != KeyCode.None) {
                input |= Input.GetKeyDown(m_Key);
            }

            if (string.IsNullOrWhiteSpace(m_Button) == false) {
                input |= Input.GetButtonDown(m_Button);
            }
#endif

            return input;
        }
        
        public bool GetInputUp()
        {
            var input = false;
            
#if ENABLE_INPUT_SYSTEM
            if (m_NewKey != Key.None) {
                input |=  Keyboard.current[m_NewKey].wasReleasedThisFrame;
            }

            if (m_InputAction != null) {
                input |= m_InputAction.WasReleasedThisFrame();
            }
#endif
            
#if ENABLE_LEGACY_INPUT_MANAGER
            if (m_Key != KeyCode.None) {
                input |= Input.GetKeyUp(m_Key);
            }

            if (string.IsNullOrWhiteSpace(m_Button) == false) {
                input |= Input.GetButtonUp(m_Button);
            }
#endif
            
            return input;
        }
        
        public bool GetInput()
        {
            var input = false;
            
#if ENABLE_INPUT_SYSTEM
            if (m_NewKey != Key.None) {
                input |=  Keyboard.current[m_NewKey].isPressed;
            }

            if (m_InputAction != null) {
                input |= m_InputAction.IsPressed();
            }
#endif
            
#if ENABLE_LEGACY_INPUT_MANAGER
            if (m_Key != KeyCode.None) {
                input |= Input.GetKey(m_Key);
            }

            if (string.IsNullOrWhiteSpace(m_Button) == false) {
                input |= Input.GetButton(m_Button);
            }
#endif

            return input;
        }
        
        public float GetAxisValue()
        {
            var input = false;
            
#if ENABLE_INPUT_SYSTEM
            if (m_NewKey != Key.None) {
                input |=  Keyboard.current[m_NewKey].isPressed;
            }

            if (m_InputAction != null) {
                return m_InputAction.ReadValue<float>();
            }
#endif
            
#if ENABLE_LEGACY_INPUT_MANAGER
            if (m_Key != KeyCode.None) {
                input |= Input.GetKey(m_Key);
            }

            if (string.IsNullOrWhiteSpace(m_Button) == false) {
                return Input.GetAxisRaw(m_Button);
            }
#endif

            return input? 1: 0f;
        }
    }
}