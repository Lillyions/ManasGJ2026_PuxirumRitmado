using System;
using System.Collections.Generic;
using System.Linq;
using Dypsloom.RhythmTimeline.Core.Managers;
using Dypsloom.RhythmTimeline.Core.Playables;
using UnityEngine;
using UnityEngine.Timeline;

namespace PuxirumRitmado.AnimationProgression
{
    [Serializable]
    public sealed class AnimationStageBinding
    {
        [SerializeField] private string stageId;
        [SerializeField] private GameObject root;
        [SerializeField] private string beatStateName;
        [SerializeField] private AnimationClip beatClip;

        [NonSerialized] private Animator animator;
        [NonSerialized] private int beatStateHash;

        public string StageId => stageId;
        public GameObject Root => root;
        public string BeatStateName => beatStateName;
        public AnimationClip BeatClip => beatClip;
        public Animator Animator => animator;
        public int BeatStateHash => beatStateHash;

        public void CacheRuntimeData()
        {
            animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
            beatStateHash = string.IsNullOrWhiteSpace(beatStateName)
                ? 0
                : Animator.StringToHash(beatStateName);
        }
    }

    public sealed class AnimationSequenceController : MonoBehaviour
    {
        private const double TimeEpsilon = 0.0001d;
        private const double NaturalEndDetectionTolerance = 0.25d;

        [Header("Timeline")]
        [SerializeField] private RhythmDirector rhythmDirector;
        [SerializeField] private AnimationSequenceSO sequence;

        [Header("Scene")]
        [SerializeField] private GameObject alwaysVisibleRoot;
        [SerializeField] private List<AnimationStageBinding> stages = new();

        private readonly Dictionary<string, AnimationStageBinding> bindings =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> revealedStageIds = new(StringComparer.Ordinal);
        private readonly List<RuntimeCue> runtimeCues = new();

        private int nextCueIndex;
        private double firstBeatTime;
        private double secondsPerBeat;
        private double songDuration;
        private double lastObservedTimelineTime;
        private bool sequenceReady;
        private bool naturalEndingApplied;

        public AnimationSequenceSO Sequence => sequence;
        public double FirstBeatTime => firstBeatTime;
        public bool IsReady => sequenceReady;

        private readonly struct RuntimeCue
        {
            public RuntimeCue(AnimationRevealCue cue, int sourceIndex, double time)
            {
                Cue = cue;
                SourceIndex = sourceIndex;
                Time = time;
            }

            public AnimationRevealCue Cue { get; }
            public int SourceIndex { get; }
            public double Time { get; }
        }

        private void Awake()
        {
            BuildBindingCache();
            ResetVisualState();
        }

        private void OnEnable()
        {
            if (rhythmDirector == null)
                return;

            rhythmDirector.OnSongPlay += HandleSongPlay;
            rhythmDirector.OnSongEnd += HandleSongEnd;
        }

        private void Start()
        {
            if (rhythmDirector != null && rhythmDirector.IsPlaying)
                HandleSongPlay();
        }

        private void OnDisable()
        {
            if (rhythmDirector == null)
                return;

            rhythmDirector.OnSongPlay -= HandleSongPlay;
            rhythmDirector.OnSongEnd -= HandleSongEnd;
        }

        private void Update()
        {
            if (!sequenceReady || rhythmDirector == null || !rhythmDirector.IsPlaying)
                return;

            double currentTime = rhythmDirector.PlayableDirector.time;

            if (currentTime + TimeEpsilon < lastObservedTimelineTime)
            {
                ResetVisualState();
                nextCueIndex = 0;
            }

            ProcessCuesThrough(currentTime);
            lastObservedTimelineTime = currentTime;

            if (!naturalEndingApplied && songDuration > 0d && currentTime >= songDuration - TimeEpsilon)
                ApplyNaturalEnding();
        }

        public bool Reveal(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
            {
                Debug.LogError("Cannot reveal an animation stage with an empty ID.", this);
                return false;
            }

            if (!bindings.TryGetValue(stageId, out AnimationStageBinding binding))
            {
                Debug.LogError($"Animation stage '{stageId}' has no scene binding.", this);
                return false;
            }

            if (revealedStageIds.Contains(stageId))
                return true;

            double timelineTime = rhythmDirector != null
                ? rhythmDirector.PlayableDirector.time
                : firstBeatTime;
            if (!StartBeatLoop(binding, timelineTime))
                return false;

            revealedStageIds.Add(stageId);
            return true;
        }

        public bool ResumeBeatLoop(string stageId)
        {
            if (!bindings.TryGetValue(stageId, out AnimationStageBinding binding))
            {
                Debug.LogError($"Animation stage '{stageId}' has no scene binding.", this);
                return false;
            }

            if (binding.Root == null || !binding.Root.activeSelf)
                return false;

            double timelineTime = rhythmDirector != null
                ? rhythmDirector.PlayableDirector.time
                : firstBeatTime;
            return StartBeatLoop(binding, timelineTime);
        }

        public static double CalculateCueTime(
            double firstBeat,
            double bpm,
            int beatsPerBar,
            int bar,
            int beat)
        {
            if (bpm <= 0d)
                throw new ArgumentOutOfRangeException(nameof(bpm), "BPM must be greater than zero.");
            if (beatsPerBar < 1)
                throw new ArgumentOutOfRangeException(nameof(beatsPerBar));
            if (bar < 1)
                throw new ArgumentOutOfRangeException(nameof(bar));
            if (beat < 1 || beat > beatsPerBar)
                throw new ArgumentOutOfRangeException(nameof(beat));

            long zeroBasedBeat = ((long)bar - 1L) * beatsPerBar + beat - 1L;
            return firstBeat + zeroBasedBeat * (60d / bpm);
        }

        [ContextMenu("Validate Configuration")]
        public bool ValidateConfiguration()
        {
            BuildBindingCache();
            return ValidateAndLogConfiguration();
        }

        private void HandleSongPlay()
        {
            BuildBindingCache();
            ResetVisualState();
            runtimeCues.Clear();
            nextCueIndex = 0;
            lastObservedTimelineTime = 0d;
            songDuration = rhythmDirector.PlayableDirector.duration;
            naturalEndingApplied = false;
            sequenceReady = false;

            if (!ValidateAndLogConfiguration())
                return;

            if (!TryGetFirstOnBeatMarkerTime(out firstBeatTime))
            {
                Debug.LogError(
                    "Animation progression requires at least one TempoMarker with ID 0 in the song Timeline.",
                    this);
                return;
            }

            secondsPerBeat = 60d / rhythmDirector.Bpm;

            runtimeCues.AddRange(sequence.Cues
                .Select((cue, index) => new RuntimeCue(
                    cue,
                    index,
                    CalculateCueTime(
                        firstBeatTime,
                        rhythmDirector.Bpm,
                        sequence.BeatsPerBar,
                        cue.Bar,
                        cue.Beat)))
                .OrderBy(item => item.Time)
                .ThenBy(item => item.SourceIndex));

            sequenceReady = true;
            ProcessCuesThrough(rhythmDirector.PlayableDirector.time);
        }

        private void HandleSongEnd()
        {
            sequenceReady = false;

            bool reachedNaturalEnd = naturalEndingApplied ||
                                     (songDuration > 0d &&
                                      lastObservedTimelineTime >= songDuration - NaturalEndDetectionTolerance);

            if (reachedNaturalEnd)
                ApplyNaturalEnding();
            else
                FreezeRevealedStages();
        }

        private void ProcessCuesThrough(double timelineTime)
        {
            while (nextCueIndex < runtimeCues.Count &&
                   runtimeCues[nextCueIndex].Time <= timelineTime + TimeEpsilon)
            {
                Reveal(runtimeCues[nextCueIndex].Cue.StageId);
                nextCueIndex++;
            }
        }

        private bool StartBeatLoop(AnimationStageBinding binding, double timelineTime)
        {
            if (binding.Root == null)
                return false;

            if (secondsPerBeat <= 0d && rhythmDirector != null && rhythmDirector.Bpm > 0f)
            {
                secondsPerBeat = 60d / rhythmDirector.Bpm;
                TryGetFirstOnBeatMarkerTime(out firstBeatTime);
            }

            if (secondsPerBeat <= 0d)
            {
                Debug.LogError("Cannot start a beat loop before a valid BPM is available.", this);
                return false;
            }

            binding.Root.SetActive(true);

            Animator animator = binding.Animator;
            if (animator == null || binding.BeatClip == null || binding.BeatStateHash == 0)
                return false;

            if (!animator.HasState(0, binding.BeatStateHash))
            {
                Debug.LogError(
                    $"Animator for stage '{binding.StageId}' has no state '{binding.BeatStateName}' on layer 0.",
                    binding.Root);
                return false;
            }

            double beatPosition = Math.Max(0d, (timelineTime - firstBeatTime) / secondsPerBeat);
            float beatPhase = (float)(beatPosition - Math.Floor(beatPosition));

            animator.speed = (float)(binding.BeatClip.length / secondsPerBeat);
            animator.Play(binding.BeatStateHash, 0, beatPhase);
            animator.Update(0f);
            return true;
        }

        private void ApplyNaturalEnding()
        {
            if (!naturalEndingApplied)
            {
                foreach (AnimationStageBinding binding in stages)
                {
                    if (binding == null || string.IsNullOrWhiteSpace(binding.StageId))
                        continue;

                    if (!revealedStageIds.Contains(binding.StageId))
                        Reveal(binding.StageId);
                }

                naturalEndingApplied = true;
            }

            FreezeRevealedStages();
        }

        private void FreezeRevealedStages()
        {
            foreach (string stageId in revealedStageIds)
            {
                if (bindings.TryGetValue(stageId, out AnimationStageBinding binding) &&
                    binding.Animator != null)
                {
                    binding.Animator.speed = 0f;
                }
            }
        }

        private void ResetVisualState()
        {
            revealedStageIds.Clear();

            if (alwaysVisibleRoot != null)
                alwaysVisibleRoot.SetActive(true);

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding == null)
                    continue;

                binding.CacheRuntimeData();

                if (binding.Animator != null)
                    binding.Animator.speed = 1f;

                if (binding.Root != null)
                    binding.Root.SetActive(false);
            }
        }

        private void BuildBindingCache()
        {
            bindings.Clear();

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding == null)
                    continue;

                binding.CacheRuntimeData();
                if (!string.IsNullOrWhiteSpace(binding.StageId) && !bindings.ContainsKey(binding.StageId))
                    bindings.Add(binding.StageId, binding);
            }
        }

        private bool ValidateAndLogConfiguration()
        {
            var errors = new List<string>();

            if (rhythmDirector == null)
                errors.Add("Rhythm Director reference is missing.");
            if (sequence == null)
                errors.Add("Animation Sequence asset is missing.");
            if (alwaysVisibleRoot == null)
                errors.Add("Always Visible Root (acaizeiro) reference is missing.");

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < stages.Count; i++)
            {
                AnimationStageBinding binding = stages[i];
                string label = $"Stage binding {i + 1}";

                if (binding == null)
                {
                    errors.Add($"{label} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.StageId))
                    errors.Add($"{label} has an empty Stage ID.");
                else if (!stageIds.Add(binding.StageId))
                    errors.Add($"Stage ID '{binding.StageId}' is bound more than once.");

                if (binding.Root == null)
                    errors.Add($"{label} has no root GameObject.");
                if (binding.Animator == null)
                    errors.Add($"{label} has no Animator below its root.");
                if (binding.BeatClip == null)
                    errors.Add($"{label} has no beat clip.");
                if (string.IsNullOrWhiteSpace(binding.BeatStateName))
                    errors.Add($"{label} has no beat state name.");
            }

            if (sequence != null)
                errors.AddRange(sequence.GetValidationErrors(stageIds));

            if (rhythmDirector != null && rhythmDirector.Bpm <= 0f)
                errors.Add("Rhythm Director BPM must be greater than zero.");

            foreach (string error in errors)
                Debug.LogError($"Animation progression: {error}", this);

            return errors.Count == 0;
        }

        private bool TryGetFirstOnBeatMarkerTime(out double markerTime)
        {
            markerTime = double.PositiveInfinity;

            if (rhythmDirector == null || rhythmDirector.SongTimelineAsset == null)
                return false;

            foreach (TrackAsset track in rhythmDirector.SongTimelineAsset.GetOutputTracks())
            {
                if (track is not TempoTrack)
                    continue;

                foreach (IMarker marker in track.GetMarkers())
                {
                    if (marker is TempoMarker tempoMarker && tempoMarker.ID == 0)
                        markerTime = Math.Min(markerTime, marker.time);
                }
            }

            return !double.IsPositiveInfinity(markerTime);
        }
    }
}
