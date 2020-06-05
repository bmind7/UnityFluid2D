using UnityEngine;

public class Menu : MonoBehaviour
{
    //-----------------------------------------------------------------
    public GameObject[] m_Simulations;
    //-----------------------------------------------------------------
    public void EnableSimulation( GameObject go )
    {
        foreach( GameObject simGO in m_Simulations)
        {
            // Only enable game object wich was passed to the function
            simGO.SetActive( simGO == go );
        }
    }
    //-----------------------------------------------------------------
}
