﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class WarSenseEffect : MonoBehaviour {
    
    private RadarScanRing shaderScript;
    [SerializeField]
    public Color ringColor = new Color(0.8f, 0.4f, 0.4f, 0.7f);

    public void Scan(float timeLength, float scanRadius, int scanPassCount)
    {
        shaderScript = Camera.main.gameObject.AddComponent<RadarScanRing>();
        shaderScript.ringColor = ringColor;
        shaderScript.radius = 0.1f;
        shaderScript.thickness = 1.0f;
        shaderScript.sourceTransform = transform;

        StartCoroutine(ScanCoroutine(timeLength, scanRadius, scanPassCount));
    }

    IEnumerator ScanCoroutine(float timeLength, float scanRadius, int scanPassCount)
    {
        float startTime = Time.time;
        float now = Time.time;
        float timeOnePass = timeLength / scanPassCount;
        float timeCurrentPass = 0.0f;
        while(now - startTime < timeLength)
        {
            timeCurrentPass += Time.time - now;
            if (timeCurrentPass > timeOnePass)
                timeCurrentPass -= timeOnePass;
            shaderScript.radius = Mathf.Lerp(0.1f, scanRadius, timeCurrentPass / timeOnePass);
            now = Time.time;
            yield return new WaitForEndOfFrame();
        }
        EndScan();
    }


    void EndScan()
    {
        Destroy(shaderScript);
        Destroy(this);
    }

}
