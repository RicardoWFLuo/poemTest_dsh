using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocationVisitedTrigger : MonoBehaviour
{
    [SerializeField] private LocationSO loccationVisited;
    [SerializeField] private bool destoryOnTouch = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            GameManager.Instance.LocationHistoryTracker.RecordLocation(loccationVisited);
            if(destoryOnTouch)
            {
                Destroy(gameObject);
            }
        }
    }
}
