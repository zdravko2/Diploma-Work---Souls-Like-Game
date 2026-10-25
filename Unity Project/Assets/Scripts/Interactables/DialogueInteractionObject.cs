using UnityEngine;

public class DialogueInteractable : InteractableObject
{
    [TextArea(2, 5)]
    public string[] dialogueLines;

    private int currentLine = 0;
    private bool isTalking = false;

    public override void Interact(PlayerManager player)
    {
        var ui = PlayerUIManager.Instance.popUpManager;

        if (!isTalking)
        {
            isTalking = true;
            currentLine = 0;
            ui.ShowDialogueMessage(dialogueLines[currentLine]);
            if (player.controller is PlayerController controller)
            {
                controller.dialogueObject = gameObject;
            }
        }
        else
        {
            currentLine++;

            if (currentLine >= dialogueLines.Length)
            {
                isTalking = false;
                currentLine = 0;
                ui.CloseDialogueMessage();

                if (player.controller is PlayerController controller)
                {
                    controller.dialogueObject = null;
                }
            }
            else
            {
                ui.ShowDialogueMessage(dialogueLines[currentLine]);
            }
        }
    }
}