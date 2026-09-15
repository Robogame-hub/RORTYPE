using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    // Authored UI graphic: chamfered metal plate, brass trim, rivets and a winged seal.
    public sealed class ImperialPanelGraphic : Image
    {
        [SerializeField] private Color trimColor = new Color(0.58f, 0.45f, 0.25f, 1f);
        [SerializeField] private bool ornament = true;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (!ornament)
            {
                Plate(vh, r, 0, color);
                const float length = 12f;
                foreach (var x in new[] { r.x, r.xMax - length })
                    foreach (var y in new[] { r.y, r.yMax - 1 })
                        Quad(vh, new Rect(x, y, length, 1), trimColor);
                foreach (var x in new[] { r.x, r.xMax - 1 })
                    foreach (var y in new[] { r.y, r.yMax - length })
                        Quad(vh, new Rect(x, y, 1, length), trimColor);
                return;
            }
            var cut = Mathf.Min(12f, Mathf.Min(r.width, r.height) * 0.15f);
            Plate(vh, new Rect(r.x + 3, r.y - 4, r.width, r.height), cut, new Color(0, 0, 0, 0.65f));
            Plate(vh, r, cut, trimColor);
            Plate(vh, Inset(r, 2f), Mathf.Max(0, cut - 2), color);
            var subtle = new Color(trimColor.r, trimColor.g, trimColor.b, 0.25f);
            Quad(vh, new Rect(r.x + 14, r.yMax - 7, r.width - 28, 1), subtle);
            Quad(vh, new Rect(r.x + 14, r.y + 6, r.width - 28, 1), subtle);
            foreach (var x in new[] { r.x + 8, r.xMax - 8 })
                foreach (var y in new[] { r.y + 8, r.yMax - 8 })
                    Quad(vh, new Rect(x - 1.5f, y - 1.5f, 3, 3), trimColor);
            if (!ornament || r.width < 250 || r.height < 90) return;
            var center = r.center.x;
            var top = r.yMax - 16;
            Plate(vh, new Rect(center - 4, top - 4, 8, 8), 4, trimColor);
            for (var i = 0; i < 3; i++)
            {
                var width = 24 - i * 5;
                Quad(vh, new Rect(center + 8, top + 3 - i * 4, width, 2), trimColor);
                Quad(vh, new Rect(center - 8 - width, top + 3 - i * 4, width, 2), trimColor);
            }
        }

        private static Rect Inset(Rect r, float inset) =>
            new Rect(r.x + inset, r.y + inset, r.width - inset * 2, r.height - inset * 2);

        private static void Quad(VertexHelper vh, Rect r, Color tint) => Plate(vh, r, 0, tint);

        private static void Plate(VertexHelper vh, Rect r, float cut, Color tint)
        {
            var points = new[]
            {
                new Vector2(r.x + cut, r.y), new Vector2(r.xMax - cut, r.y),
                new Vector2(r.xMax, r.y + cut), new Vector2(r.xMax, r.yMax - cut),
                new Vector2(r.xMax - cut, r.yMax), new Vector2(r.x + cut, r.yMax),
                new Vector2(r.x, r.yMax - cut), new Vector2(r.x, r.y + cut)
            };
            var start = vh.currentVertCount;
            vh.AddVert(r.center, tint, Vector2.zero);
            for (var i = 0; i < points.Length; i++) vh.AddVert(points[i], tint, Vector2.zero);
            for (var i = 0; i < points.Length; i++) vh.AddTriangle(start, start + i + 1, start + (i + 1) % 8 + 1);
        }
    }
}
