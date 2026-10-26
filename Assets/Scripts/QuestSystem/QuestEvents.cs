using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class QuestEvents
{
    public static Action<QuestSO> OnQuestOfferRequested;
    public static Action<QuestSO> OnQuestTurnInRequested;
    public static Action<QuestSO> OnQuestAccepted;
    public static Func<QuestSO, bool> IsQuestComplete;
}
