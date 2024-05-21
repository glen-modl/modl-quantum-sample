using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modl.Internal
{
    public class ModlDebugMessage : MonoBehaviour
    {
        private GUIContent _text;
        private Color _color;

        private float _fontSize;
        
        private bool _isVisible;
        private float _fadeSpeed;
        private float _fadeTimeLeft;
        
        private static GUIStyle GUILabelStyle;
        private static GUIStyle GizmoStyle;

        private GameObject _followThis;
        
        private void Awake()
        {
            GUILabelStyle = new GUIStyle {alignment = TextAnchor.MiddleCenter, fontSize = 40};
            GizmoStyle = new GUIStyle {alignment = TextAnchor.MiddleCenter, fontSize = 16};
        }


        public void ShowMessage(string msg, string loggedMsg, GameObject followThis = null, float fadeTime = 5f, LogType logType = LogType.Log)
        {
            ShowMessage(msg, loggedMsg, Color.white, followThis, fadeTime, logType);
        }
        
        public void ShowMessage(string msg, GameObject followThis = null, float fadeTime = 5f, LogType logType = LogType.Log)
        {
            ShowMessage(msg, Color.white, followThis, fadeTime, logType);
        }

        public void ShowMessage(string msg, Color color, GameObject followThis = null, float fadeTime = 5f, LogType logType = LogType.Log)
        {
            ShowMessage(msg, msg, color, followThis, fadeTime,logType);
        }
        
        public void ShowMessage(string msg, string loggedMsg, Color color, GameObject followThis = null, float fadeTime = 5f, LogType logType = LogType.Log)
        {
            Debug.LogFormat(logType, LogOption.None, null, loggedMsg);
            //Debug.Log(loggedMsg);
            _isVisible = true;
            _fadeTimeLeft = fadeTime;
            _fadeSpeed = color.a / fadeTime;
            _text = new GUIContent(msg);
            _color = color;
            _followThis = followThis;
        }
        
        private void OnGUI()
        {
            if (_isVisible)
            {
                if (Application.isEditor)
                {
                    GUILabelStyle.normal.textColor = _color;
                    var size = GUILabelStyle.CalcSize(_text);
                    
                    var scale = (Screen.width / 2f) / size.x;
                    GUILabelStyle.fontSize = Mathf.RoundToInt(GUILabelStyle.fontSize *  scale);

                    GUI.Label(
                        new Rect(Screen.width / 2f - size.x / 2f, Screen.height / 2f - size.y / 2f, size.x, size.y),
                        _text, GUILabelStyle);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_isVisible)
            {
#if UNITY_EDITOR
                var size = GizmoStyle.CalcSize(new GUIContent(_text));
                GizmoStyle.contentOffset = new Vector2(-size.x / 4f, 0);

                GizmoStyle.normal.textColor = _color;
                Handles.Label(transform.position + transform.up, _text, GizmoStyle);
#endif
            }
        }

        private void LateUpdate()
        {
            if (_followThis)
            {
                transform.position = _followThis.transform.position;    
            }

            if (_fadeTimeLeft > 0)
            {
                _color.a -= _fadeSpeed * Time.unscaledDeltaTime;
                _fadeTimeLeft -= Time.unscaledDeltaTime;
            }
            else
            {
                _color.a = 0;
                _isVisible = false;
            }
        }
    }
}