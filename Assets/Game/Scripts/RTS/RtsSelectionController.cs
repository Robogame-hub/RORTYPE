using System;
using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;
        [SerializeField] private RectTransform selectionBox;
        [SerializeField, Min(0.05f)] private float doubleClickWindow = 0.28f;
        [SerializeField, Min(4f)] private float minimumDragPixels = 12f;
        [SerializeField, Min(0.5f)] private float formationSpacing = 3f;

        private readonly List<RtsUnit> selectedUnits = new List<RtsUnit>();
        private readonly List<RtsUnit>[] controlGroups =
        {
            new List<RtsUnit>(), new List<RtsUnit>(), new List<RtsUnit>(), new List<RtsUnit>(), new List<RtsUnit>(),
            new List<RtsUnit>(), new List<RtsUnit>(), new List<RtsUnit>(), new List<RtsUnit>(), new List<RtsUnit>()
        };

        private Vector2 dragStart;
        private bool dragging;
        private float lastGroundClickTime = float.NegativeInfinity;
        private int selectionVersion;
        private readonly RaycastHit[] raycastBuffer = new RaycastHit[32];

        public event Action SelectionChanged;

        public IReadOnlyList<RtsUnit> SelectedUnits => selectedUnits;
        public int SelectionVersion => selectionVersion;

        private void Awake()
        {
            gameCamera ??= Camera.main;
            if (selectionBox != null)
            {
                selectionBox.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            HandleControlGroups();
            if (gameCamera == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                dragStart = Input.mousePosition;
                dragging = true;
            }

            if (dragging && Input.GetMouseButton(0))
            {
                UpdateSelectionBox();
            }

            if (dragging && Input.GetMouseButtonUp(0))
            {
                HandlePrimaryMouseRelease();
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                TryUseAbility(0);
            }
        }

        public void SelectAllPlayerUnits()
        {
            var units = RtsUnit.AllUnits;
            var result = new List<RtsUnit>();
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].IsControllableByPlayer)
                {
                    result.Add(units[i]);
                }
            }
            SetSelection(result);
        }

        public bool TryGetCursorWorldPoint(out Vector3 point)
        {
            point = Vector3.zero;
            if (gameCamera == null)
            {
                return false;
            }

            var ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out var distance))
            {
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        public void TryUseAbility(int abilityIndex)
        {
            if (selectedUnits.Count != 1 || !TryGetCursorWorldPoint(out var worldPoint))
            {
                return;
            }

            var abilities = selectedUnits[0].GetAbilities();
            if (abilityIndex >= 0 && abilityIndex < abilities.Length && abilities[abilityIndex] != null)
            {
                abilities[abilityIndex].TryActivate(selectedUnits[0], worldPoint);
            }
        }

        private void HandlePrimaryMouseRelease()
        {
            dragging = false;
            if (selectionBox != null)
            {
                selectionBox.gameObject.SetActive(false);
            }

            var mousePosition = (Vector2)Input.mousePosition;
            if (Vector2.Distance(dragStart, mousePosition) >= minimumDragPixels)
            {
                SelectUnitsInRectangle(dragStart, mousePosition);
                return;
            }

            var unit = FindUnitUnderCursor();
            if (unit != null && unit.IsControllableByPlayer)
            {
                SetSelection(new[] { unit });
                return;
            }

            if (selectedUnits.Count == 0)
            {
                return;
            }

            if (unit != null && selectedUnits[0].IsEnemyOf(unit))
            {
                IssueAttack(unit);
                return;
            }

            if (!TryGetCursorWorldPoint(out var worldPoint))
            {
                return;
            }

            var isAssaultMove = Time.time - lastGroundClickTime <= doubleClickWindow;
            lastGroundClickTime = Time.time;
            IssueMovement(worldPoint, isAssaultMove);
        }

        private RtsUnit FindUnitUnderCursor()
        {
            if (gameCamera == null)
            {
                return null;
            }

            var ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            var hitCount = Physics.RaycastNonAlloc(ray, raycastBuffer, 500f);
            if (hitCount == 0)
            {
                return null;
            }

            Array.Sort(raycastBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);
            for (var i = 0; i < hitCount; i++)
            {
                var collider = raycastBuffer[i].collider;
                var unit = collider != null ? collider.GetComponentInParent<RtsUnit>() : null;
                if (unit != null)
                {
                    return unit;
                }
            }

            return null;
        }

        private void IssueMovement(Vector3 point, bool assault)
        {
            var commandUnits = new List<RtsUnit>();
            for (var i = 0; i < selectedUnits.Count; i++)
            {
                var unit = selectedUnits[i];
                if (unit != null && unit.IsAlive)
                {
                    commandUnits.Add(unit);
                }
            }

            if (commandUnits.Count == 0)
            {
                return;
            }

            var centroid = Vector3.zero;
            for (var i = 0; i < commandUnits.Count; i++)
            {
                centroid += commandUnits[i].transform.position;
            }
            centroid /= commandUnits.Count;

            var forward = point - centroid;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = gameCamera != null ? gameCamera.transform.forward : Vector3.forward;
                forward.y = 0f;
            }
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);

            // Preserve lateral order so a group does not cross through itself on the way to a formation.
            commandUnits.Sort((left, rightUnit) =>
                Vector3.Dot(left.transform.position - centroid, right)
                    .CompareTo(Vector3.Dot(rightUnit.transform.position - centroid, right)));

            var columns = Mathf.CeilToInt(Mathf.Sqrt(commandUnits.Count));
            var rows = Mathf.CeilToInt(commandUnits.Count / (float)columns);
            for (var i = 0; i < commandUnits.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var lateralOffset = (column - (columns - 1) * 0.5f) * formationSpacing;
                var depthOffset = (row - (rows - 1) * 0.5f) * formationSpacing;
                var formationPoint = point + right * lateralOffset + forward * depthOffset;

                if (assault)
                {
                    commandUnits[i].IssueAssaultMove(formationPoint);
                }
                else
                {
                    commandUnits[i].IssueForcedMove(formationPoint);
                }
            }
        }

        private void IssueAttack(RtsUnit enemy)
        {
            for (var i = 0; i < selectedUnits.Count; i++)
            {
                selectedUnits[i]?.IssueAttack(enemy);
            }
        }

        private void SelectUnitsInRectangle(Vector2 from, Vector2 to)
        {
            var minimum = Vector2.Min(from, to);
            var maximum = Vector2.Max(from, to);
            var result = new List<RtsUnit>();
            var units = RtsUnit.AllUnits;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.IsControllableByPlayer)
                {
                    continue;
                }

                var screenPoint = gameCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPoint.z <= 0f)
                {
                    continue;
                }

                var screenPosition = (Vector2)screenPoint;
                if (screenPosition.x >= minimum.x && screenPosition.x <= maximum.x &&
                    screenPosition.y >= minimum.y && screenPosition.y <= maximum.y)
                {
                    result.Add(unit);
                }
            }
            SetSelection(result);
        }

        private void SetSelection(IReadOnlyList<RtsUnit> nextSelection)
        {
            for (var i = 0; i < selectedUnits.Count; i++)
            {
                selectedUnits[i]?.SetSelected(false);
            }
            selectedUnits.Clear();

            for (var i = 0; i < nextSelection.Count; i++)
            {
                var unit = nextSelection[i];
                if (unit != null && unit.IsControllableByPlayer && !selectedUnits.Contains(unit))
                {
                    selectedUnits.Add(unit);
                    unit.SetSelected(true);
                }
            }

            selectionVersion++;
            SelectionChanged?.Invoke();
        }

        private void HandleControlGroups()
        {
            if (Input.GetKeyDown(KeyCode.A) && Input.GetKey(KeyCode.LeftControl))
            {
                SelectAllPlayerUnits();
            }

            for (var i = 0; i < controlGroups.Length; i++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha0 + i);
                if (!Input.GetKeyDown(key))
                {
                    continue;
                }

                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    controlGroups[i].Clear();
                    controlGroups[i].AddRange(selectedUnits);
                }
                else
                {
                    controlGroups[i].RemoveAll(unit => unit == null || !unit.IsAlive);
                    SetSelection(controlGroups[i]);
                }
            }
        }

        private void UpdateSelectionBox()
        {
            if (selectionBox == null)
            {
                return;
            }

            var current = (Vector2)Input.mousePosition;
            var minimum = Vector2.Min(dragStart, current);
            var maximum = Vector2.Max(dragStart, current);
            selectionBox.gameObject.SetActive(true);
            selectionBox.position = minimum;
            selectionBox.sizeDelta = maximum - minimum;
        }

        private void OnGUI()
        {
            if (selectionBox != null || !dragging || !Input.GetMouseButton(0))
            {
                return;
            }

            var current = (Vector2)Input.mousePosition;
            var minimum = Vector2.Min(dragStart, current);
            var maximum = Vector2.Max(dragStart, current);
            var rect = Rect.MinMaxRect(minimum.x, Screen.height - maximum.y, maximum.x, Screen.height - minimum.y);
            var previousColor = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }
    }
}
