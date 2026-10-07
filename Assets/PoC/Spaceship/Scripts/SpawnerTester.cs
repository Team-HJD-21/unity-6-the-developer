using TeamHJD.Game.Domain;
using UnityEngine;

public class SpawnerTester : MonoBehaviour
{
    EncounterRuntime encounterRuntime;
    void Awake()
    {
        encounterRuntime = new EncounterRuntime();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;
        
        encounterRuntime.Spawn();
    }
}
