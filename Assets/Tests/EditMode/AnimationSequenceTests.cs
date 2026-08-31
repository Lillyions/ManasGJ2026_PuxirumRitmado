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
        public void CalculateCueTime_RejectsInvalidMusicalPosition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueTime(0d, 0d, 4, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueTime(0d, 120d, 4, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AnimationSequenceController.CalculateCueTime(0d, 120d, 4, 1, 5));
        }

        [Test]
        public void Validation_AllowsDifferentStagesAtTheSameMoment()
        {
            SetCues(
                new AnimationRevealCue("stage_a", 4, 1),
                new AnimationRevealCue("stage_b", 4, 1));

            var knownIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "stage_a",
                "stage_b"
            };

            Assert.That(sequence.GetValidationErrors(knownIds), Is.Empty);
        }

        [Test]
        public void Validation_RejectsDuplicateAndUnknownStageIds()
        {
            SetCues(
                new AnimationRevealCue("stage_a", 1, 1),
                new AnimationRevealCue("stage_a", 2, 1),
                new AnimationRevealCue("missing", 3, 1));

            var knownIds = new HashSet<string>(StringComparer.Ordinal) { "stage_a" };
            List<string> errors = sequence.GetValidationErrors(knownIds);

            Assert.That(errors, Has.Some.Contains("more than one cue"));
            Assert.That(errors, Has.Some.Contains("unknown Stage ID 'missing'"));
        }

        [Test]
        public void GameScene_ContainsConfiguredProgressionRoot()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects()
                .Single(item => item.name == "AnimationProgressionRoot");
            AnimationSequenceController controller = root.GetComponent<AnimationSequenceController>();

            Assert.That(controller, Is.Not.Null);

            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("rhythmDirector").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("sequence").objectReferenceValue, Is.Not.Null);

            GameObject alwaysVisible =
                (GameObject)serialized.FindProperty("alwaysVisibleRoot").objectReferenceValue;
            Assert.That(alwaysVisible.name, Is.EqualTo("acaizeiro"));
            Assert.That(alwaysVisible.activeSelf, Is.True);

            SerializedProperty stages = serialized.FindProperty("stages");
            Assert.That(stages.arraySize, Is.EqualTo(5));

            string[] expectedIds =
            {
                "ribeirinho_sobe",
                "ribeirinha_retira",
                "agua_quente",
                "agua_fria",
                "prepara_polpa"
            };

            for (int i = 0; i < stages.arraySize; i++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(i);
                Assert.That(
                    stage.FindPropertyRelative("stageId").stringValue,
                    Is.EqualTo(expectedIds[i]));
                Assert.That(stage.FindPropertyRelative("root").objectReferenceValue, Is.Not.Null);
                Assert.That(stage.FindPropertyRelative("beatClip").objectReferenceValue, Is.Not.Null);

                var stageRoot = (GameObject)stage.FindPropertyRelative("root").objectReferenceValue;
                Assert.That(stageRoot.activeSelf, Is.False);

                Animator animator = stageRoot.GetComponentInChildren<Animator>(true);
                string stateName = stage.FindPropertyRelative("beatStateName").stringValue;
                Assert.That(animator, Is.Not.Null);

                var animatorController = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(animatorController, Is.Not.Null);
                Assert.That(
                    animatorController.layers[0].stateMachine.states
                        .Any(childState => childState.state.name == stateName),
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
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
