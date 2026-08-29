using UnityEngine;

public class MusicConductor : MonoBehaviour
{
    [SerializeField] private double startDelay = 2.0;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private double bpm = 114.1;
    [SerializeField] private double firstBeatOffset = 0.128;

    public double SongTime { get; private set; }
    public int CurrentBeat { get; private set; } = -1;
    public event System.Action<int> BeatOccurred;

    private double scheduledStartTime;
    private int lastLoggedBeat = -1;

    private void Awake()
    {
        
    }

    private void Start()
    {
        scheduledStartTime = AudioSettings.dspTime + startDelay;
        musicSource.PlayScheduled(scheduledStartTime);
    }

    private void Update()
    {
        SongTime = AudioSettings.dspTime - scheduledStartTime;

        if (SongTime < 0 || !musicSource.isPlaying)
            return;

        double adjustedSongTime = SongTime - firstBeatOffset;

        if (adjustedSongTime < 0)
            return;

        double secondsPerBeat = 60.0 / bpm;
        int currentBeat = (int)(adjustedSongTime / secondsPerBeat);
        CurrentBeat = currentBeat;

        if (currentBeat == lastLoggedBeat)
            return;

        lastLoggedBeat = currentBeat;
        Debug.Log($"Beat {currentBeat + 1}");
        BeatOccurred?.Invoke(currentBeat);
    }
}
