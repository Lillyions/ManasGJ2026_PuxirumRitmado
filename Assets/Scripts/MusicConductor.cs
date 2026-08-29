using UnityEngine;

//[RequireComponent(typeof(AudioSource))]
public class MusicConductor : MonoBehaviour
{
    [SerializeField] private double startDelay = 2.0;
    [SerializeField] private AudioSource musicSource;

    public double SongTime { get; private set; }

    //private AudioSource musicSource;
    private double scheduledStartTime;
    private int lastLoggedSecond = -1;

    private void Awake()
    {
        //musicSource = GetComponent<AudioSource>();
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

        int currentSecond = (int)SongTime;

        if (currentSecond == lastLoggedSecond)
            return;

        lastLoggedSecond = currentSecond;
        Debug.Log($"Tempo da música: {currentSecond}s");
    }
}
