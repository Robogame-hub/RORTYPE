using UnityEngine;

namespace RorType.Gameplay.Rts
{
    internal sealed class RtsShotTracer : MonoBehaviour
    {
        private float expireAt;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var tracerObject = new GameObject("RtsShotTracer");
            tracerObject.transform.position = from;

            var tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.SetPositions(new[] { from, to });
            tracer.startWidth = 0.055f;
            tracer.endWidth = 0.012f;
            tracer.startColor = color;
            tracer.endColor = new Color(color.r, color.g, color.b, 0.1f);
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                tracer.material = new Material(shader);
            }

            var effect = tracerObject.AddComponent<RtsShotTracer>();
            effect.expireAt = Time.time + 0.075f;
        }

        private void Update()
        {
            if (Time.time >= expireAt)
            {
                Destroy(gameObject);
            }
        }
    }
}
