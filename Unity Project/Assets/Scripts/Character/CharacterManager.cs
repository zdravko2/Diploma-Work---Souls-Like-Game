using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

[DisallowMultipleComponent]
public class CharacterManager : MonoBehaviour
{
    [Header("Managers")]
    [HideInInspector] public CharacterController characterController;
    [HideInInspector] public Rigidbody characterRigidbody;
    public CharacterLocomotionManager locomotionManager { get; private set; }
    public CharacterAnimatorManager animatorManager { get; private set; }
    public CharacterEffectsManager effectsManager { get; private set; }
    public CharacterSoundFXManager soundFXManager { get; private set; }
    public CharacterVisualFXManager visualFXManager { get; private set; }
    public CharacterStatusManager statusManager { get; private set; }
    public CharacterControlsManager controller { get; private set; }
    public CharacterInventoryManager inventoryManager { get; private set; }
    public CharacterEquipmentManager equipmentManager { get; private set; }
    public CharacterCombatManager combatManager { get; private set; }
    public CharacterGroup characterGroup = CharacterGroup.Neutral;


    protected virtual void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterRigidbody = GetComponent<Rigidbody>();
        locomotionManager = GetComponent<CharacterLocomotionManager>();
        animatorManager = GetComponent<CharacterAnimatorManager>();
        effectsManager = GetComponent<CharacterEffectsManager>();
        soundFXManager = GetComponent<CharacterSoundFXManager>();
        visualFXManager = GetComponent<CharacterVisualFXManager>();
        statusManager = GetComponent<CharacterStatusManager>();
        controller = GetComponent<CharacterControlsManager>();
        inventoryManager = GetComponent<CharacterInventoryManager>();
        equipmentManager = GetComponent<CharacterEquipmentManager>();
        combatManager = GetComponent<CharacterCombatManager>();

        characterRigidbody.useGravity = false;
        characterRigidbody.isKinematic = true;
    }

    void OnEnable()
    {
        statusManager.OnCharacterDeath += PlayCharacterDeath;
    }

    void OnDisable()
    {
        statusManager.OnCharacterDeath -= PlayCharacterDeath;
    }

    protected virtual void Start()
    {

    }

    protected virtual void Update()
    {

    }

    public virtual void LoadCharacterData(ref CharacterSaveData saveData)
    {
        //transform.position = new Vector3(saveData.xPosition, saveData.yPosition, saveData.zPosition);
        //transform.rotation = Quaternion.Euler(saveData.xRotation, saveData.yRotation, saveData.zRotation);
    }

    protected virtual void PlayCharacterDeath()
    {
        StartCoroutine(DestroyCharacter());
    }

    protected virtual IEnumerator DestroyCharacter()
    {
        animatorManager.UpdateAnimatorStatusParameters(statusManager.isDead);
        animatorManager.UpdateAnimatorMovementParameters(0f, 0f, 0f);
        combatManager.SetLockOnTarget(null);
        combatManager.canBeLockedOn = false;
        
        if (!combatManager.isExposed)
            animatorManager.PlayTargetAnimation(animatorManager.animations.HitDeath, crossFadeDuration: 0f, canBeInterrupted: false, forceInterruption: true);

        const float waitTime = 3f;
        yield return new WaitForSecondsRealtime(waitTime);

        characterController.enabled = false;

        const float dissolveDuration = 2f;
        StartCoroutine(DissolveEffect(dissolveDuration));
        yield return new WaitForSecondsRealtime(dissolveDuration);
        
        Destroy(gameObject);
    }


    IEnumerator DissolveEffect(float dissolveDuration)
    {
        Material[] materials = GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(renderer => renderer.materials).ToArray();

        float dissolveAmount = 0f;
        while (dissolveAmount < 1f)
        {
            dissolveAmount += Time.deltaTime * (1f / dissolveDuration);
            foreach (Material material in materials)
            {
                if (material.shader.name == "Shader Graphs/ToonShader")
                    material.SetFloat("_Dissolve_Amount", dissolveAmount);
            }
            yield return null;
        }
    }
}
