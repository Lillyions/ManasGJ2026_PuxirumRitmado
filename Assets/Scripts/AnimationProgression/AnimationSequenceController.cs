using System;
using System.Collections.Generic;
using System.Linq;
using Dypsloom.RhythmTimeline.Core.Managers;
using Dypsloom.RhythmTimeline.Core.Playables;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

namespace PuxirumRitmado.AnimationProgression
{
    [Serializable]
    public sealed class AnimationStageBinding
    {
        [SerializeField] private string stageId;
        [SerializeField] private GameObject root;
        [SerializeField] private Transform unitSceneRoot;
        [SerializeField] private string beatStateName;
        [SerializeField] private AnimationClip beatClip;

        [NonSerialized] private Animator animator;
        [NonSerialized] private Animator[] unitAnimators = Array.Empty<Animator>();
        [NonSerialized] private int beatStateHash;

        public string StageId => stageId;
        public GameObject Root => root;
        public Transform UnitSceneRoot => unitSceneRoot;
        public string BeatStateName => beatStateName;
        public AnimationClip BeatClip => beatClip;
        public Animator Animator => animator;
        public IReadOnlyList<Animator> UnitAnimators => unitAnimators;
        public int BeatStateHash => beatStateHash;

        public void CacheRuntimeData()
        {
            animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
            unitAnimators = unitSceneRoot != null
                ? unitSceneRoot.GetComponentsInChildren<Animator>(true)
                : Array.Empty<Animator>();
            beatStateHash = string.IsNullOrWhiteSpace(beatStateName)
                ? 0
                : Animator.StringToHash(beatStateName);
        }
    }

    public sealed class AnimationSequenceController : MonoBehaviour
    {
        private const double TimeEpsilon = 0.0001d;
        private const double NaturalEndDetectionTolerance = 0.25d;
        private const int UnitSceneSortingOrder = -1;

        [Header("Timeline")]
        [SerializeField] private RhythmDirector rhythmDirector;
        [SerializeField] private AnimationSequenceSO sequence;

        [Header("Scene")]
        [SerializeField] private Transform cumulativeSceneRoot;
        [SerializeField] private GameObject alwaysVisibleRoot;
        [SerializeField] private List<AnimationStageBinding> stages = new();

        private readonly Dictionary<string, AnimationStageBinding> bindings =
            new(StringComparer.Ordinal);
        private readonly Dictionary<RuntimeAnimatorController, AnimationStageBinding> animationProfiles = new();
        private readonly HashSet<string> revealedStageIds = new(StringComparer.Ordinal);
        private readonly List<RuntimeCueEvent> runtimeEvents = new();

        private int nextEventIndex;
        private double firstBeatTime;
        private double secondsPerBeat;
        private double songDuration;
        private double lastObservedTimelineTime;
        private bool sequenceReady;
        private bool naturalEndingApplied;
        private string activeUnitStageId;

        public AnimationSequenceSO Sequence => sequence;
        public double FirstBeatTime => firstBeatTime;
        public bool IsReady => sequenceReady;
        public string ActiveUnitStageId => activeUnitStageId;

        private enum CueEventKind
        {
            Complete = 0,
            Begin = 1
        }

        private readonly struct RuntimeCueEvent
        {
            public RuntimeCueEvent(
                AnimationRevealCue cue,
                int sourceIndex,
                double time,
                CueEventKind kind)
            {
                Cue = cue;
                SourceIndex = sourceIndex;
                Time = time;
                Kind = kind;
            }

            public AnimationRevealCue Cue { get; }
            public int SourceIndex { get; }
            public double Time { get; }
            public CueEventKind Kind { get; }
        }

        private void Awake()
        {
            BuildBindingCache();
            ConfigureUnitScenePresentation();
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
                nextEventIndex = 0;
            }

            ProcessEventsThrough(currentTime);
            lastObservedTimelineTime = currentTime;

            if (!naturalEndingApplied && songDuration > 0d && currentTime >= songDuration - TimeEpsilon)
                ApplyNaturalEnding();
        }

        public bool Reveal(string stageId)
        {
            if (!TryGetBinding(stageId, out AnimationStageBinding binding))
                return false;

            if (revealedStageIds.Contains(stageId))
                return true;

            if (!IsValidAnimationProfile(binding))
                return false;

            revealedStageIds.Add(stageId);
            binding.Root.SetActive(true);

            if (cumulativeSceneRoot == null || !cumulativeSceneRoot.gameObject.activeInHierarchy)
                return true;

            double timelineTime = rhythmDirector != null
                ? rhythmDirector.PlayableDirector.time
                : firstBeatTime;

            if (StartBeatLoop(binding, timelineTime))
                return true;

            revealedStageIds.Remove(stageId);
            binding.Root.SetActive(false);
            return false;
        }

        public bool ResumeBeatLoop(string stageId)
        {
            if (!TryGetBinding(stageId, out AnimationStageBinding binding))
                return false;

            if (binding.Root == null || !binding.Root.activeInHierarchy)
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

        public static double CalculateCueEndTime(double cueTime, double bpm, int durationBeats)
        {
            if (bpm <= 0d)
                throw new ArgumentOutOfRangeException(nameof(bpm), "BPM must be greater than zero.");
            if (durationBeats < 1)
                throw new ArgumentOutOfRangeException(nameof(durationBeats));

            return cueTime + durationBeats * (60d / bpm);
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
            ConfigureUnitScenePresentation();
            ResetVisualState();
            runtimeEvents.Clear();
            nextEventIndex = 0;
            lastObservedTimelineTime = 0d;
            songDuration = 0d;
            naturalEndingApplied = false;
            sequenceReady = false;

            if (!ValidateAndLogConfiguration())
                return;

            songDuration = rhythmDirector.PlayableDirector.duration;

            if (!TryGetFirstOnBeatMarkerTime(out firstBeatTime))
            {
                Debug.LogError(
                    "Animation progression requires a TempoMarker with ID 0 in the song Timeline.",
                    this);
                return;
            }

            secondsPerBeat = 60d / rhythmDirector.Bpm;

            for (int i = 0; i < sequence.Cues.Count; i++)
            {
                AnimationRevealCue cue = sequence.Cues[i];
                double cueTime = CalculateCueTime(
                    firstBeatTime,
                    rhythmDirector.Bpm,
                    sequence.BeatsPerBar,
                    cue.Bar,
                    cue.Beat);
                double endTime = CalculateCueEndTime(cueTime, rhythmDirector.Bpm, cue.DurationBeats);

                runtimeEvents.Add(new RuntimeCueEvent(cue, i, cueTime, CueEventKind.Begin));
                runtimeEvents.Add(new RuntimeCueEvent(cue, i, endTime, CueEventKind.Complete));
            }

            runtimeEvents.Sort((left, right) =>
            {
                int timeComparison = left.Time.CompareTo(right.Time);
                if (timeComparison != 0)
                    return timeComparison;

                int kindComparison = left.Kind.CompareTo(right.Kind);
                return kindComparison != 0
                    ? kindComparison
                    : left.SourceIndex.CompareTo(right.SourceIndex);
            });

            sequenceReady = true;
            lastObservedTimelineTime = rhythmDirector.PlayableDirector.time;
            ProcessEventsThrough(lastObservedTimelineTime);
        }

        private void HandleSongEnd()
        {
            sequenceReady = false;

            bool reachedNaturalEnd = naturalEndingApplied ||
                                     (songDuration > 0d &&
                                      lastObservedTimelineTime >= songDuration - NaturalEndDetectionTolerance);

            if (reachedNaturalEnd)
            {
                ApplyNaturalEnding();
                return;
            }

            HideAllUnitScenes();
            RestoreCumulativeScene(lastObservedTimelineTime);
            FreezeRevealedStages();
        }

        private void ProcessEventsThrough(double timelineTime)
        {
            while (nextEventIndex < runtimeEvents.Count &&
                   runtimeEvents[nextEventIndex].Time <= timelineTime + TimeEpsilon)
            {
                RuntimeCueEvent cueEvent = runtimeEvents[nextEventIndex];

                if (cueEvent.Kind == CueEventKind.Begin)
                    BeginUnitScene(cueEvent.Cue, timelineTime);
                else
                    CompleteUnitScene(cueEvent.Cue, timelineTime);

                nextEventIndex++;
            }
        }

        private void BeginUnitScene(AnimationRevealCue cue, double timelineTime)
        {
            if (cue == null || revealedStageIds.Contains(cue.StageId))
                return;

            if (!bindings.TryGetValue(cue.StageId, out AnimationStageBinding binding) ||
                binding.UnitSceneRoot == null)
            {
                Debug.LogError($"Cannot start unit scene for Stage ID '{cue.StageId}'.", this);
                return;
            }

            HideAllUnitScenes();

            if (cumulativeSceneRoot != null)
                cumulativeSceneRoot.gameObject.SetActive(false);

            binding.UnitSceneRoot.gameObject.SetActive(true);
            activeUnitStageId = cue.StageId;

            foreach (Animator unitAnimator in binding.UnitAnimators)
            {
                if (!TryGetAnimationProfile(unitAnimator, out AnimationStageBinding profile))
                    continue;

                StartBeatLoop(
                    unitAnimator,
                    profile.BeatStateHash,
                    profile.BeatStateName,
                    profile.BeatClip,
                    cue.StageId,
                    timelineTime);
            }
        }

        private void CompleteUnitScene(AnimationRevealCue cue, double timelineTime)
        {
            if (cue == null)
                return;

            HideAllUnitScenes();

            if (!revealedStageIds.Contains(cue.StageId) &&
                TryGetBinding(cue.StageId, out AnimationStageBinding binding) &&
                IsValidAnimationProfile(binding))
            {
                revealedStageIds.Add(cue.StageId);
            }

            RestoreCumulativeScene(timelineTime);
        }

        private void RestoreCumulativeScene(double timelineTime)
        {
            if (cumulativeSceneRoot != null)
                cumulativeSceneRoot.gameObject.SetActive(true);

            if (alwaysVisibleRoot != null)
                alwaysVisibleRoot.SetActive(true);

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding?.Root == null)
                    continue;

                bool isRevealed = revealedStageIds.Contains(binding.StageId);
                binding.Root.SetActive(isRevealed);

                if (isRevealed)
                    StartBeatLoop(binding, timelineTime);
            }
        }

        private bool StartBeatLoop(AnimationStageBinding binding, double timelineTime)
        {
            if (binding.Root == null)
                return false;

            binding.Root.SetActive(true);
            return StartBeatLoop(
                binding.Animator,
                binding.BeatStateHash,
                binding.BeatStateName,
                binding.BeatClip,
                binding.StageId,
                timelineTime);
        }

        private bool StartBeatLoop(
            Animator animator,
            int stateHash,
            string stateName,
            AnimationClip beatClip,
            string stageId,
            double timelineTime)
        {
            EnsureBeatTimingAvailable();

            if (secondsPerBeat <= 0d)
            {
                Debug.LogError("Cannot start a beat loop before a valid BPM is available.", this);
                return false;
            }

            if (animator == null || beatClip == null || stateHash == 0)
                return false;

            if (!animator.HasState(0, stateHash))
            {
                Debug.LogError(
                    $"Animator for stage '{stageId}' has no state '{stateName}' on layer 0.",
                    animator);
                return false;
            }

            double beatPosition = Math.Max(0d, (timelineTime - firstBeatTime) / secondsPerBeat);
            float beatPhase = (float)(beatPosition - Math.Floor(beatPosition));

            animator.speed = (float)(beatClip.length / secondsPerBeat);
            animator.Play(stateHash, 0, beatPhase);
            animator.Update(0f);
            return true;
        }

        private void EnsureBeatTimingAvailable()
        {
            if (secondsPerBeat > 0d || rhythmDirector == null || rhythmDirector.Bpm <= 0f)
                return;

            secondsPerBeat = 60d / rhythmDirector.Bpm;
            TryGetFirstOnBeatMarkerTime(out firstBeatTime);
        }

        private void ApplyNaturalEnding()
        {
            HideAllUnitScenes();

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.StageId))
                    continue;

                revealedStageIds.Add(binding.StageId);
            }

            double timelineTime = rhythmDirector != null
                ? rhythmDirector.PlayableDirector.time
                : lastObservedTimelineTime;
            RestoreCumulativeScene(timelineTime);
            naturalEndingApplied = true;
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
            activeUnitStageId = null;

            if (cumulativeSceneRoot != null)
                cumulativeSceneRoot.gameObject.SetActive(true);

            if (alwaysVisibleRoot != null)
                alwaysVisibleRoot.SetActive(true);

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding == null)
                    continue;

                binding.CacheRuntimeData();

                if (binding.Animator != null)
                    binding.Animator.speed = 1f;

                foreach (Animator unitAnimator in binding.UnitAnimators)
                {
                    if (unitAnimator != null)
                        unitAnimator.speed = 1f;
                }

                if (binding.Root != null)
                    binding.Root.SetActive(false);

                if (binding.UnitSceneRoot != null)
                    binding.UnitSceneRoot.gameObject.SetActive(false);
            }
        }

        private void HideAllUnitScenes()
        {
            foreach (AnimationStageBinding binding in stages)
            {
                if (binding?.UnitSceneRoot != null)
                    binding.UnitSceneRoot.gameObject.SetActive(false);
            }

            activeUnitStageId = null;
        }

        private void ConfigureUnitScenePresentation()
        {
            var configuredRoots = new HashSet<Transform>();

            foreach (AnimationStageBinding binding in stages)
            {
                Transform unitRoot = binding?.UnitSceneRoot;
                if (unitRoot == null || !configuredRoots.Add(unitRoot))
                    continue;

                foreach (Canvas canvas in unitRoot.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = UnitSceneSortingOrder;
                }

                SortingGroup sortingGroup = unitRoot.GetComponent<SortingGroup>();
                if (sortingGroup == null)
                    sortingGroup = unitRoot.gameObject.AddComponent<SortingGroup>();

                sortingGroup.sortingOrder = UnitSceneSortingOrder;
            }
        }

        private void BuildBindingCache()
        {
            bindings.Clear();
            animationProfiles.Clear();

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding == null)
                    continue;

                binding.CacheRuntimeData();

                if (!string.IsNullOrWhiteSpace(binding.StageId) && !bindings.ContainsKey(binding.StageId))
                    bindings.Add(binding.StageId, binding);

                RuntimeAnimatorController controller = binding.Animator?.runtimeAnimatorController;
                if (controller != null && !animationProfiles.ContainsKey(controller))
                    animationProfiles.Add(controller, binding);
            }
        }

        private bool TryGetBinding(string stageId, out AnimationStageBinding binding)
        {
            binding = null;

            if (string.IsNullOrWhiteSpace(stageId))
            {
                Debug.LogError("Cannot use an animation stage with an empty ID.", this);
                return false;
            }

            if (!bindings.TryGetValue(stageId, out binding))
            {
                Debug.LogError($"Animation stage '{stageId}' has no scene binding.", this);
                return false;
            }

            return true;
        }

        private bool TryGetAnimationProfile(
            Animator animator,
            out AnimationStageBinding profile)
        {
            profile = null;
            RuntimeAnimatorController controller = animator?.runtimeAnimatorController;
            return controller != null && animationProfiles.TryGetValue(controller, out profile);
        }

        private static bool IsValidAnimationProfile(AnimationStageBinding binding)
        {
            return binding?.Root != null && binding.Animator != null && binding.BeatClip != null &&
                   binding.BeatStateHash != 0;
        }

        private bool ValidateAndLogConfiguration()
        {
            var errors = new List<string>();

            if (rhythmDirector == null)
                errors.Add("Rhythm Director reference is missing.");
            else if (rhythmDirector.PlayableDirector == null)
                errors.Add("Rhythm Director has no Playable Director.");
            if (sequence == null)
                errors.Add("Animation Sequence asset is missing.");
            if (cumulativeSceneRoot == null)
                errors.Add("Cumulative Scene Root reference is missing.");
            if (alwaysVisibleRoot == null)
                errors.Add("Always Visible Root (acaizeiro) reference is missing.");
            else if (cumulativeSceneRoot != null &&
                     !alwaysVisibleRoot.transform.IsChildOf(cumulativeSceneRoot))
                errors.Add("Always Visible Root must be a child of Cumulative Scene Root.");

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            var unitRoots = new HashSet<Transform>();
            var profileControllers = new HashSet<RuntimeAnimatorController>();

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
                    errors.Add($"{label} has no cumulative root GameObject.");
                else if (cumulativeSceneRoot != null &&
                         !binding.Root.transform.IsChildOf(cumulativeSceneRoot))
                    errors.Add($"{label} cumulative root must be a child of Cumulative Scene Root.");

                if (binding.Animator == null)
                    errors.Add($"{label} has no Animator below its cumulative root.");
                else if (binding.Animator.runtimeAnimatorController == null)
                    errors.Add($"{label} cumulative Animator has no controller.");
                else if (!profileControllers.Add(binding.Animator.runtimeAnimatorController))
                    errors.Add($"{label} reuses an Animator Controller already assigned to another stage.");

                if (binding.BeatClip == null)
                    errors.Add($"{label} has no beat clip.");
                if (string.IsNullOrWhiteSpace(binding.BeatStateName))
                    errors.Add($"{label} has no beat state name.");

                if (binding.UnitSceneRoot == null)
                {
                    errors.Add($"{label} has no unit scene root.");
                }
                else
                {
                    if (!unitRoots.Add(binding.UnitSceneRoot))
                        errors.Add($"{label} reuses a unit scene root assigned to another stage.");
                    if (binding.UnitSceneRoot.GetComponentInChildren<Canvas>(true) == null)
                        errors.Add($"{label} unit scene has no Canvas.");
                    if (binding.UnitAnimators.Count == 0)
                        errors.Add($"{label} unit scene has no Animator.");
                }
            }

            foreach (AnimationStageBinding binding in stages)
            {
                if (binding == null)
                    continue;

                foreach (Animator unitAnimator in binding.UnitAnimators)
                {
                    if (unitAnimator == null || unitAnimator.runtimeAnimatorController == null)
                    {
                        errors.Add($"Unit scene for '{binding.StageId}' contains an Animator without a controller.");
                    }
                    else if (!animationProfiles.ContainsKey(unitAnimator.runtimeAnimatorController))
                    {
                        errors.Add(
                            $"Unit scene for '{binding.StageId}' contains an Animator with no matching beat profile.");
                    }
                }
            }

            if (sequence != null)
                errors.AddRange(sequence.GetValidationErrors(stageIds));

            if (rhythmDirector != null && rhythmDirector.Bpm <= 0f)
                errors.Add("Rhythm Director BPM must be greater than zero.");

            foreach (string error in errors.Distinct())
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
