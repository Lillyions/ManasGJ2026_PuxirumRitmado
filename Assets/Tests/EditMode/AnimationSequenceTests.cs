using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuxirumRitmado.AnimationProgression.Tests
{
    public sealed class AnimationSequenceTests
    {
        private AnimationSequenceSO sequence;

        [SetUp]
        public void SetUp()
        {
            sequence = ScriptableObject.CreateInstance<AnimationSequenceSO>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void CalculateCueTime_UsesFirstBeatAndMusicalPosition()
        {
            const double firstBeat = 1.7055635679304273d;
            const double bpm = 114.1d;

            double actual = AnimationSequenceController.CalculateCueTime(
                firstBeat,
                bpm,
                4,
                2,
                1);

            double expected = firstBeat + 4d * (60d / bpm);
            Assert.That(actual, Is.EqualTo(expected).Within(0.0000001d));
        }

        [Test]
        public void CalculateCueEndTime_AddsConfiguredDurationInBeats()
        {
            const double cueTime = 3.808103446979507d;
            const double bpm = 114.1d;

            double actual = AnimationSequenceController.CalculateCueEndTime(cueTime, bpm, 4);
            double expected = cueTime + 4d * (60d / bpm);

            Assert.That(actual, Is.EqualTo(expected).Within(0.0000001d));
        }

        [Test]
        public void CalculateCueTime_RejectsInvalidMusicalPositionAndDuration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueTime(0d, 0d, 4, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueTime(0d, 120d, 4, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueTime(0d, 120d, 4, 1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueEndTime(0d, 120d, 0));
        }

        [Test]
        public void Validation_AllowsAdjacentUnitScenes()
        {
            SetCues(
                new AnimationRevealCue("stage_a", 1, 1, 4),
                new AnimationRevealCue("stage_b", 2, 1, 4));

            var knownIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "stage_a",
                "stage_b"
            };

            Assert.That(sequence.GetValidationErrors(knownIds), Is.Empty);
        }

        [Test]
        public void Validation_RejectsOverlappingUnitScenes()
        {
            SetCues(
                new AnimationRevealCue("stage_a", 1, 1, 4),
                new AnimationRevealCue("stage_b", 1, 4, 4));

            var knownIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "stage_a",
                "stage_b"
            };

            Assert.That(sequence.GetValidationErrors(knownIds), Has.Some.Contains("overlaps"));
        }

        [Test]
        public void Validation_RejectsDuplicateUnknownAndInvalidDuration()
        {
            SetCues(
                new AnimationRevealCue("stage_a", 1, 1, 0),
                new AnimationRevealCue("stage_a", 2, 1),
                new AnimationRevealCue("missing", 3, 1));

            var knownIds = new HashSet<string>(StringComparer.Ordinal) { "stage_a" };
            List<string> errors = sequence.GetValidationErrors(knownIds);

            Assert.That(errors, Has.Some.Contains("more than one cue"));
            Assert.That(errors, Has.Some.Contains("unknown Stage ID 'missing'"));
            Assert.That(errors, Has.Some.Contains("duration must be at least 1 beat"));
        }

        [Test]
        public void SequenceAsset_HasUnitDurationsAndColdWaterBeforeHotWater()
        {
            AnimationSequenceSO asset = AssetDatabase.LoadAssetAtPath<AnimationSequenceSO>(
                "Assets/Animations/Sequences/SomDaAmazonia_AnimationSequence.asset");

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Cues.Select(cue => cue.StageId), Is.EqualTo(new[]
            {
                "ribeirinho_sobe",
                "ribeirinha_retira",
                "agua_fria",
                "agua_quente",
                "prepara_polpa"
            }));
            Assert.That(asset.Cues.All(cue => cue.DurationBeats == 4), Is.True);
        }

        [Test]
        public void GameScene_ContainsConfiguredCumulativeAndUnitScenes()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
            AnimationSequenceController controller = Resources
                .FindObjectsOfTypeAll<AnimationSequenceController>()
                .Single(item => item.gameObject.scene == scene);

            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("rhythmDirector").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("sequence").objectReferenceValue, Is.Not.Null);

            var cumulativeRoot =
                (Transform)serialized.FindProperty("cumulativeSceneRoot").objectReferenceValue;
            var alwaysVisible =
                (GameObject)serialized.FindProperty("alwaysVisibleRoot").objectReferenceValue;

            Assert.That(cumulativeRoot, Is.Not.Null);
            Assert.That(cumulativeRoot.name, Is.EqualTo("CumulativeSceneRoot"));
            Assert.That(cumulativeRoot.gameObject.activeSelf, Is.True);
            Assert.That(alwaysVisible.name, Is.EqualTo("acaizeiro"));
            Assert.That(alwaysVisible.activeSelf, Is.True);
            Assert.That(alwaysVisible.transform.IsChildOf(cumulativeRoot), Is.True);

            SerializedProperty stages = serialized.FindProperty("stages");
            Assert.That(stages.arraySize, Is.EqualTo(5));

            var expectedUnitNames = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ribeirinho_sobe"] = "ANIM_subindo_acaizeiro",
                ["ribeirinha_retira"] = "ANIM_retirando_sementes",
                ["agua_quente"] = "ANIM_agua_quente",
                ["agua_fria"] = "ANIM_agua_fria",
                ["prepara_polpa"] = "ANIM_triturando_acai"
            };
            var animationProfiles = new HashSet<RuntimeAnimatorController>();
            var unitScenes = new List<(string StageId, Transform Root)>();

            for (int i = 0; i < stages.arraySize; i++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(i);
                string stageId = stage.FindPropertyRelative("stageId").stringValue;
                var stageRoot = (GameObject)stage.FindPropertyRelative("root").objectReferenceValue;
                var unitRoot = (Transform)stage.FindPropertyRelative("unitSceneRoot").objectReferenceValue;
                var beatClip = stage.FindPropertyRelative("beatClip").objectReferenceValue;
                string stateName = stage.FindPropertyRelative("beatStateName").stringValue;

                Assert.That(expectedUnitNames.ContainsKey(stageId), Is.True);
                Assert.That(stageRoot, Is.Not.Null);
                Assert.That(stageRoot.activeSelf, Is.False);
                Assert.That(stageRoot.transform.IsChildOf(cumulativeRoot), Is.True);
                Assert.That(unitRoot, Is.Not.Null);
                Assert.That(unitRoot.name, Is.EqualTo(expectedUnitNames[stageId]));
                Assert.That(unitRoot.gameObject.activeSelf, Is.False);
                Assert.That(beatClip, Is.Not.Null);

                Animator animator = stageRoot.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);

                var animatorController = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(animatorController, Is.Not.Null);
                Assert.That(
                    animatorController.layers[0].stateMachine.states
                        .Any(childState => childState.state.name == stateName),
                    Is.True);

                animationProfiles.Add(animator.runtimeAnimatorController);
                unitScenes.Add((stageId, unitRoot));
            }

            foreach ((string stageId, Transform unitRoot) in unitScenes)
            {
                Animator[] unitAnimators = unitRoot.GetComponentsInChildren<Animator>(true);
                int expectedAnimatorCount = stageId == "agua_quente" ? 2 : 1;

                Assert.That(unitAnimators.Length, Is.EqualTo(expectedAnimatorCount));
                Assert.That(
                    unitAnimators.All(animator =>
                        animationProfiles.Contains(animator.runtimeAnimatorController)),
                    Is.True);
            }
        }

        private void SetCues(params AnimationRevealCue[] cues)
        {
            var serialized = new SerializedObject(sequence);
            SerializedProperty property = serialized.FindProperty("cues");
            property.arraySize = cues.Length;

            for (int i = 0; i < cues.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("stageId").stringValue = cues[i].StageId;
                element.FindPropertyRelative("bar").intValue = cues[i].Bar;
                element.FindPropertyRelative("beat").intValue = cues[i].Beat;
                element.FindPropertyRelative("durationBeats").intValue = cues[i].DurationBeats;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
