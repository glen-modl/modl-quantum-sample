using UnityEditor;
using UnityEngine;

namespace Modl.Editor
{
    public static class ModlStyles
    {
        private const string DARK_SKIN = "Skins/modl_dark";
        private static GUISkin _skin;
        private static Texture2D _errorRowBackground;

        public static GUIStyle Title => GetStyle("title");
        public static GUIStyle Subtitle => GetStyle("subtitle");

        public static GUIStyle GetStyledFoldout(GUIStyle refStyle)
        {
            var style = new GUIStyle(EditorStyles.foldout);
            style.font = refStyle.font;
            style.fontSize = refStyle.fontSize;
            style.fontStyle = refStyle.fontStyle;
            return style;
        }

        public static GUIStyle H1 => GetStyle("H1");
        public static GUIStyle H2 => GetStyle("H2");
        public static GUIStyle H3 => GetStyle("H3");
        public static GUIStyle H4 => GetStyle("H4");
        public static GUIStyle H5 => GetStyle("H5");
        public static GUIStyle RowOdd => GetStyle("odd_row");
        public static GUIStyle RowEven => GetStyle("even_row");
        public static GUIStyle RowInvalid => GetStyle("invalid_row");
        
        public static GUIStyle HeaderRow => GetStyle("header_row");
        
        
        public static GUIStyle TabbedLabel => GetStyle("tabbed_label");
        public static GUIStyle MenuTab => GetStyle("menu_tab");
        
        // TODO handle light skin
        private static GUIStyle GetStyle(string key)
        {
            if (_skin is null)
            {
                _skin = Resources.Load<GUISkin>(DARK_SKIN);
            }

            return _skin.GetStyle(key);
        }
    }
}
