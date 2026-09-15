using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    // Vector silhouettes remain crisp at any Canvas scale; authored on the HUD prefab.
    public sealed class MinimalHudGraphic : MaskableGraphic
    {
        public enum Symbol { Burst, StickyBomb, Reticle, Ammunition, Coin, Bolter }
        [SerializeField] private Symbol symbol;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var scale = Mathf.Min(r.width, r.height) / 48f;
            var center = r.center;
            if (symbol == Symbol.Reticle)
            {
                Arc(vh, center, 21 * scale, 35, 145, scale);
                Arc(vh, center, 21 * scale, 215, 325, scale);
                Arc(vh, center, scale, 0, 360, 1.5f * scale);
            }
            else if (symbol == Symbol.Burst)
            {
                for (var i = 0; i < 7; i++)
                {
                    var a = i * Mathf.PI * 2 / 7;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var n = new Vector2(-d.y, d.x);
                    Triangle(vh, center + d * 22 * scale, center + (d * 8 + n * 3) * scale, center + (d * 11 - n * 3) * scale);
                }
                Arc(vh, center, 4 * scale, 0, 360, 2 * scale);
            }
            else if (symbol == Symbol.StickyBomb)
            {
                Arc(vh, center + Vector2.down * 3 * scale, 12 * scale, 0, 360, 3 * scale);
                Line(vh, center + new Vector2(-4, 11) * scale, center + new Vector2(4, 11) * scale, 5 * scale);
                Line(vh, center + new Vector2(0, 13) * scale, center + new Vector2(6, 20) * scale, 2 * scale);
                for (var i = 0; i < 3; i++)
                {
                    var a = (i * 65 + 5) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(vh, center + (new Vector2(6, 20) + d * 3) * scale, center + (new Vector2(6, 20) + d * 6) * scale, scale);
                }
            }
            else if (symbol == Symbol.Bolter)
            {
                // Heavy receiver, short muzzle, box magazine and pistol grip.
                var unit = Mathf.Min(r.width / 72f, r.height / 40f);
                Box(vh, center, unit, -25, -2, 43, 17);
                Box(vh, center, unit, 18, 1, 12, 9);
                Box(vh, center, unit, 29, -1, 5, 13);
                Box(vh, center, unit, -31, -3, 7, 15);
                Box(vh, center, unit, -18, -17, 8, 16);
                Box(vh, center, unit, 0, -18, 13, 17);
                Box(vh, center, unit, -20, 15, 7, 4);
                Box(vh, center, unit, 12, 15, 5, 3);
                Line(vh, center + new Vector2(-10, -3) * unit, center + new Vector2(-7, -10) * unit, unit);
                Line(vh, center + new Vector2(-7, -10) * unit, center + new Vector2(0, -10) * unit, unit);
            }
            else if (symbol == Symbol.Ammunition)
            {
                for (var i = -1; i <= 1; i++)
                {
                    var p = center + Vector2.right * i * 13 * scale;
                    Line(vh, p + Vector2.down * 17 * scale, p + Vector2.up * 9 * scale, 5 * scale);
                    Triangle(vh, p + new Vector2(-2.5f, 10) * scale, p + new Vector2(2.5f, 10) * scale, p + Vector2.up * 19 * scale);
                }
            }
            else
            {
                Arc(vh, center, 19 * scale, 0, 360, scale);
                Arc(vh, center, 15 * scale, 0, 360, scale);
                Line(vh, center + Vector2.down * 10 * scale, center + Vector2.up * 10 * scale, 2 * scale);
                Line(vh, center + Vector2.left * 6 * scale, center + Vector2.right * 6 * scale, 2 * scale);
            }
        }

        private void Box(VertexHelper vh, Vector2 c, float unit, float x, float y, float w, float h)
        {
            var a = c + new Vector2(x, y) * unit;
            var b = c + new Vector2(x + w, y) * unit;
            var d = c + new Vector2(x, y + h) * unit;
            var e = c + new Vector2(x + w, y + h) * unit;
            Triangle(vh, a, d, e);
            Triangle(vh, a, e, b);
        }

        private void Arc(VertexHelper vh, Vector2 c, float radius, float from, float to, float width)
        {
            var steps = Mathf.CeilToInt((to - from) / 8);
            for (var i = 0; i < steps; i++)
            {
                var a = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
                var b = Mathf.Lerp(from, to, (float)(i + 1) / steps) * Mathf.Deg2Rad;
                Line(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width);
            }
        }

        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * width * 0.5f;
            Triangle(vh, a - n, a + n, b + n);
            Triangle(vh, a - n, b + n, b - n);
        }

        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            var i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }
    }
}
