using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPSCounter : MonoBehaviour
{
    //-----------------------------------------------------------------
    [SerializeField]
    private Text m_FPSText;
    //-----------------------------------------------------------------
    private void Update()
    {
        int fps = (int) (1.0f / Time.deltaTime);
        m_FPSText.text = $"FPS: { fps }";
    }
    //-----------------------------------------------------------------
}
