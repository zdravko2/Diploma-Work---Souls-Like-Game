using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHUDManager : MonoBehaviour
{
    [Header("Settings")]
    public float statusBarScale = 20f;

    [Header("UI Elements")]
    [SerializeField] UIStatusBar healthBar;
    [SerializeField] UIStatusBar staminaBar;
    [SerializeField] UIStatusBar manaBar;

    void Awake()
    {
        healthBar.widthScaleMultiplier = statusBarScale;
        staminaBar.widthScaleMultiplier = statusBarScale;
        manaBar.widthScaleMultiplier = statusBarScale;
    }

    public virtual void RefreshHUD()
    {
        gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    public void SetHealth(float value)
    {
        healthBar.SetValue(value);
    }

    public void SetMaxHealth(float value)
    {
        healthBar.SetMaxValue(value);
    }

    public void SetStamina(float value)
    {
        staminaBar.SetValue(value);
    }

    public void SetMaxStamina(float value)
    {
        staminaBar.SetMaxValue(value);
    }

    public void SetMana(float value)
    {
        manaBar.SetValue(value);
    }

    public void SetMaxMana(float value)
    {
        manaBar.SetMaxValue(value);
    }
}
