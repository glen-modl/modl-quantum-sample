using UnityEditor;
using UnityEngine;

namespace Modl.Editor.UI
{
    public static class UtilsEditorUI
    {
        public static void GuiLine( int i_height = 1, params GUILayoutOption[] options)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, i_height, options);

            rect.height = i_height;

            EditorGUI.DrawRect(rect, new Color ( 0.5f,0.5f,0.5f, 1 ) );
        }
        
        public static void GuiLine( Color color, params GUILayoutOption[] options)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, options);

            EditorGUI.DrawRect(rect, color);
        }
        
        
        //public static void Space(float width, bool expand) => GUILayoutUtility.GetRect(width, width, 0, 1, GUILayout.ExpandWidth(expand));
        
        public static void Space(float width, float height, bool expand) => GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(expand));
    }
}