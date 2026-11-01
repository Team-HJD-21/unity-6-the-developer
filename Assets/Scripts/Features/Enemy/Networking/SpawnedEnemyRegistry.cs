// 서버/Client에서 NGO spawn이 끝난 적의 등록 사실을 Feature 바인딩에 알립니다.
// 생성·제거·시간을 소유하지 않으며 소비자는 자신의 NetworkManager/Scene으로 범위를 제한합니다.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpawnedEnemyRegistry
{
    private static readonly HashSet<EnemyNetworkController> Actors = new();
    public static event Action<EnemyNetworkController> Registered, Unregistered;
    public static EnemyNetworkController[] CaptureActors()
    { var actors = new EnemyNetworkController[Actors.Count]; Actors.CopyTo(actors); return actors; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Actors.Clear(); Registered = null; Unregistered = null; }
    public static void Register(EnemyNetworkController actor) { if (actor != null && Actors.Add(actor)) Notify(Registered, actor); }
    public static void Unregister(EnemyNetworkController actor) { if (Actors.Remove(actor)) Notify(Unregistered, actor); }
    private static void Notify(Action<EnemyNetworkController> observers, EnemyNetworkController actor)
    {
        if (observers == null) return;
        foreach (Action<EnemyNetworkController> observer in observers.GetInvocationList())
            try { observer(actor); } catch (Exception error) { Debug.LogException(error, actor); }
    }
}
