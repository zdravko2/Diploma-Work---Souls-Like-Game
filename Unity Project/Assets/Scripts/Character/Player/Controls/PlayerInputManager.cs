using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerInputManager : MonoBehaviour
{
    public static PlayerInputManager Instance { get; private set; }
    public PlayerControls playerControls;
    
    [Header("Player Inputs")]
    [field: SerializeField] public Vector2 MoveInput { get; private set; }
    [field: SerializeField] public Vector2 LookInput { get; private set; }
    [field: SerializeField] public bool SprintHeld { get; private set; }
    [field: SerializeField] public bool DodgePressed { get; private set; }
    [field: SerializeField] public bool BlockPressed { get; private set; }
    [field: SerializeField] public bool BlockHeld { get; private set; }
    [field: SerializeField] public bool JumpPressed { get; private set; }
    [field: SerializeField] public bool InteractPressed { get; private set; }
    [field: SerializeField] public bool PrimaryAction { get; private set; }
    [field: SerializeField] public bool SecondaryAction { get; private set; }
    [field: SerializeField] public bool LockOnPressed { get; private set; }
    [field: SerializeField] public bool UseItemPressed { get; private set; }
    [field: SerializeField] public bool CycleItemPressed { get; private set; }
    
    [Header("System Inputs")]
    [field: SerializeField] public bool ToggleMenuPressed { get; private set; }
    [field: SerializeField] public bool ToggleHudPressed { get; private set; }

    [Header("Debug Inputs")]
    [field: SerializeField] public bool DebugAction1 { get; private set; }
    [field: SerializeField] public bool DebugAction2 { get; private set; }
    [field: SerializeField] public bool DebugAction3 { get; private set; }
    [field: SerializeField] public bool DebugAction4 { get; private set; }
    [field: SerializeField] public bool DebugAction5 { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    void OnEnable()
    {
        if (playerControls == null)
        {
            playerControls = new PlayerControls();

            // Movement
            playerControls.Player.Move.performed += i => MoveInput = i.ReadValue<Vector2>();
            playerControls.Player.Look.performed += i => LookInput = i.ReadValue<Vector2>();

            playerControls.Player.Sprint.performed += i => SprintHeld = true;
            playerControls.Player.Sprint.canceled += i => SprintHeld = false;

            playerControls.Player.LockOn.performed += i => LockOnPressed = true;

            // Actions
            playerControls.Player.PrimaryAction.performed += i => PrimaryAction = true;
            playerControls.Player.PrimaryAction.canceled += i => PrimaryAction = false;
            playerControls.Player.SecondaryAction.performed += i => SecondaryAction = true;
            playerControls.Player.SecondaryAction.canceled += i => SecondaryAction = false;

            playerControls.Player.Interact.performed += i => InteractPressed = true;
            playerControls.Player.Dodge.performed += i => DodgePressed = true;
            playerControls.Player.Block.performed += i => BlockPressed = true;
            playerControls.Player.Block.performed += i => BlockHeld = true;
            playerControls.Player.Block.canceled += i => BlockHeld = false;
            playerControls.Player.UseItem.performed += i => UseItemPressed = true;
            playerControls.Player.CycleItem.performed += i => CycleItemPressed = true;

            // System
            playerControls.System.ToggleHUD.performed += i => ToggleHudPressed = true;
            playerControls.System.Menu.performed += i => ToggleMenuPressed = true;

            // Debug
            playerControls.Debug.Action1.performed += i => DebugAction1 = true;
            playerControls.Debug.Action2.performed += i => DebugAction2 = true;
            playerControls.Debug.Action3.performed += i => DebugAction3 = true;
            playerControls.Debug.Action4.performed += i => DebugAction4 = true;
            playerControls.Debug.Action5.performed += i => DebugAction5 = true;

        }

        playerControls.Enable();
    }

    void OnDisable()
    {
        playerControls.Disable();
    }

    void OnApplicationFocus(bool focus)
    {
        if (enabled)
        {
            if (focus)
            {
                playerControls.Enable();
            }
            else
            {
                playerControls.Disable();
            }
        }
    }

    private void LateUpdate()
    {
        // Clear one-frame inputs
        DodgePressed = false;
        JumpPressed = false;
        PrimaryAction = false;
        SecondaryAction = false;
        BlockPressed = false;
        LockOnPressed = false;
        InteractPressed = false;
        UseItemPressed = false;
        CycleItemPressed = false;

        // System
        ToggleHudPressed = false;
        ToggleMenuPressed = false;

        // Debug
        DebugAction1 = false;
        DebugAction2 = false;
        DebugAction3 = false;
        DebugAction4 = false;
        DebugAction5 = false;
    }
}
