using System;
using UnityEngine;

namespace CartoonFX
{
    public interface ICameraShake
    {
        event Action OnShakeStopped;
        bool IsShaking { get; set; }
        void FetchCameras();
        void StartShake();
        void Animate(float time, Vector3 shakeStrength);
    }
}