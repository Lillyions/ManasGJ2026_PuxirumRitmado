using Dypsloom.RhythmTimeline.Core;
using Dypsloom.RhythmTimeline.Core.Notes;
using Dypsloom.RhythmTimeline.Effects;
using Dypsloom.Shared.Utility;
using UnityEngine;

public class ColoredEffectSpawner : PooledObjectSpawner
{
    [SerializeField] 
    private Note _noteScript;

    public void ColoredSpawning()
    {
        GameObject spawnedGameObject;
        if (m_SpawnPosition != null)
        {
            Vector3 higherPos = new Vector3(m_SpawnPosition.position.x, m_SpawnPosition.position.y + 0.1f, m_SpawnPosition.position.z);
            spawnedGameObject = PoolManager.Instantiate(m_Prefab, higherPos, m_SpawnPosition.rotation, m_Parent);
        }
        else
        {
            spawnedGameObject = PoolManager.Instantiate(m_Prefab, m_Parent);
        }

        if (_noteScript != null)
        {
            TrackObject currentTrack = _noteScript.RhythmClipData.TrackObject;
            if (spawnedGameObject.TryGetComponent<SetCustomGroupPArticles>(out SetCustomGroupPArticles particles))
            {
                particles.SetCustomParticles(currentTrack.PrimaryColor, currentTrack.SecondaryColor);
            }
        }

        if (m_ScheduledDestroy > 0)
        {
            SchedulerManager.Schedule(() =>
            {
                PoolManager.Destroy(spawnedGameObject);
            }, m_ScheduledDestroy);
        }
    }
}
