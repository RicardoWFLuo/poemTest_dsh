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

    /// <summary>已交互过的角色（存档用）。</summary>
    public IReadOnlyCollection<ActorSO> GetSpokenActors()
    {
        return spokenNPCs;
    }

    /// <summary>用存档数据整体替换（读档用）。</summary>
    public void Restore(IEnumerable<ActorSO> actors)
    {
        spokenNPCs.Clear();

        if (actors == null)
            return;

        foreach (var actor in actors)
        {
            if (actor != null)
                spokenNPCs.Add(actor);
        }
    }
}
