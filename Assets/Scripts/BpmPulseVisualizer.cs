using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BpmPulseVisualizer : MonoBehaviour
{
    [SerializeField] private MusicConductor conductor;
    [SerializeField] private Image targetImage;
    [SerializeField] private Color beatColor = Color.red;
    [SerializeField, Min(0f)] private float flashDuration = 0.12f;

    private Color baseColor;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        baseColor = targetImage.color;
    }

    private void OnEnable()
    {
        conductor.BeatOccurred += HandleBeat;
    }

    private void HandleBeat(int beat)
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(Pulse());
    }

    private IEnumerator Pulse()
    {
        targetImage.color = beatColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        targetImage.color = baseColor;
        pulseRoutine = null;
    }

    private void OnDisable()
    {
        conductor.BeatOccurred -= HandleBeat;

        if (targetImage != null)
            targetImage.color = baseColor;
    }
}
