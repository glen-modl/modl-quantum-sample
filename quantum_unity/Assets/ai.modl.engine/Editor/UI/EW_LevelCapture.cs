using System;
using UnityEditor;
using UnityEngine;
using static Modl.LevelCaptureUtils;

namespace Modl.Editor.UI
{
    public class EW_LevelCapture : SceneView
    {
        private static Color kSceneViewMidLight = new Color(57f / 500f, 0.125f, 0.133f, 1f);
        
        private Vector2 _offset;
        private ViewDirection _direction;
        private string _filename;
        private bool _autoMergeSections = true;

        public override void OnEnable()
        {
            //NOTE: Due to the EditorWindowTitle attribute on SceneView, overriding it Logs an error related to the icon,
            //disabling the logger during initialisation avoids that error message.
            var logEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            base.OnEnable();
            Debug.unityLogger.logEnabled = logEnabled;

            //Reinitialize level capture settings from editor prefs.
            _offset = LevelOffset;
            size = LevelSize;
            _direction = LevelDirection;
            _autoMergeSections = true;
            
            SetPivotFromDirectionAndOffset();
            
            LookAt(pivot, DirectionRotations[(int)_direction], size, true, true);
            isRotationLocked = true;
            
            // orthographic = true;
            // drawGizmos = false;
            // sceneViewState.SetAllEnabled(false);
            // autoRepaintOnSceneChange = false;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            
            //Save level capture settings to editor prefs.
            LevelOffset = _offset;
            LevelSize = size;
            LevelDirection = _direction;
        }
        
#if UNITY_2021_3_OR_NEWER
        protected override void OnSceneGUI()
#else
        protected override void OnGUI()
#endif        
        {
            var currentIsKeyEvent = Event.current.isKey;
            var currentIsMouseEvent = Event.current.isMouse;
            var tmpTool = Tools.current;
            Tools.current = Tool.None;

            var eventType = Event.current.type;
            if (currentIsKeyEvent)
            {
                //Ignore default keyboard input handling
                Event.current.type = EventType.Ignore;
            }
            
            if (currentIsMouseEvent && Event.current.button == 0)
            {
                //Make sure mouse 0 doesn't select things!
                Event.current.type = EventType.Ignore;
            }
            
#if UNITY_2021_3_OR_NEWER
                base.OnSceneGUI();
#else
                base.OnGUI();
#endif

            //Reset event
            Event.current.type = eventType;

            using(var scope = new EditorGUI.ChangeCheckScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(20f);
                    
                    using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            _direction = (ViewDirection) EditorGUILayout.EnumPopup(new GUIContent("View Direction"), _direction);
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            size = EditorGUILayout.FloatField(new GUIContent("Orthographic Size"), size);
                        }
                        
                        using (new GUILayout.HorizontalScope())
                        {
                            _offset = EditorGUILayout.Vector2Field(new GUIContent("Offset"), To2DOffset(_direction, pivot));
                        }

                        if(scope.changed)
                        {
                            SetPivotFromDirectionAndOffset();
                            LookAt(pivot, DirectionRotations[(int) _direction], this.size, true);
                        }    
                    }
                    
                    EditorGUILayout.Space(20f);
                }
            }
            
            GUILayout.FlexibleSpace();
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(20f);

                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        _filename = EditorGUILayout.TextField(new GUIContent("Level Name:"), _filename);    
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Capture"))
                        {
                            if (string.IsNullOrWhiteSpace(_filename))
                            {
                                EditorUtility.DisplayDialog("Level Name Empty",
                                    "Please define a level name before capturing", "OK");
                            }
                            else
                            {
                                CaptureSceneView(_autoMergeSections);
                            }
                        }
                        
                        _autoMergeSections = GUILayout.Toggle(_autoMergeSections, "Auto Merge Captured Sections", GUILayout.Width(200f));
                    }
                }

                EditorGUILayout.Space(10f);
            }
            
            
            Tools.current = tmpTool;
        }

         private void CaptureSceneView(bool autoMerge)
         {
             GameObject tempLight = null;
             if(!sceneLighting)
             {
                 //Recreating what the SceneView does in OnGUI when sceneLighting is disabled.
                 tempLight = new GameObject("tempLight");
                 tempLight.hideFlags = HideFlags.HideAndDontSave;
                 var lightComp = tempLight.AddComponent<Light>();
                 lightComp.color = Color.white;
                 lightComp.type = LightType.Directional;
                 tempLight.transform.rotation = camera.transform.rotation;
                 UnityEditorInternal.InternalEditorUtility.SetCustomLighting(new []{lightComp}, kSceneViewMidLight);
             }
                
             CaptureImageAndCoordinates(_filename, camera, _direction, _offset, 1000f, autoMerge);
                
             if(!sceneLighting)
             {
                 //Cleaning it up like SceneView does in OnGUI when sceneLighting is disabled.
                 UnityEditorInternal.InternalEditorUtility.RemoveCustomLighting();
                 DestroyImmediate(tempLight);
             }
         }

        private void SetPivotFromDirectionAndOffset()
        {
            //TODO: We may want a way for users to set custom iso-metric perspectives, but then we will have to deal with that on the platform as well.
            switch (_direction)
            {
                case ViewDirection.TopView:
                    pivot = new Vector3(_offset.x, 0, _offset.y);
                    break;
                case ViewDirection.BottomView:
                    pivot = new Vector3(_offset.x, 0, -_offset.y);
                    break;
                case ViewDirection.FrontView:
                    pivot = new Vector3(-_offset.x, _offset.y, 0);
                    break;
                case ViewDirection.BackView:
                    pivot = new Vector3(_offset.x, _offset.y, 0);
                    break;
                case ViewDirection.LeftView:
                    pivot = new Vector3(0, _offset.y, -_offset.x);
                    break;
                case ViewDirection.RightView:
                    pivot = new Vector3(0, _offset.y, _offset.x);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #region Editor Prefs

        private static Vector2 LevelOffset
        {
            get => new Vector2(EditorPrefs.GetFloat("modl_leveOffset_x", 0), EditorPrefs.GetFloat("modl_levelOffset_y", 0));
            set
            {
                EditorPrefs.SetFloat("modl_levelOffset_x", value.x);
                EditorPrefs.SetFloat("modl_levelOffset_y", value.y);
            }
        }
        
        private static float LevelSize
        {
            get => EditorPrefs.GetFloat("modl_levelSize", 10);
            set => EditorPrefs.SetFloat("modl_levelSize", value);
        }

        private static ViewDirection LevelDirection
        {
            get => (ViewDirection)EditorPrefs.GetInt("modl_levelView", 0);
            set => EditorPrefs.SetInt("modl_levelView", (int)value);
        }

        #endregion
    }
}