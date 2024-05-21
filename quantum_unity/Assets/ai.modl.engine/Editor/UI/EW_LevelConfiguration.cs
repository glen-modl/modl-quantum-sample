using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Modl.Editor.UI
{
    public class EW_LevelConfiguration : EditorWindow
    {
        private ModlLevelConfigurationExport _exportSelection = new ModlLevelConfigurationExport{MapList = new List<LevelConfiguration>()};

        private static readonly Dictionary<string, Texture2D> LevelImageCache = new Dictionary<string, Texture2D>();

        private bool _didLoadFiles;
        private Vector2 _selectedFilePos;
        private bool _configIsDirty;

        private int _lastActiveIndex = -1;
        private ReorderableList _levelConfigurationList;
        
        private readonly GUIStyle _header = new GUIStyle();

        private bool _mouseOverPreview = false;
        private bool _popupPreviewOpen = false;
        private Rect _previewRect;
        private int _previewIndex = -1;
        private Texture2D _levelImagePreviewPopupTexture;

        private static class LevelConfigLabels
        {
            //Popups
            public const string EmptyLevelPopupTitle = "Empty Level Load Commands";
            public const string EmptyLevelPopupContent = "You have level configurations with empty Level Load Commands, save anyway?";
            public const string EmptyNamePopupTitle = "Empty Level Name";
            public const string EmptyNamePopupContent = "You have level configurations with empty Names, do you want to discard them?";
            public const string UnsavedChangesPopupTitle = "Save changes to level configuration?";
            public const string UnsavedChangesPopupContent = "You have unsaved level configuration changes, would you like to save them?";
            
            //GUI
            public const string ConfigHeader = "Level Configurations";
            public const string ListHeader = "Configure levels for test build:";

            //Editor fields
            public const string LevelName = "Name";
            public const string LevelCommands = "Level Load Commands";
            public const string LevelServerCommands = "Level Load Commands";
            public const string LevelImagePath = "Level Image Path";

            //Buttons
            public const string ConfigSaveButtonLabel = "Save Configuration";
            public const string GameServerCommandToggle = "For Game Server";
            public const string OpenImageFileButtonAndTitle = "Select level image";
            
            //Generic menu items
            public const string AddDirectionalLighting = "Using Scene View Lighting";
            public const string UseInGameLighting = "Using In Game Lighting";
            public const string AddEmptyLabel = "Add Empty Row";
            public const string NoPreviewLabel = "No \nimage \npreview";
            public const string CaptureAllScenesLabel = "Capture All Scenes in Build Settings";
            public const string PixelsPerUnitLabel = "pixel per unit";
            public const string CaptureCurrentSceneLabel = "Capture Current Scene";
            public const string FailedToLoadLevelConfigs = "Failed to load level config!";
            public const string FailedToLoadLevelConfigContent = "Failed to load level configurations from 'modl_map_config.json'\nWhat would you like to do with the file?";
        }
        private static class LevelConfigToolTips
        {
            //TODO: ADD MORE TOOLTIPS?
            public const string ConfigHeaderTooltip = "The different level configurations modl:test will use when testing the game.";
            public const string LevelImagePathTooltip = "This is the image which will be used for visualizing a test run on the web-app.";
            public const string LevelImageLocationTooltip = "The image must be inside the capturedLevels folder.";
            public const string LevelCommandTooltip = "These are the command line arguments used by modl:test when running the game client for a test for the specified level.";
        }
        

        public void OnEnable()
        {
            _header.fontSize = 15;
            _header.fontStyle = FontStyle.Bold;
            if (EditorGUIUtility.isProSkin)
            {
                _header.normal.textColor = Color.white;
            }
            else
            {
                _header.normal.textColor = Color.black;
            }
            
            //Makes sure that we don't repopulate the window just because it regained focus. 
            if (!_didLoadFiles)
            {
                LoadLevelConfigurationsFromFile();
                _didLoadFiles = true;
            }

            SetupReorderableList();
        }
        
        /// <summary>
        /// Captures the window closing event, checks for unsaved changes, and tries to save.
        /// </summary>
        public void OnDestroy()
        {
            if (DoesUserWantOrNeedToSave())
            {
                SaveConfiguration();
            }
        }

        private void OnGUI()
        {
            using (var s = new EditorGUILayout.ScrollViewScope(_selectedFilePos))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    new GUIContent(LevelConfigLabels.ConfigHeader, 
                        LevelConfigToolTips.ConfigHeaderTooltip), _header,
                    GUILayout.MaxWidth(200));
                EditorGUILayout.Space();
                
                using (var c = new EditorGUI.ChangeCheckScope())
                {
                    _mouseOverPreview = false;
                    _levelConfigurationList.DoLayoutList();
                    
                    if (!_mouseOverPreview && Event.current.type == EventType.Repaint)
                    {
                        ChangePreviewIndexAndClosePopup(-1);
                    }

                    if (c.changed)
                    {
                        _configIsDirty = true;
                    }
                }

                _selectedFilePos = s.scrollPosition;
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!_configIsDirty))
            {
                if (GUILayout.Button(LevelConfigLabels.ConfigSaveButtonLabel))
                {
                    SaveConfiguration();
                }    
            }
            
            //If the popup is closed and the mouse is over a preview, refocus the window.
            if(!_popupPreviewOpen && _mouseOverPreview)
            {
                Focus();
            }
            
            OpenPopupPreviewIfNeeded();
        }

        private void SetupReorderableList()
        {
            _levelConfigurationList = new ReorderableList(_exportSelection.MapList, typeof(LevelConfiguration));
            _levelConfigurationList.onReorderCallback += list =>
            {
                _configIsDirty = true;
            };
            
            _levelConfigurationList.onAddDropdownCallback += (rect, list) =>
            {
                var menu = new GenericMenu ();
                GenerateMenuItemsFor("Low", 1f, menu);
                GenerateMenuItemsFor("Medium", 5f, menu);
                GenerateMenuItemsFor("High", 10f, menu);
                GenerateMenuItemsFor("Ultra", 20f, menu);
                
                menu.AddItem(new GUIContent(LevelConfigLabels.AddEmptyLabel), false, () =>
                {
                    _exportSelection.MapList.Add(new LevelConfiguration());
                });
                
                menu.ShowAsContext();
            };

            _levelConfigurationList.drawHeaderCallback += (rect) =>
            {
                EditorGUI.LabelField(rect, new GUIContent(LevelConfigLabels.ListHeader));
            };

            const float linesOfContent = 3f;
            _levelConfigurationList.elementHeight = linesOfContent * EditorGUIUtility.singleLineHeight +
                                                    linesOfContent * EditorGUIUtility.standardVerticalSpacing +
                                                    EditorGUIUtility.standardVerticalSpacing;
            var imageSize = _levelConfigurationList.elementHeight - 2 * EditorGUIUtility.standardVerticalSpacing;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            _levelConfigurationList.drawElementCallback += (rect, index, active, focused) =>
            {
                //Draw Name
                GUI.SetNextControlName("activeLevelName");
                var lineWidth = rect.width - (imageSize + EditorGUIUtility.standardVerticalSpacing);
                var nameRect = new Rect(rect.position +
                                        Vector2.right * (imageSize + EditorGUIUtility.standardVerticalSpacing) +
                                        Vector2.up * EditorGUIUtility.standardVerticalSpacing,
                    new Vector2(lineWidth, lineHeight));
                _exportSelection.MapList[index].Name =
                    EditorGUI.TextField(nameRect, LevelConfigLabels.LevelName, _exportSelection.MapList[index].Name);

                if (active && _lastActiveIndex != index)
                {
                    _lastActiveIndex = index;
                }
                
                //Draw Image File Selection
                var fileButtonWidth = 150f;
                var imagePathRect = new Rect(nameRect.position +
                                             Vector2.up * nameRect.height +
                                             Vector2.up * EditorGUIUtility.standardVerticalSpacing,
                    new Vector2(lineWidth - fileButtonWidth - EditorGUIUtility.standardVerticalSpacing, lineHeight));
                var fileButtonRect = new Rect(
                    imagePathRect.position +
                    Vector2.right * (imagePathRect.width + EditorGUIUtility.standardVerticalSpacing),
                    new Vector2(fileButtonWidth, lineHeight));

                EditorGUI.BeginDisabledGroup(true);
                _exportSelection.MapList[index].MapImageFile = EditorGUI.TextField(imagePathRect,
                    new GUIContent(LevelConfigLabels.LevelImagePath,
                        LevelConfigToolTips.LevelImagePathTooltip),
                    _exportSelection.MapList[index].MapImageFile);
                EditorGUI.EndDisabledGroup();
                if (GUI.Button(fileButtonRect, new GUIContent($"{LevelConfigLabels.OpenImageFileButtonAndTitle}...", LevelConfigToolTips.LevelImageLocationTooltip)))
                {
                    var image = EditorUtility.OpenFilePanel(LevelConfigLabels.OpenImageFileButtonAndTitle ,
                        Directory.Exists(LevelCaptureUtils.LevelCaptureFolder) ? LevelCaptureUtils.LevelCaptureFolder : "", "png");
                    if (!string.IsNullOrWhiteSpace(image) && LevelCaptureUtils.CheckFilePairExists(image))
                    {
                        var relativePath = LevelCaptureUtils.GetRelativePath(Application.dataPath, image);
                        _exportSelection.MapList[index].MapImageFile = Path.Combine(
                            Path.GetDirectoryName(relativePath) ?? string.Empty,
                            Path.GetFileNameWithoutExtension(image));
                    }
                    else if (string.IsNullOrWhiteSpace(image))
                    {
                        //do nothing
                        //this is here so that the warning doesn't appear if the user presses the Select Level Image button then presses cancel
                    }
                    else
                    {
                        Debug.LogWarning($"The level image must be inside the {LevelCaptureUtils.LevelCaptureFolder} folder");
                    }
                }
                
                //Draw Load commands
                var noServerCommands = string.IsNullOrWhiteSpace(_exportSelection.MapList[index].ServerCommands);
                var toggleWidth = 150f;
                var loadCommandRect = new Rect(imagePathRect.position +
                                               Vector2.up * imagePathRect.height +
                                               Vector2.up * EditorGUIUtility.standardVerticalSpacing,
                    new Vector2(lineWidth - toggleWidth - EditorGUIUtility.standardVerticalSpacing, lineHeight));
                var loadCommandServerCheckRect = new Rect(
                    loadCommandRect.position +
                    Vector2.right * (loadCommandRect.width + EditorGUIUtility.standardVerticalSpacing),
                    new Vector2(toggleWidth, lineHeight));

                //TODO: outline with Color.red highlight if it's not filled.
                if (noServerCommands)
                {
                    if (string.IsNullOrEmpty(_exportSelection.MapList[index].Commands))
                    {
                        EditorGUI.DrawRect(loadCommandRect, Color.red);
                    }
                    
                    _exportSelection.MapList[index].Commands = EditorGUI.TextField(loadCommandRect,
                        new GUIContent( LevelConfigLabels.LevelCommands, LevelConfigToolTips.LevelCommandTooltip),
                        _exportSelection.MapList[index].Commands);
                }
                else
                {
                    if (string.IsNullOrEmpty(_exportSelection.MapList[index].ServerCommands))
                    {
                        EditorGUI.DrawRect(loadCommandRect, Color.red);
                    }
                    
                    _exportSelection.MapList[index].ServerCommands = EditorGUI.TextField(loadCommandRect,
                        new GUIContent(LevelConfigLabels.LevelServerCommands, LevelConfigToolTips.LevelCommandTooltip),
                        _exportSelection.MapList[index].ServerCommands);
                }

                EditorGUI.BeginChangeCheck();
                noServerCommands = !EditorGUI.ToggleLeft(loadCommandServerCheckRect, LevelConfigLabels.GameServerCommandToggle, !noServerCommands);

                if (EditorGUI.EndChangeCheck())
                {
                    if (noServerCommands)
                    {
                        _exportSelection.MapList[index].Commands = _exportSelection.MapList[index].ServerCommands;
                        _exportSelection.MapList[index].ServerCommands = string.Empty;
                    }
                    else
                    {
                        _exportSelection.MapList[index].ServerCommands = _exportSelection.MapList[index].Commands;
                        _exportSelection.MapList[index].Commands = string.Empty;
                    }
                }
                
                //Draw Image preview
                var rowPreviewRect = new Rect(
                    rect.position +
                    Vector2.up * EditorGUIUtility.standardVerticalSpacing,
                    new Vector2(imageSize, imageSize)); 
                
                var pngPath = _exportSelection.MapList[index].MapImageFile + ".png";
                if (string.IsNullOrWhiteSpace(_exportSelection.MapList[index].MapImageFile) || !File.Exists(pngPath))
                {
                    EditorGUI.DrawRect(rowPreviewRect, Color.black);
                    EditorGUI.LabelField(rowPreviewRect, LevelConfigLabels.NoPreviewLabel);
                }
                else
                {
                    try
                    {
                
                        Texture2D tex;
                        if (LevelImageCache.ContainsKey(_exportSelection.MapList[index].MapImageFile))
                        {
                            tex = LevelImageCache[_exportSelection.MapList[index].MapImageFile];
                        }
                        else
                        {
                            tex = new Texture2D(2, 2, TextureFormat.ARGB32, false)
                            {
                                filterMode = FilterMode.Point,
                                wrapMode = TextureWrapMode.Clamp
                            };
                            tex.LoadImage(File.ReadAllBytes(pngPath));
                            LevelImageCache[_exportSelection.MapList[index].MapImageFile] = tex;
                        }
                
                        var aspect = tex.width * 1.0f / tex.height;
                        var pixelWidth = aspect >= 1 ? imageSize : imageSize * aspect;

                        var mouseInPreview = rowPreviewRect.Contains(Event.current.mousePosition);
                        if (Event.current.type == EventType.Repaint)
                        {
                            if (mouseInPreview)
                            {
                                _mouseOverPreview = true;
                                ChangePreviewIndexAndClosePopup(index);
                                _previewRect = rowPreviewRect;
                                _levelImagePreviewPopupTexture = tex;
                            }    
                        }

                        rowPreviewRect = new Rect(
                            rect.position +
                            Vector2.up * EditorGUIUtility.standardVerticalSpacing +
                            Vector2.up * Mathf.Max(0, imageSize - pixelWidth / aspect) / 2f, 
                            //NOTE: to center align horizontally do this
                            //+ Vector2.right * (aspect >= 1 ? 0f : imageSize - pixelWidth) / 2f,
                            new Vector2(pixelWidth, pixelWidth / aspect));

                        EditorGUI.DrawTextureTransparent(rowPreviewRect, tex);
                    }
                    catch
                    {
                        LevelImageCache.Remove(_exportSelection.MapList[index].MapImageFile);
                
                        EditorGUI.DrawRect(rowPreviewRect, Color.black);
                        EditorGUI.LabelField(rowPreviewRect, LevelConfigLabels.NoPreviewLabel); 
                    }
                }
            };
        }
        
        #region PreviewHelpers
        
        private class LevelImagePreviewPopup : PopupWindowContent
        {
            public Texture2D previewImage;
            public Vector2 imageSize;

            public override void OnGUI(Rect rect)
            {
                if (previewImage != null)
                {
                    GUI.DrawTexture(rect, previewImage);
                }
            }

            public override Vector2 GetWindowSize()
            {
                if (previewImage != null)
                {
                    return imageSize;
                }
            
                return base.GetWindowSize();
            }
        }
        
        private void OpenPopupPreviewIfNeeded()
        {
            //Do not open a preview popup if the mouse isn't over a small preview.
            if (!_mouseOverPreview) return;

            //Do not open a new preview while another preview is showing
            if (_popupPreviewOpen || HasOpenInstances<PopupWindow>()) return;
            
            var aspect = _levelImagePreviewPopupTexture.width * 1.0f / _levelImagePreviewPopupTexture.height;
            float previewWidth = Screen.height < Screen.width ? Screen.height : Screen.width;
            
            //Adjust to aspect
            previewWidth = aspect >= 1 ? previewWidth : previewWidth * aspect;

            var popup = new LevelImagePreviewPopup 
            {
                previewImage = _levelImagePreviewPopupTexture,
                imageSize = new Vector2(previewWidth, previewWidth / aspect)
            };

            _popupPreviewOpen = true;
            PopupWindow.Show(_previewRect, popup);
        }
        
        private void ChangePreviewIndexAndClosePopup(int index)
        {
            if (_previewIndex == index) return;

            if (_previewIndex != -1)
            {
                _popupPreviewOpen = false;
                Focus();
            }
            _previewIndex = index;
        }
        
        #endregion

        #region Menu helpers
        
        
        private void GenerateMenuItemsFor(string itemName, float pixelsPerUnit, GenericMenu menu)
        {
            //Add with and without sceneLight
            for (int i = 0; i < 2; i++)
            {
                //Add all and current scene
                for (int j = 0; j < 2; j++)
                {
                    AddCaptureMenuItem(itemName, pixelsPerUnit, i == 0, j == 1, menu);
                }
            }
        }

        private void AddCaptureMenuItem(string itemName,
            float pixelsPerUnit, bool addSceneLight, bool allScenes,
            GenericMenu menu)
        {
            var lightingMenu = addSceneLight ? LevelConfigLabels.UseInGameLighting : LevelConfigLabels.AddDirectionalLighting ;
            if (allScenes)
            {
                menu.AddItem(new GUIContent($"{LevelConfigLabels.CaptureAllScenesLabel}/{lightingMenu}/{itemName} ({pixelsPerUnit} {LevelConfigLabels.PixelsPerUnitLabel})"),
                    false, () => { CaptureScenes(_exportSelection, pixelsPerUnit, addSceneLight); });
            }
            else
            {
                menu.AddItem(
                    new GUIContent($"{LevelConfigLabels.CaptureCurrentSceneLabel}/{lightingMenu}/{itemName} ({pixelsPerUnit} {LevelConfigLabels.PixelsPerUnitLabel})"), false,
                    () =>
                    {
                        //Get the current scene name, and capture that. (Later on we could also ask the user for a name)
                        var sceneName = SceneManager.GetActiveScene().name;
                        CaptureCurrentScene(sceneName, _exportSelection, pixelsPerUnit, addSceneLight);
                    });
            }
        }
        
        #endregion
        
        #region File save/load helpers
        
        private bool DoesUserWantOrNeedToSave()
        {
            if (!_configIsDirty) return false;
            
            return EditorUtility.DisplayDialog(
                LevelConfigLabels.UnsavedChangesPopupTitle,
                LevelConfigLabels.UnsavedChangesPopupContent,
                "Yes", "No");
        }
        
        private void SaveConfiguration()
        {
            var anyEmptyLevelCommands = _exportSelection.MapList.Any(item =>
                string.IsNullOrWhiteSpace(item.Commands) && string.IsNullOrWhiteSpace(item.ServerCommands));

            if (!anyEmptyLevelCommands || EditorUtility.DisplayDialog(LevelConfigLabels.EmptyLevelPopupTitle,
                    LevelConfigLabels.EmptyLevelPopupContent, "Yes", "No"))
            {
                SaveSelectedMapFiles(_exportSelection);
                _configIsDirty = false;
            }
        }
        
        public static void SaveSelectedMapFiles(ModlLevelConfigurationExport definitions)
        {
            var discard = false;
            if (definitions.MapList.Any(item => string.IsNullOrWhiteSpace(item.Name) && 
                                                (!(string.IsNullOrWhiteSpace(item.Commands) &&
                                                   string.IsNullOrWhiteSpace(item.ServerCommands)) 
                                                 || !string.IsNullOrWhiteSpace(item.MapImageFile))))
            {
                if (EditorUtility.DisplayDialog(LevelConfigLabels.EmptyNamePopupTitle,
                        LevelConfigLabels.EmptyNamePopupContent, "Discard", "Keep"))
                {
                    discard = true;
                }
            }
            
            //Make sure to not save "empty" level configurations
            for (var index = 0; index < definitions.MapList.Count; index++)
            {
                var levelConfiguration = definitions.MapList[index];
                if (string.IsNullOrWhiteSpace(levelConfiguration.Name))
                {
                    //TODO: figure out if we should always remove completely empty rows or we should allow users to save them if they want.
                    if (discard || (string.IsNullOrWhiteSpace(levelConfiguration.Commands) &&
                                    string.IsNullOrWhiteSpace(levelConfiguration.ServerCommands) && 
                                    string.IsNullOrWhiteSpace(levelConfiguration.MapImageFile)))
                    {
                        definitions.MapList.RemoveAt(index--);
                    }
                    else
                    {
                        definitions.MapList[index].Name = $"({index})";
                    }
                }
            }
            
            File.WriteAllText(LevelCaptureUtils.LevelConfigName, JsonUtility.ToJson(definitions, true));
        }
        
        private void LoadLevelConfigurationsFromFile()
        {
            _exportSelection = LoadSelectedMapFiles();

            if (_exportSelection.MapList.Count == 0)
            {
                Debug.Log($"Initializing empty modl modl_map_config.json");
                _configIsDirty = true;
            }
        }
        
        public static ModlLevelConfigurationExport LoadSelectedMapFiles()
        {
            var levelConfigs = new ModlLevelConfigurationExport{MapList = new List<LevelConfiguration>()};

            try
            {
                if (File.Exists(LevelCaptureUtils.LevelConfigName))
                {
                    var config = File.ReadAllText(LevelCaptureUtils.LevelConfigName);
                    var loadedExportSelection = JsonUtility.FromJson<ModlLevelConfigurationExport>(config);

                    foreach (var selectedMap in loadedExportSelection.MapList)
                    {
                        var systemFamily = SystemInfo.operatingSystemFamily;
                        switch (systemFamily)
                        {
                            case OperatingSystemFamily.Linux:
                            case OperatingSystemFamily.MacOSX:
                                if (selectedMap.MapImageFile.Contains("\\"))
                                {
                                    Debug.LogWarning($"Map {selectedMap.Name} contains paths from another OS. Converting to work on current OS.");
                                    selectedMap.MapImageFile = selectedMap.MapImageFile.Replace("\\", "/");
                                    levelConfigs.MapList.Add(selectedMap);
                                    SaveSelectedMapFiles(levelConfigs);    
                                }
                                break;
                            case OperatingSystemFamily.Windows:
                                if (selectedMap.MapImageFile.Contains("/"))
                                {
                                    Debug.LogWarning($"Map {selectedMap.Name} contains paths from another OS. Converting to work on current OS.");
                                    selectedMap.MapImageFile = selectedMap.MapImageFile.Replace("/", "\\");
                                    levelConfigs.MapList.Add(selectedMap);
                                    SaveSelectedMapFiles(levelConfigs);    
                                }
                                break;
                        }
                        
                        if (!levelConfigs.MapList.Contains(selectedMap))
                        {
                            levelConfigs.MapList.Add(selectedMap);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("Failed to load level configurations from 'modl_map_config.json'");
                if (!EditorUtility.DisplayDialog(LevelConfigLabels.FailedToLoadLevelConfigs,
                        LevelConfigLabels.FailedToLoadLevelConfigContent,
                        "Keep It", "Delete It"))
                {
                    File.Delete(LevelCaptureUtils.LevelConfigName);
                }
            }
            
            return levelConfigs;
        }
        
        #endregion

        #region Capturing helpers

        private static void CaptureCurrentScene(string sceneName, ModlLevelConfigurationExport levelConfiguration, float pixelsPerUnit = 10f, bool addDirectLighting = false)
        {
            
            LevelCaptureEditorHelper.SetupCameraAndCapture(sceneName, levelConfiguration, LevelCaptureUtils.ViewDirection.TopView, addDirectLighting, pixelsPerUnit, addDirectLighting ? "InGameLighting" : "SceneViewLight");
        }

        private static void CaptureScenes(ModlLevelConfigurationExport levelConfiguration, float pixelsPerUnit = 10f, bool addDirectLighting = false)
        {
            LevelCaptureEditorHelper.CaptureAndAddScenesToMapDefinitions(GetSceneSelection(), LevelCaptureUtils.ViewDirection.TopView, addDirectLighting, pixelsPerUnit, addDirectLighting ?  "InGameLighting" : "SceneViewLight", levelConfiguration);
        }

        private static List<string> GetSceneSelection()
        {
            var allScenes = new List<string>();
            
            for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                allScenes.Add(EditorBuildSettings.scenes[i].path);
            }

            return allScenes;
        }

        #endregion

        #region Data Classes
        
        [Serializable]
        public class ModlLevelConfigurationExport
        {
            //NOTE: we can't rename this without changing code in the platform to handle the new name.
            public List<LevelConfiguration> MapList;
        }
        
        [Serializable]
        public class LevelConfiguration
        {
            public string Name;
            //TODO: in the future we may want Commands to be a list of KeyValuePair's
            public string Commands;
            //TODO: in the future we may want a definition for sections/areas/views in a single test/map
            public string ServerCommands;
            
            //NOTE: we can't rename this without changing code in the platform to handle the new name.
            public string MapImageFile;
        }
        
        #endregion
    }
}