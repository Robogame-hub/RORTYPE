using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace CartoonFX
{
    public class CameraShake : MonoBehaviour, ICameraShake
    {
        public event Action OnShakeStopped;

        // Change this value to easily tune the camera shake strength for all effects
        const float GlobalCameraShakeMultiplier = 1.0f;

        public enum ShakeSpace
        {
            Screen,
            World
        }

        public static bool EditorPreview = true;

        //--------------------------------------------------------------------------------------------------------------------------------

        public bool _enabled;
        [Space] public bool _useMainCamera = true;
        public List<Camera> _cameras = new();
        [Space] public float _delay;
        public float _duration = 1.0f;
        public ShakeSpace _shakeSpace = ShakeSpace.Screen;
        public AnimationCurve _shakeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [Space] [Range(0, 0.1f)] public float _shakesDelay;

        public bool IsShaking { get; set; }
        private readonly Dictionary<Camera, Vector3> _camerasPreRenderPosition = new();
        private Vector3 _shakeVector;
        private float _delaysTimer;

        //--------------------------------------------------------------------------------------------------------------------------------
        // STATIC
        // Use static methods to dispatch the Camera callbacks, to ensure that ScreenShake components are called in an order in PreRender,
        // and in the _reverse_ order for PostRender, so that the final Camera position is the same as it is originally (allowing concurrent
        // screen shake to be active)

        static bool _sCallbackRegistered;
        static List<CameraShake> _sCameraShakes = new List<CameraShake>();

        // "Enter Play Mode Options" with Reload Domain disabled keeps static state alive
        // between play sessions. Without this reset a shake that was active when play mode
        // was stopped leaves a destroyed CameraShake registered on the render pipeline,
        // which keeps offsetting Camera.main every frame on the next run (camera "drifts away").
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            Camera.onPreRender -= OnPreRenderCamera_Static;
            Camera.onPostRender -= OnPostRenderCamera_Static;
#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering -= OnPreRenderCamera_Static_URP;
            RenderPipelineManager.endCameraRendering -= OnPostRenderCamera_Static_URP;
#endif
            _sCameraShakes.Clear();
            _sCallbackRegistered = false;
        }

        private void Awake()
        {
            FetchCameras();
        }

        private void OnDisable()
        {
            // Drop the static subscription when the component is disabled or play mode exits,
            // so a destroyed instance can't keep shaking the camera on the next play session.
            if (IsShaking)
            {
                IsShaking = false;
                _shakeVector = Vector3.zero;
            }

            UnregisterStaticCallback(this);
        }

#if UNITY_2019_1_OR_NEWER
        static void OnPreRenderCamera_Static_URP(ScriptableRenderContext context, Camera cam)
        {
            OnPreRenderCamera_Static(cam);
        }

        static void OnPostRenderCamera_Static_URP(ScriptableRenderContext context, Camera cam)
        {
            OnPostRenderCamera_Static(cam);
        }
#endif

        static void OnPreRenderCamera_Static(Camera cam)
        {
            int count = _sCameraShakes.Count;
            for (int i = 0; i < count; i++)
            {
                var ss = _sCameraShakes[i];
                ss.OnPreRenderCamera(cam);
            }
        }

        static void OnPostRenderCamera_Static(Camera cam)
        {
            int count = _sCameraShakes.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                var ss = _sCameraShakes[i];
                ss.OnPostRenderCamera(cam);
            }
        }

        static void RegisterStaticCallback(CameraShake cameraShake)
        {
            _sCameraShakes.Add(cameraShake);

            if (!_sCallbackRegistered)
            {
#if UNITY_2019_1_OR_NEWER
#if UNITY_2019_3_OR_NEWER
                if (GraphicsSettings.currentRenderPipeline == null)
#else
					if (GraphicsSettings.renderPipelineAsset == null)
#endif
                {
                    // Built-in Render Pipeline
                    Camera.onPreRender += OnPreRenderCamera_Static;
                    Camera.onPostRender += OnPostRenderCamera_Static;
                }
                else
                {
                    // URP
                    RenderPipelineManager.beginCameraRendering += OnPreRenderCamera_Static_URP;
                    RenderPipelineManager.endCameraRendering += OnPostRenderCamera_Static_URP;
                }
#else
						Camera.onPreRender += OnPreRenderCamera_Static;
						Camera.onPostRender += OnPostRenderCamera_Static;
#endif

                _sCallbackRegistered = true;
            }
        }

        static void UnregisterStaticCallback(CameraShake cameraShake)
        {
            _sCameraShakes.Remove(cameraShake);

            if (_sCallbackRegistered && _sCameraShakes.Count == 0)
            {
#if UNITY_2019_1_OR_NEWER
#if UNITY_2019_3_OR_NEWER
                if (GraphicsSettings.currentRenderPipeline == null)
#else
					if (GraphicsSettings.renderPipelineAsset == null)
#endif
                {
                    // Built-in Render Pipeline
                    Camera.onPreRender -= OnPreRenderCamera_Static;
                    Camera.onPostRender -= OnPostRenderCamera_Static;
                }
                else
                {
                    // URP
                    RenderPipelineManager.beginCameraRendering -= OnPreRenderCamera_Static_URP;
                    RenderPipelineManager.endCameraRendering -= OnPostRenderCamera_Static_URP;
                }
#else
						Camera.onPreRender -= OnPreRenderCamera_Static;
						Camera.onPostRender -= OnPostRenderCamera_Static;
#endif

                _sCallbackRegistered = false;
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------------

        void OnPreRenderCamera(Camera cam)
        {
#if UNITY_EDITOR
            //add scene view camera if necessary
            if (SceneView.currentDrawingSceneView != null && SceneView.currentDrawingSceneView.camera == cam)
            {
                _camerasPreRenderPosition.TryAdd(cam, cam.transform.localPosition);
            }
#endif

            if (IsShaking && _camerasPreRenderPosition.ContainsKey(cam))
            {
                _camerasPreRenderPosition[cam] = cam.transform.localPosition;

                if (Time.timeScale <= 0) return;

                switch (_shakeSpace)
                {
                    case ShakeSpace.Screen:
                        cam.transform.localPosition += cam.transform.rotation * _shakeVector;
                        break;
                    case ShakeSpace.World:
                        cam.transform.localPosition += _shakeVector;
                        break;
                }
            }
        }

        void OnPostRenderCamera(Camera cam)
        {
            if (_camerasPreRenderPosition.TryGetValue(cam, out var value))
            {
                cam.transform.localPosition = value;
            }
        }

        public void FetchCameras()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
#endif

            foreach (var cam in _cameras)
            {
                if (cam == null) continue;

                _camerasPreRenderPosition.Remove(cam);
            }

            _cameras.Clear();

            if (_useMainCamera && Camera.main != null)
            {
                _cameras.Add(Camera.main);
            }

            foreach (var cam in _cameras)
            {
                if (cam == null) continue;

                _camerasPreRenderPosition.TryAdd(cam, Vector3.zero);
            }
        }

        public void StartShake()
        {
            if (IsShaking)
            {
                StopShake();
            }

            IsShaking = true;
            RegisterStaticCallback(this);
        }

        public void StopShake()
        {
            IsShaking = false;
            _shakeVector = Vector3.zero;
            UnregisterStaticCallback(this);

            OnShakeStopped?.Invoke();
        }

        public void Animate(float time, Vector3 shakeStrength)
        {
#if UNITY_EDITOR
            if (!EditorPreview && !EditorApplication.isPlaying)
            {
                _shakeVector = Vector3.zero;
                return;
            }
#endif

            float totalDuration = _duration + _delay;
            if (time < totalDuration)
            {
                if (time < _delay)
                {
                    return;
                }

                if (!IsShaking)
                {
                    this.StartShake();
                }

                // duration of the camera shake
                float delta = Mathf.Clamp01(time / totalDuration);

                // delay between each camera move
                if (_shakesDelay > 0)
                {
                    _delaysTimer += Time.deltaTime;
                    if (_delaysTimer < _shakesDelay)
                    {
                        return;
                    }
                    else
                    {
                        while (_delaysTimer >= _shakesDelay)
                        {
                            _delaysTimer -= _shakesDelay;
                        }
                    }
                }

                var randomVec = new Vector3(Random.value, Random.value, Random.value);
                var shakeVec = Vector3.Scale(randomVec, shakeStrength) * (Random.value > 0.5f ? -1 : 1);
                _shakeVector = shakeVec * _shakeCurve.Evaluate(delta) * GlobalCameraShakeMultiplier;
            }
            else if (IsShaking)
            {
                StopShake();
            }
        }
    }
}