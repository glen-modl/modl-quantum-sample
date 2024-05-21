using System.Collections.Generic;
using System.IO;
using Modl.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Modl.Editor
{
    public static class LevelCaptureEditorHelper
    {
        private static Color kSceneViewMidLight = new Color(57f / 500f, 0.125f, 0.133f, 1f);

        public static void CaptureAndAddScenesToMapDefinitions(List<string> scenePaths, LevelCaptureUtils.ViewDirection captureDirection = LevelCaptureUtils.ViewDirection.TopView, bool sceneLighting = false, float pixelsPerUnit = 1f, string postfix = "", EW_LevelConfiguration.ModlLevelConfigurationExport levelConfigurationRef = null)
        {
            //Save the current Editor setup, before running this!
            var currentSetup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            
            //Ask the user if they want to save any changes they did to the current scene before we begin:
            UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            var levelConfigurations = levelConfigurationRef;
            if (levelConfigurations == null)
            {
                //Load the current definitions to avoid overriding them.
                 levelConfigurations = EW_LevelConfiguration.LoadSelectedMapFiles();    
            }
            
            //Loop over all the scenes
            for (var index = 0; index < scenePaths.Count; index++)
            {
                var scenePath = scenePaths[index];
                if (EditorUtility.DisplayCancelableProgressBar("Capturing Scene Images", $"Processing {scenePath}.", index * 1f / scenePaths.Count))
                {
                    break;
                }
                
                //Load the Scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

                SetupCameraAndCapture(scene.name, levelConfigurations, captureDirection, sceneLighting, pixelsPerUnit,
                    postfix);
            }

            if (levelConfigurationRef == null)
            {
                //Save the definitions again at the end.
                EW_LevelConfiguration.SaveSelectedMapFiles(levelConfigurations);    
            }

            //Restore the Editor setup once the capturing has completed.
            UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(currentSetup);

            EditorUtility.ClearProgressBar();
        }

        public static void SetupCameraAndCapture(string sceneName,
            EW_LevelConfiguration.ModlLevelConfigurationExport levelConfigurations,
            LevelCaptureUtils.ViewDirection captureDirection = LevelCaptureUtils.ViewDirection.TopView,
            bool sceneLighting = false,
            float pixelsPerUnit = 1f,
            string postfix = "")
        {
            //Find all renderers
            var renderers = Object.FindObjectsOfType<Renderer>();
            if (renderers.Length == 0)
            {
                // Scene has no renderers, let's skip it
                return;
            }

            //Find first available enabled renderer.
            var indexOfFirstEnabledRenderer = -1;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;

                indexOfFirstEnabledRenderer = i;
                break;
            }

            if (indexOfFirstEnabledRenderer == -1)
            {
                //Scene has no enabled renderers, let's skip it
                return;
            }

            //Init bounds with the first enabled renderer
            var bounds = renderers[indexOfFirstEnabledRenderer].bounds;
            for (var i = indexOfFirstEnabledRenderer + 1; i < renderers.Length; i++)
            {
                //Skip disabled renderers
                if (!renderers[i].enabled) continue;

                //Expand bounds to contain each renderer
                bounds.Encapsulate(renderers[i].bounds);
            }

            //Create a temporary camera to capture the level image.
            var go = new GameObject("ModlLevelMapCaptureCamera", typeof(Camera)) {hideFlags = HideFlags.HideAndDontSave};
            var cam = go.GetComponent<Camera>();

            cam.orthographic = true;
            //cam.cameraType = CameraType.SceneView;
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = Color.clear;

            //Set camera position, based on the calculated bounds
            cam.transform.position = bounds.center
                                     //Move 2 units off the center of the bounding box 
                                     + LevelCaptureUtils.DirectionPositionOffset[(int) captureDirection] * 2
                                     //From the (altered) center add the extent (extent is half the size)
                                     + new Vector3(
                                         LevelCaptureUtils.DirectionPositionOffset[(int) captureDirection].x * bounds.extents.x,
                                         LevelCaptureUtils.DirectionPositionOffset[(int) captureDirection].y * bounds.extents.y,
                                         LevelCaptureUtils.DirectionPositionOffset[(int) captureDirection].z * bounds.extents.z);

            //Set the rotation of the camera to look in the right direction
            cam.transform.rotation = LevelCaptureUtils.DirectionRotations[(int) captureDirection];

            //Set the aspect ratio, based on the size of the bounds
            var captureUnitSize = LevelCaptureUtils.To2DOffset(captureDirection, bounds.size);
            cam.aspect = Mathf.Abs(captureUnitSize.x) / Mathf.Abs(captureUnitSize.y);

            //Set camera orthographic size, based on the min and max bounds:
            var min = LevelCaptureUtils.To2DOffset(captureDirection, bounds.min);
            var max = LevelCaptureUtils.To2DOffset(captureDirection, bounds.max);
            cam.orthographicSize = Vector2.Distance(min, max) / 2f;

            //TODO: Consider implementing additional customization parameters, similar to SceneView / EW_MapCapture 
            //sceneView.drawGizmos = false;
            //sceneView.sceneLighting = true;
            //sceneView.showGrid = true;
            //sceneView.sceneViewState.SetAllEnabled(false);

            //Set capture name to scene name + direction + postfix
            var filename = $"{sceneName}_{captureDirection}_{postfix}_{pixelsPerUnit}ppu";

            //Calculate the desired pixel width (cam.orthographicSize is the "half-size" of the camera width).
            var desiredPixelWidth = 2f * cam.orthographicSize * pixelsPerUnit;
            //Make sure we never become larger than the max supported texture size
            var maxedPixelWidth = Mathf.Min(desiredPixelWidth, SystemInfo.maxTextureSize);

            var capturePixelWidth = cam.aspect >= 1 ? maxedPixelWidth : maxedPixelWidth * cam.aspect;

            //Capture the level image
            CaptureSceneView(filename, cam, captureDirection, LevelCaptureUtils.To2DOffset(captureDirection, bounds.center),
                capturePixelWidth, sceneLighting);

            //Add it to the Map Definitions
            levelConfigurations.MapList.Add(new EW_LevelConfiguration.LevelConfiguration
            {
                Name = sceneName,
                MapImageFile = Path.Combine(LevelCaptureUtils.LevelCaptureFolder, filename),
                Commands = string.Empty,
            });

            Object.DestroyImmediate(cam);
        }

        public static void CaptureSceneView(string filename, Camera camera, LevelCaptureUtils.ViewDirection direction, Vector2 offset, float width, bool relyOnGameLighting = true)
        {
            GameObject tempLight = null;
            if(!relyOnGameLighting)
            {
                //Recreating what the SceneView does in OnGUI when sceneLighting is disabled.
                tempLight = new GameObject("tempLight", typeof(Light)){hideFlags = HideFlags.HideAndDontSave};
                var lightComp = tempLight.GetComponent<Light>();
                lightComp.color = Color.white;
                lightComp.type = LightType.Directional;
                tempLight.transform.rotation = camera.transform.rotation;
                UnityEditorInternal.InternalEditorUtility.SetCustomLighting(new []{lightComp}, kSceneViewMidLight);
            }
            
            LevelCaptureUtils.CaptureImageAndCoordinates(filename, camera, direction, offset, width);
                
            if(!relyOnGameLighting)
            {
                //Cleaning it up like SceneView does in OnGUI when sceneLighting is disabled.
                UnityEditorInternal.InternalEditorUtility.RemoveCustomLighting();
                Object.DestroyImmediate(tempLight);
            }
        }
    }
}