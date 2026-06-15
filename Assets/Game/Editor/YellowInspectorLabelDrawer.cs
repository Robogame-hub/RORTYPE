using RorType.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace RorType.Gameplay.Editor
{
    [CustomPropertyDrawer(typeof(YellowInspectorLabelAttribute))]
    public sealed class YellowInspectorLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var previousContentColor = GUI.contentColor;
            GUI.contentColor = Color.yellow;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.contentColor = previousContentColor;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    [CustomPropertyDrawer(typeof(RedInspectorLabelAttribute))]
    public sealed class RedInspectorLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var previousContentColor = GUI.contentColor;
            GUI.contentColor = Color.red;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.contentColor = previousContentColor;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
