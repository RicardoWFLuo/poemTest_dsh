using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerChangeEquipment : MonoBehaviour
{
    public PlayerCombat combat;
    public PlayerBow bow;

    void Update()
    {
        if(MobileInputManager.Instance.GetSwitchDown())
        {
            combat.enabled = !combat.enabled;
            bow.enabled = !bow.enabled;
        }
    }
}
