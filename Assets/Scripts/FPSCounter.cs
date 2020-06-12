using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPSCounter : MonoBehaviour
{
    //-----------------------------------------------------------------
    [SerializeField]
    private Text m_FPSText;
    [SerializeField]
    private Text m_FrameTimeText;
    //-----------------------------------------------------------------
    private void Update()
    {
        m_FPSText.text = $"FPS: {( 1.0f / Time.deltaTime ),5:N0}";
        m_FrameTimeText.text = $"FrameTime(ms): {(Time.deltaTime * 1000.0f),5:N1}";
    }
    //-----------------------------------------------------------------
}
