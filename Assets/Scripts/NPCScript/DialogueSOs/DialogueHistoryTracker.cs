using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueHistoryTracker : MonoBehaviour
{
    private readonly HashSet<ActorSO> spokenNPCs = new HashSet<ActorSO>();


    public void RecordNPC(ActorSO actorSO)
    {
        if (actorSO == null)
            return;

        if (spokenNPCs.Add(actorSO))
        {
            Debug.Log("Just spoke to " + actorSO.actorName);
        }
    }

    public bool HasSpokenWith(ActorSO actorSO)
    {
        if (actorSO == null)
            return false;

        return spokenNPCs.Contains(actorSO);
    }
}
