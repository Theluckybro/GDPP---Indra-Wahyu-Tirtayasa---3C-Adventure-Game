using UnityEngine;
using UnityEngine.InputSystem;
using System;
public class InputManager : MonoBehaviour
{
    public Action<Vector2> OnMoveInput;
    public Action<bool> OnSprintInput;
    public Action OnJumpInput;
    public Action OnClimbInput;
    public Action OnCancelClimb;
    public Action OnChangePOV;
    public Action OnCrouchInput;
    public Action OnGlideInput;
    public Action OnCancelGlide;
    public Action OnPunchInput;
    public Action OnMainMenuInput;
    private void Update()
    {
        CheckMovementInput();
        CheckJumpInput();
        CheckSprintInput();
        CheckCrouchInput();
        CheckChangePOVInput();
        CheckClimbInput();
        CheckGlideInput();
        CheckCancelInput();
        CheckPunchInput();
        CheckMainMenuInput();
    }

    private void CheckMovementInput()
    {
        float verticalAxis = Keyboard.current != null ? (Keyboard.current.wKey.isPressed ? 1f : (Keyboard.current.sKey.isPressed ? -1f : 0f)) : 0f;
        float horizontalAxis = Keyboard.current != null ? (Keyboard.current.dKey.isPressed ? 1f : (Keyboard.current.aKey.isPressed ? -1f : 0f)) : 0f;

        Vector2 inputAxis = new Vector2(horizontalAxis, verticalAxis);

        if (OnMoveInput != null)
        {
            OnMoveInput.Invoke(inputAxis);
        }
    }
    private void CheckJumpInput()
    {
        bool isPressJumpInput = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (isPressJumpInput)
        {
            OnJumpInput();
        }
    }

    private void CheckSprintInput()
    {
        bool isHoldSprintInput = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        if (isHoldSprintInput)
        {
            OnSprintInput(true);
        }
        else
        {
            OnSprintInput(false);
        }
    }

    private void CheckCrouchInput()
    {
        bool isPressCrouchInput = Keyboard.current != null && (Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.rightCtrlKey.wasPressedThisFrame);
        if (isPressCrouchInput)
        {
            OnCrouchInput();
        }
    }

    private void CheckChangePOVInput()
    {
        bool isPressChangePOVInput = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        if (isPressChangePOVInput)
        {
            if (OnChangePOV != null)
            {
                OnChangePOV();
            }
        }
    }

    private void CheckClimbInput()
    {
        bool isPressClimbInput = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        if (isPressClimbInput)
        {
            OnClimbInput();
        }
    }

    private void CheckGlideInput()
    {
        bool isPressGlideInput = Keyboard.current != null && Keyboard.current.gKey.isPressed;
        if (isPressGlideInput)
        {
            if (OnGlideInput != null)
            {
                OnGlideInput();
            }
        }
    }

    private void CheckCancelInput()
    {
        bool isPressCancelInput = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        if (isPressCancelInput)
        {
            if (OnCancelClimb != null)
            {
                OnCancelClimb();
            }
            if (OnCancelGlide != null)
            {
                OnCancelGlide();
            }
        }
    }

    private void CheckPunchInput()
    {
        bool isPressPunchInput = Mouse.current.leftButton.wasPressedThisFrame;
        if (isPressPunchInput)
        {
            OnPunchInput();
        }
    }

    private void CheckMainMenuInput()
    {
        bool isPressMainMenuInput = Keyboard.current.escapeKey.wasPressedThisFrame;
        if (isPressMainMenuInput)
        {
            if (OnMainMenuInput != null)
            {
                OnMainMenuInput();
            }
        }
    }
}
