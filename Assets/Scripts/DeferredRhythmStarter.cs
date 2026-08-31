using System.Collections;
using Dypsloom.RhythmTimeline.Core;
using Dypsloom.RhythmTimeline.Core.Managers;
using UnityEngine;

public sealed class DeferredRhythmStarter : MonoBehaviour
{
    [SerializeField] private RhythmDirector rhythmDirector;
    [SerializeField, Min(0)] private int stabilizationFrames = 3;
    [SerializeField, Min(0f)] private float startupDelaySeconds = 0.25f;

    private IEnumerator Start()
    {
        if (rhythmDirector == null)
        {
            Debug.LogError("Deferred rhythm start requires a RhythmDirector reference.", this);
            yield break;
        }

        RhythmTimelineAsset song = rhythmDirector.SongTimelineAsset;
        if (song == null)
        {
            Debug.LogError("Deferred rhythm start could not find a song Timeline.", this);
            yield break;
        }

        AudioClip audioClip = song.AudioClip;
        if (audioClip != null)
        {
            if (audioClip.loadState == AudioDataLoadState.Unloaded && !audioClip.LoadAudioData())
            {
                Debug.LogError($"Could not begin loading audio clip '{audioClip.name}'.", this);
                yield break;
            }

            while (audioClip.loadState == AudioDataLoadState.Loading)
                yield return null;

            if (audioClip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogError(
                    $"Audio clip '{audioClip.name}' was not loaded. State: {audioClip.loadState}.",
                    this);
                yield break;
            }
        }

        for (int i = 0; i < stabilizationFrames; i++)
            yield return null;

        if (startupDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startupDelaySeconds);

        if (rhythmDirector.IsPlaying)
            yield break;

        AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
        Debug.Log(
            $"Starting rhythm after scene stabilization. " +
            $"ClipState={audioClip?.loadState.ToString() ?? "None"}, " +
            $"DSPTime={AudioSettings.dspTime:F6}, DSPBuffer={bufferLength}x{numBuffers}.",
            this);

        rhythmDirector.PlaySong(song);
    }
}
