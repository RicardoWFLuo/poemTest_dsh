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
}
