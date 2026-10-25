using UnityEngine;

public class CharacterUIStatusManager : MonoBehaviour
{
    CharacterManager character;
    [Header("UI Settings")]
    [SerializeField] private bool preserveParentRotation = false;
    [SerializeField] private bool preserveParentScale = false;
    [SerializeField] private Vector3 desiredWorldScale = Vector3.one;
    [SerializeField] private bool hideStatus = false;

    [Header("UI Elements")]
    [SerializeField] UIStatusBar healthBar;
    [SerializeField] UIStatusBar poiseBar;
    [SerializeField] UIEffectsManager effectsManager;
    [SerializeField] GameObject statusPanel;
    [SerializeField] GameObject lockOnIndicator;

    void Start()
    {
        character = GetComponentInParent<CharacterManager>();

        SetMaxHealth(character.statusManager.maxHealth);
        SetHealth(character.statusManager.currentHealth);
        SetMaxPoise(character.statusManager.maxPoise);
        SetPoise(character.statusManager.currentPoise);

        character.statusManager.OnHealthChanged += SetHealth;
        character.statusManager.OnMaxHealthChanged += SetMaxHealth;
        character.statusManager.OnPoiseChanged += SetPoise;
        character.statusManager.OnMaxPoiseChanged += SetMaxPoise;
        character.effectsManager.OnEffectStarted += effectsManager.AddIcon;
        character.effectsManager.OnEffectEnded += effectsManager.RemoveIcon;

        statusPanel.SetActive(false);
        lockOnIndicator.SetActive(false);
    }

    void Update()
    {
        if (!preserveParentRotation) transform.rotation = Quaternion.identity;
        if (!preserveParentScale)
        {
            Vector3 parentScale = transform.parent.lossyScale;

            transform.localScale = new Vector3(
                desiredWorldScale.x / parentScale.x,
                desiredWorldScale.y / parentScale.y,
                desiredWorldScale.z / parentScale.z
            );
        }
    }

    void OnDisable()
    {
        character.statusManager.OnHealthChanged -= SetHealth;
        character.statusManager.OnMaxHealthChanged -= SetMaxHealth;
        character.statusManager.OnPoiseChanged -= SetPoise;
        character.statusManager.OnMaxPoiseChanged -= SetMaxPoise;
        character.effectsManager.OnEffectStarted -= effectsManager.AddIcon;
        character.effectsManager.OnEffectEnded -= effectsManager.RemoveIcon;
    }

    public void SetHealth(float value)
    {
        healthBar.SetValue(value);
    }

    public void SetMaxHealth(float value)
    {
        healthBar.SetMaxValue(value);
    }

    public void SetPoise(float value)
    {
        poiseBar.SetValue(value);
    }

    public void SetMaxPoise(float value)
    {
        poiseBar.SetMaxValue(value);
    }
    
    public void SetLockOnIndicator(bool isActive)
    {
        if (!hideStatus)
        {
            statusPanel.SetActive(isActive);
        }
        lockOnIndicator.SetActive(isActive);
    }
}
