using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIPopUpManager : MonoBehaviour
{
    public bool popUpWindowOpen = false;
    [Header("Pop-Up Message")]
    [SerializeField] GameObject popUpMessageGameObject;
    [SerializeField] TextMeshProUGUI popUpText;

    [Header("Death Message")]
    [SerializeField] GameObject deathMessageGameObject;
    [SerializeField] TextMeshProUGUI deathMessageText;
    
    [Header("Victory Message")]
    [SerializeField] GameObject victoryMessageGameObject;
    [SerializeField] TextMeshProUGUI victoryMessageText;

    [Header("Fade Black Screen")]
    [SerializeField] GameObject fadeBlackScreenGameObject;
    [SerializeField] Image fadeBlackScreen;

    void Awake()
    {
        deathMessageGameObject.SetActive(false);
        victoryMessageGameObject.SetActive(false);
        popUpMessageGameObject.SetActive(false);
        fadeBlackScreenGameObject.SetActive(false);
    }

    public void ShowDeathMessage(string messageText)
    {
        popUpWindowOpen = true;
        deathMessageText.text = messageText;
        deathMessageGameObject.SetActive(true);
    }
    
    public void CloseDeathMessage()
    {
        popUpWindowOpen = false;
        deathMessageGameObject.SetActive(false);
    }
    
    public void ShowPopUpMessage(string messageText)
    {
        popUpWindowOpen = true;
        popUpText.text = messageText;
        popUpMessageGameObject.SetActive(true);
    }
    
    public void ClosePopUpMessage()
    {
        popUpWindowOpen = false;
        popUpMessageGameObject.SetActive(false);
    }

    public void ShowVictoryMessage(string messageText)
    {
        popUpWindowOpen = true;
        victoryMessageText.text = messageText;
        victoryMessageGameObject.SetActive(true);
    }

    public void CloseVictoryMessage()
    {
        popUpWindowOpen = false;
        victoryMessageGameObject.SetActive(false);
    }

    public void ShowDialogueMessage(string messageText)
    {
        popUpWindowOpen = true;
        popUpText.text = messageText;
        popUpMessageGameObject.SetActive(true);
    }
    
    public void CloseDialogueMessage()
    {
        popUpWindowOpen = false;
        popUpMessageGameObject.SetActive(false);
    }

    private IEnumerator PopUpMessageCoroutine(string messageText, float duration)
    {
        popUpWindowOpen = true;
        this.popUpText.text = messageText;
        this.popUpText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        this.popUpText.gameObject.SetActive(false);
        popUpWindowOpen = false;
    }

    public IEnumerator FadeScreenForDuration(bool toBlack, float duration)
    {
        popUpWindowOpen = true;
        fadeBlackScreenGameObject.SetActive(true);

        float startAlpha = toBlack ? 0f : 1f;
        float endAlpha = toBlack ? 1f : 0f;

        float elapsed = 0f;

        Color color = fadeBlackScreen.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            fadeBlackScreen.color = new Color(color.r, color.g, color.b, alpha);

            yield return null;
        }

        fadeBlackScreen.color = new Color(color.r, color.g, color.b, endAlpha);

        if (!toBlack)
        {
            fadeBlackScreenGameObject.SetActive(false);
            popUpWindowOpen = false;
        }
    }
}
