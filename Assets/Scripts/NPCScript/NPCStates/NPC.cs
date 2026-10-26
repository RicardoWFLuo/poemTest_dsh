using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : MonoBehaviour
{
    public enum NPCState
    {
        Default,
        Idle,
        Patrol,
        Wander,
        Talk
    }
    public NPCState currentState = NPCState.Patrol;
    private NPCState defaultState;


    public NPCPatrol patrol;
    public NPCWander wander;
    public NPCTalk talk;

    void Start()
    {
        defaultState = currentState;
        SwitchState(currentState);
    }

    public void SwitchState(NPCState newState)
    {
        currentState = newState;

        if (patrol != null) patrol.enabled = newState == NPCState.Patrol;
        if (wander != null) wander.enabled = newState == NPCState.Wander;
        if (talk != null) talk.enabled = newState == NPCState.Talk;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SwitchState(NPCState.Talk);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SwitchState(defaultState);
        }
    }
}
