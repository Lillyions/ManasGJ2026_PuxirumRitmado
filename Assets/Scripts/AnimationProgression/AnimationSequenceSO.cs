using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuxirumRitmado.AnimationProgression
{
    [Serializable]
    public sealed class AnimationRevealCue
    {
        [SerializeField] private string stageId;
        [SerializeField, Min(1)] private int bar = 1;
        [SerializeField, Min(1)] private int beat = 1;
        [SerializeField, Min(1)] private int durationBeats = 4;

        public string StageId => stageId;
        public int Bar => bar;
        public int Beat => beat;
        public int DurationBeats => durationBeats;

        public AnimationRevealCue(string stageId, int bar, int beat, int durationBeats = 4)
        {
            this.stageId = stageId;
            this.bar = bar;
            this.beat = beat;
            this.durationBeats = durationBeats;
        }
    }

    [CreateAssetMenu(
        fileName = "AnimationSequence",
        menuName = "Puxirum/Animation Sequence",
        order = 20)]
    public sealed class AnimationSequenceSO : ScriptableObject
    {
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [SerializeField] private List<AnimationRevealCue> cues = new();

        public int BeatsPerBar => beatsPerBar;
        public IReadOnlyList<AnimationRevealCue> Cues => cues;

        public List<string> GetValidationErrors(ISet<string> knownStageIds = null)
        {
            var errors = new List<string>();
            var seenStageIds = new HashSet<string>(StringComparer.Ordinal);

            if (beatsPerBar < 1)
                errors.Add("Beats Per Bar must be at least 1.");

            for (int i = 0; i < cues.Count; i++)
            {
                AnimationRevealCue cue = cues[i];
                string label = $"Cue {i + 1}";

                if (cue == null)
                {
                    errors.Add($"{label} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cue.StageId))
                {
                    errors.Add($"{label} has an empty Stage ID.");
                }
                else
                {
                    if (!seenStageIds.Add(cue.StageId))
                        errors.Add($"Stage ID '{cue.StageId}' is used by more than one cue.");

                    if (knownStageIds != null && !knownStageIds.Contains(cue.StageId))
                        errors.Add($"{label} references unknown Stage ID '{cue.StageId}'.");
                }

                if (cue.Bar < 1)
                    errors.Add($"{label} has invalid bar {cue.Bar}; bars start at 1.");

                if (cue.Beat < 1 || cue.Beat > beatsPerBar)
                    errors.Add($"{label} has invalid beat {cue.Beat}; valid beats are 1-{beatsPerBar}.");

                if (cue.DurationBeats < 1)
                    errors.Add($"{label} has invalid duration {cue.DurationBeats}; duration must be at least 1 beat.");
            }

            AddOverlapErrors(errors);

            return errors;
        }

        private void AddOverlapErrors(List<string> errors)
        {
            if (beatsPerBar < 1)
                return;

            var scheduledCues = new List<(AnimationRevealCue Cue, int SourceIndex, long StartBeat)>();

            for (int i = 0; i < cues.Count; i++)
            {
                AnimationRevealCue cue = cues[i];
                if (cue == null || cue.Bar < 1 || cue.Beat < 1 || cue.Beat > beatsPerBar ||
                    cue.DurationBeats < 1)
                {
                    continue;
                }

                long startBeat = ((long)cue.Bar - 1L) * beatsPerBar + cue.Beat - 1L;
                scheduledCues.Add((cue, i, startBeat));
            }

            scheduledCues.Sort((left, right) =>
            {
                int timeComparison = left.StartBeat.CompareTo(right.StartBeat);
                return timeComparison != 0
                    ? timeComparison
                    : left.SourceIndex.CompareTo(right.SourceIndex);
            });

            for (int i = 1; i < scheduledCues.Count; i++)
            {
                var previous = scheduledCues[i - 1];
                var current = scheduledCues[i];
                long previousEndBeat = previous.StartBeat + previous.Cue.DurationBeats;

                if (current.StartBeat < previousEndBeat)
                {
                    errors.Add(
                        $"Cue {current.SourceIndex + 1} overlaps Cue {previous.SourceIndex + 1}; " +
                        "unit animation scenes cannot overlap.");
                }
            }
        }

        private void OnValidate()
        {
            beatsPerBar = Mathf.Max(1, beatsPerBar);
        }
    }
}
