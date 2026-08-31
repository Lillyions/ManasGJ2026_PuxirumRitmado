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

        public string StageId => stageId;
        public int Bar => bar;
        public int Beat => beat;

        public AnimationRevealCue(string stageId, int bar, int beat)
        {
            this.stageId = stageId;
            this.bar = bar;
            this.beat = beat;
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
            }

            return errors;
        }

        private void OnValidate()
        {
            beatsPerBar = Mathf.Max(1, beatsPerBar);
        }
    }
}
