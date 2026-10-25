using UnityEngine;

public class TutorialNPCInteractable : InteractableObject
{
    [SerializeField] TutorialNPCController tutorialNPC;
    bool tutorialStarted;

    protected override void Awake()
    {
        base.Awake();
        if (tutorialNPC == null) tutorialNPC = GetComponentInParent<TutorialNPCController>();
    }

    public override void Interact(PlayerManager player)
    {
        base.Interact(player);
        if (tutorialNPC == null) return;

        if (tutorialNPC.IsWaitingForDialogueInput)
        {
            tutorialNPC.AdvanceDialogue(player);
            return;
        }

        if (tutorialStarted || tutorialNPC.IsTutorialRunning) return;

        tutorialStarted = true;
        tutorialNPC.BeginTutorial(player, gameObject);
    }
}
