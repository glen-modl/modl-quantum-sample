using Modl.Internal.Utils;
using Modl.Internal.Utils.Attributes;
using UnityEditor;
using UnityEngine;

namespace Modl.Editor.UI {
	[CustomPropertyDrawer(typeof(ScenePickerAttribute), true)]
	public class PD_Scene : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => 0;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var oldScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);

			EditorGUI.BeginChangeCheck();
			var newScene = EditorGUILayout.ObjectField("scene", oldScene, typeof(SceneAsset), false) as SceneAsset;

			if (!EditorGUI.EndChangeCheck())
			{
				return;
			}
			
			var newPath = AssetDatabase.GetAssetPath(newScene);
			property.stringValue = newPath;
		}
	}
}