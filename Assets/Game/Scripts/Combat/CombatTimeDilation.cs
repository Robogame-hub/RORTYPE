using System.Collections;
using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public static class CombatTimeDilation
    {
        private static TimeDilationRunner runner;

        public static void Pulse(float targetTimeScale, float durationRealtime)
        {
            if (durationRealtime <= 0f || targetTimeScale >= 1f)
            {
                return;
            }

            EnsureRunner();
            runner.PlayPulse(Mathf.Clamp(targetTimeScale, 0.05f, 1f), durationRealtime);
        }

        private static void EnsureRunner()
        {
            if (runner != null)
            {
                return;
            }

            var runnerObject = new GameObject("CombatTimeDilation");
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            runner = runnerObject.AddComponent<TimeDilationRunner>();
        }

        private sealed class TimeDilationRunner : MonoBehaviour
        {
            private Coroutine activePulse;
            private float defaultFixedDeltaTime = 0.02f;

            private void Awake()
            {
                defaultFixedDeltaTime = Time.fixedDeltaTime;
            }

            public void PlayPulse(float targetTimeScale, float durationRealtime)
            {
                if (activePulse != null)
                {
                    StopCoroutine(activePulse);
                }

                activePulse = StartCoroutine(PlayPulseRoutine(targetTimeScale, durationRealtime));
            }

            private IEnumerator PlayPulseRoutine(float targetTimeScale, float durationRealtime)
            {
                ApplyTimeScale(targetTimeScale);
                yield return new WaitForSecondsRealtime(durationRealtime);
                ApplyTimeScale(1f);
                activePulse = null;
            }

            private void OnDisable()
            {
                ApplyTimeScale(1f);
            }

            private void ApplyTimeScale(float targetTimeScale)
            {
                Time.timeScale = targetTimeScale;
                Time.fixedDeltaTime = defaultFixedDeltaTime * targetTimeScale;
            }
        }
    }
}
