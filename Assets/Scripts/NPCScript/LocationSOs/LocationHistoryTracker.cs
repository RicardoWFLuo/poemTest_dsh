using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocationHistoryTracker : MonoBehaviour
{
    private readonly HashSet<LocationSO> locationsVisited = new HashSet<LocationSO>();


    public void RecordLocation(LocationSO locationSO)
    {
        if (locationSO == null)
            return;

        if (locationsVisited.Add(locationSO))
        {
            Debug.Log("Just visited " + locationSO.displayName);
        }
    }

    public bool HasVisited(LocationSO locationSO)
    {
        if (locationSO == null)
            return false;

        return locationsVisited.Contains(locationSO);
    }

    /// <summary>已到访的地点（存档用）。</summary>
    public IReadOnlyCollection<LocationSO> GetVisitedLocations()
    {
        return locationsVisited;
    }

    /// <summary>用存档数据整体替换（读档用）。</summary>
    public void Restore(IEnumerable<LocationSO> locations)
    {
        locationsVisited.Clear();

        if (locations == null)
            return;

        foreach (var location in locations)
        {
            if (location != null)
                locationsVisited.Add(location);
        }
    }
}
