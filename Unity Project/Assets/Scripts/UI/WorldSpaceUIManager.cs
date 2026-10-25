using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class WorldSpaceUIManager : MonoBehaviour
{
    [SerializeField] bool faceCamera = true;
    [SerializeField] bool scaleWithDistance = true;
    [SerializeField] float scaleMultiplier = 1f;

    void Awake()
    {
        transform.localScale = Vector3.one * scaleMultiplier;
    }

    void Update()
    {
        if (Camera.main == null) return;
        
        if (faceCamera)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        if (scaleWithDistance)
        {
            transform.localScale = Vector3.one * Vector3.Distance(transform.position, Camera.main.transform.position) * scaleMultiplier;
        }
    }
}
