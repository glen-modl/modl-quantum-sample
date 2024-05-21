using UnityEngine;

namespace Modl.Editor.UI
{
    /// <summary>
    ///   <para>Scope for managing the GUI color of elements.</para>
    /// </summary>
    public class GUIColorScope : GUI.Scope
    {
        private readonly Color _priorColor;
        private readonly Color _priorBackgroundColor;

        /// <summary>
        ///   <para>Creates an GUIColorScope that sets the GUI.color, and restores it on close.</para>
        /// </summary>
        public GUIColorScope(Color color, bool includingBackgroundColor = false)
        {
            _priorColor = GUI.color;
            _priorBackgroundColor = GUI.backgroundColor;
            GUI.color = color;
            if (includingBackgroundColor)
            {
                GUI.backgroundColor = color;
            }
        }

        protected override void CloseScope()
        {
            GUI.color = _priorColor;
            GUI.backgroundColor = _priorBackgroundColor;
        }
    }
}