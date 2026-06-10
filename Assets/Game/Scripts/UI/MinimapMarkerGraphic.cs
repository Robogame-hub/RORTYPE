using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapMarkerGraphic : MaskableGraphic
    {
        [SerializeField] private MinimapMarkerShape shape = MinimapMarkerShape.Square;

        public void SetAppearance(MinimapMarkerShape nextShape, Color nextColor)
        {
            var hasShapeChanged = shape != nextShape;
            var hasColorChanged = color != nextColor;

            shape = nextShape;
            color = nextColor;

            if (hasShapeChanged || hasColorChanged)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            var halfWidth = rect.width * 0.5f;
            var halfHeight = rect.height * 0.5f;
            var halfMin = Mathf.Min(halfWidth, halfHeight);

            switch (shape)
            {
                case MinimapMarkerShape.Cross:
                    AddRotatedQuad(vh, Vector2.zero, new Vector2(halfMin * 1.7f, halfMin * 0.34f), 45f);
                    AddRotatedQuad(vh, Vector2.zero, new Vector2(halfMin * 1.7f, halfMin * 0.34f), -45f);
                    break;
                case MinimapMarkerShape.Square:
                    AddQuad(vh, new Vector2(-halfMin, -halfMin), new Vector2(halfMin, halfMin));
                    break;
                case MinimapMarkerShape.Triangle:
                    AddTriangle(
                        vh,
                        new Vector2(0f, halfMin),
                        new Vector2(-halfMin, -halfMin),
                        new Vector2(halfMin, -halfMin));
                    break;
                case MinimapMarkerShape.Circle:
                    AddCircle(vh, halfMin, 20);
                    break;
                case MinimapMarkerShape.Arrow:
                    AddQuad(vh, new Vector2(-halfMin * 0.18f, -halfMin), new Vector2(halfMin * 0.18f, halfMin * 0.1f));
                    AddTriangle(
                        vh,
                        new Vector2(0f, halfMin),
                        new Vector2(-halfMin * 0.7f, halfMin * 0.05f),
                        new Vector2(halfMin * 0.7f, halfMin * 0.05f));
                    break;
            }
        }

        private void AddCircle(VertexHelper vh, float radius, int segments)
        {
            var center = UIVertex.simpleVert;
            center.color = color;
            center.position = Vector3.zero;

            var centerIndex = vh.currentVertCount;
            vh.AddVert(center);

            for (var index = 0; index <= segments; index++)
            {
                var angle = (Mathf.PI * 2f * index) / segments;
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                vh.AddVert(vertex);

                if (index == 0)
                {
                    continue;
                }

                vh.AddTriangle(centerIndex, centerIndex + index, centerIndex + index + 1);
            }
        }

        private void AddRotatedQuad(VertexHelper vh, Vector2 center, Vector2 size, float angleDegrees)
        {
            var halfSize = size * 0.5f;
            var rotation = Quaternion.Euler(0f, 0f, angleDegrees);

            var bottomLeft = center + (Vector2)(rotation * new Vector2(-halfSize.x, -halfSize.y));
            var topLeft = center + (Vector2)(rotation * new Vector2(-halfSize.x, halfSize.y));
            var topRight = center + (Vector2)(rotation * new Vector2(halfSize.x, halfSize.y));
            var bottomRight = center + (Vector2)(rotation * new Vector2(halfSize.x, -halfSize.y));

            AddQuad(vh, bottomLeft, topLeft, topRight, bottomRight);
        }

        private void AddQuad(VertexHelper vh, Vector2 min, Vector2 max)
        {
            AddQuad(
                vh,
                new Vector2(min.x, min.y),
                new Vector2(min.x, max.y),
                new Vector2(max.x, max.y),
                new Vector2(max.x, min.y));
        }

        private void AddQuad(VertexHelper vh, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
        {
            var startIndex = vh.currentVertCount;
            AddVertex(vh, bottomLeft);
            AddVertex(vh, topLeft);
            AddVertex(vh, topRight);
            AddVertex(vh, bottomRight);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        private void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            var startIndex = vh.currentVertCount;
            AddVertex(vh, a);
            AddVertex(vh, b);
            AddVertex(vh, c);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        }

        private void AddVertex(VertexHelper vh, Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }
    }
}
