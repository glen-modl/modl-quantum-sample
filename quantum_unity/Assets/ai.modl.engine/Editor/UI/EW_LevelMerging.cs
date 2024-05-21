using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Modl.Editor.UI
{
    public class EW_LevelMerging : EditorWindow
    {
        private List<LevelCaptureUtils.SelectableMapFile> selectableFilePairs =  new List<LevelCaptureUtils.SelectableMapFile>();
        private Vector2 selectedFilePos;
        private bool deleteMergedFiles;
        
        public void OnEnable()
        {
            LevelCaptureUtils.LoadFilePairs(selectableFilePairs, LevelFolder);
        }
        
        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Select level sections to merge from loaded file pairs:");

            
                if (GUILayout.Button(selectableFilePairs.All(item => item.selected) ? "Uncheck All" : "Check All", GUILayout.Width(100)))
                {
                    if(selectableFilePairs.All(item => item.selected))
                    {
                        selectableFilePairs.ForEach(item => item.selected = false);
                    }
                    else
                    {
                        selectableFilePairs.ForEach(item => item.selected = true);
                    }
                }

                using (var s = new EditorGUILayout.ScrollViewScope(selectedFilePos))
                {
                    //Select files from loaded list
                    for (var index = 0; index < selectableFilePairs.Count; index++)
                    {
                        var filePair = selectableFilePairs[index];
                        filePair.selected = EditorGUILayout.ToggleLeft(filePair.file, filePair.selected);
                    }

                    selectedFilePos = s.scrollPosition;
                }

                if (GUILayout.Button("Select a folder with level sections to merge"))
                {
                    var path = EditorUtility.OpenFolderPanel(
                        "Select folder with level-sections to merge (only works on .png + .json level-section pairs)",
                        LevelFolder, "");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        LevelFolder = path;
                        selectableFilePairs.Clear();
                        LevelCaptureUtils.LoadFilePairs(selectableFilePairs, path);
                    }
                }
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                LevelFilename = EditorGUILayout.TextField("Merged Level Name:", LevelFilename);
                EditorGUILayout.Separator();
                deleteMergedFiles = EditorGUILayout.Toggle("Delete merged files: ", deleteMergedFiles);
            }
            
            if (GUILayout.Button("Merge all selected files"))
            {
                LevelCaptureUtils.MergeLevelSections(selectableFilePairs.Where(item => item.selected).Select(item => item.file), LevelFilename);

                if (deleteMergedFiles)
                {
                    var removeThese = selectableFilePairs.Where(item => item.selected).ToList();
                    removeThese.ForEach(item => File.Delete($"{item.file}.png"));
                    removeThese.ForEach(item => File.Delete($"{item.file}.json"));
                    
                    removeThese.ForEach(item => selectableFilePairs.Remove(item));
                }
            }
        }
        
        #region Editor Prefs
        private static string LevelFolder
        {
            get => EditorPrefs.GetString("modl_mergeDirectory",  LevelCaptureUtils.GetRelativePath(Application.dataPath, "capturedLevels"));
            set => EditorPrefs.SetString("modl_mergeDirectory", value);
        }
        
        private static string LevelFilename
        {
            get => EditorPrefs.GetString("modl_levelOutputFilename", "map");
            set => EditorPrefs.SetString("modl_levelOutputFilename", value);
        }
        
        #endregion
    }
}