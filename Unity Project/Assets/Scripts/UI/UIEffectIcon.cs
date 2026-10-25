using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIEffectIcon : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image fillImage;

    private TimedCharacterEffect _timedEffect;

    public void Initialize(CharacterEffect effect)
    {
        iconImage.sprite = effect.icon;

        if (effect is TimedCharacterEffect timedEffect)
        {
            _timedEffect = timedEffect;
            fillImage.gameObject.SetActive(true);
            fillImage.rectTransform.localScale = Vector3.one;
        }
        else
        {
            fillImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (_timedEffect != null)
        {
            float remainingRatio = Mathf.Clamp01(_timedEffect.remainingDuration / _timedEffect.duration);
            float value = 1f - remainingRatio;

            var parentRect = fillImage.rectTransform.parent as RectTransform;
            float targetHeight = parentRect.rect.height * value;

            fillImage.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                targetHeight
            );

        }
    }
}
