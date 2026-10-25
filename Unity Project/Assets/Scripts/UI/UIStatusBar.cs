using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIStatusBar : MonoBehaviour
{
    private Slider slider;
    private RectTransform rectTransform;

    [Header("Bar Options")]
    public bool scaleBarLengthWithStats = true;
    public float widthScaleMultiplier = 1f;
    protected virtual void Awake()
    {
        slider = GetComponent<Slider>();
        rectTransform = GetComponent<RectTransform>();
    }

    public virtual void SetValue(float value)
    {
        slider.value = value;
    }

    public virtual void SetMaxValue(float maxValue)
    {
        slider.maxValue = maxValue;

        if (scaleBarLengthWithStats)
        {
            rectTransform.sizeDelta = new Vector2(maxValue * widthScaleMultiplier, rectTransform.sizeDelta.y);
            gameObject.SetActive(false);
            gameObject.SetActive(true);
        }
    }
}