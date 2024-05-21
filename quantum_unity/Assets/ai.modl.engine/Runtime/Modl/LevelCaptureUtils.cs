using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modl
{
    public static class LevelCaptureUtils
    {
        public const string LevelConfigName = "modl_map_config.json";
        public const string LevelCaptureFolder = "capturedLevels";
        
        public class SelectableMapFile
        {
            public string file;
            public bool selected;
        }
        
        public enum ViewDirection
        {
            TopView,
            BottomView,
            FrontView,
            BackView,
            LeftView,
            RightView,
        }
        
        public static readonly Quaternion[] DirectionRotations = new Quaternion[6]
        {
            Quaternion.LookRotation(new Vector3(0.0f, -1f, 0.0f)), //Top
            Quaternion.LookRotation(new Vector3(0.0f, 1f, 0.0f)), //Bottom
            Quaternion.LookRotation(new Vector3(0.0f, 0.0f, -1f)), //Front
            Quaternion.LookRotation(new Vector3(0.0f, 0.0f, 1f)), //Back
            Quaternion.LookRotation(new Vector3(1f, 0.0f, 0.0f)), //Left
            Quaternion.LookRotation(new Vector3(-1f, 0.0f, 0.0f)) //Right
        };

        public static readonly Vector3[] DirectionPositionOffset = new Vector3[6]
        {
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
        };

        public static Vector2 To2DOffset(ViewDirection direction, Vector3 position)
        {
            switch (direction)
            {
                case ViewDirection.TopView:
                    return new Vector2(position.x, position.z);
                case ViewDirection.BottomView:
                    return new Vector2(position.x, -position.z);
                case ViewDirection.FrontView:
                    return new Vector2(-position.x, position.y);
                case ViewDirection.BackView:
                    return new Vector2(position.x, position.y);
                case ViewDirection.LeftView:
                    return new Vector2(-position.z, position.y);
                case ViewDirection.RightView:
                    return new Vector2(position.z, position.y);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        //TODO: move this into the game_config.json! 
        [Serializable]
        public struct LevelCoordinates
        {
            public float width;
            public float height;
            public float offsetHorizontal;
            public float offsetVertical;
            public ViewDirection viewDirection;
            public string axisHorizontal;
            public string axisVertical;
            public bool reverseHorizontal;
            public bool reverseVertical;
        }
        
        [Serializable]
        public struct LevelSection
        {
            public LevelCoordinates Coordinates;
            public Texture2D Image;
        }
        
        public static void LoadFilePairs(List<SelectableMapFile> selectableFilePairList, string path = "")
        {
            //Ensure the directory exists.
            Directory.CreateDirectory(path);
            var files = Directory.GetFiles(path);
            
            var pngs = files.Where(s => s.EndsWith(".png")).Select(str => str.Replace(".png", ""));

            foreach (var file in pngs)
            {
                var relativeFilePath = GetRelativePath(Application.dataPath, file);
                if (files.Contains(file + ".json") && !selectableFilePairList.Exists(item => item.file == relativeFilePath))
                {
                    selectableFilePairList.Add(new SelectableMapFile{file = relativeFilePath, selected = false});    
                }
            }
        }
        
        public static string GetRelativePath(string relativeTo, string path)
        {
            var uri = new Uri(relativeTo, UriKind.Absolute);
            string fullPath = Path.GetFullPath(path);
            var rel = uri.MakeRelativeUri(new Uri(fullPath)).ToString();
            
            var systemFamily = SystemInfo.operatingSystemFamily;
            switch (systemFamily)
            {
                case OperatingSystemFamily.Linux:
                case OperatingSystemFamily.MacOSX:
                    if (rel.Contains("\\"))
                    {
                        rel = rel.Replace("\\", "/");  
                    }
                    break;
                case OperatingSystemFamily.Windows:
                    if (rel.Contains("/"))
                    {
                        rel = rel.Replace("/", "\\");
                    }
                    break;
            }

            return rel;
        }

        public static bool CheckFilePairExists(string pathToPng)
        {
            return pathToPng.EndsWith(".png") && File.Exists(Path.ChangeExtension(pathToPng, ".json"));
        }
        
        private static (string horizonal, bool reverseHorizontal, string vertical, bool reverseVertical) GetPlottingAxes(ViewDirection direction)
        {
            switch (direction)
            {
                case ViewDirection.TopView:
                    return ("x", false, "z", false);
                case ViewDirection.BottomView:
                    return ("x", false, "z", true);
                case ViewDirection.FrontView:
                    return ("x", true, "y", false);
                case ViewDirection.BackView:
                    return ("x", false, "y", false);
                case ViewDirection.LeftView:
                    return ("z", true, "y", false);
                case ViewDirection.RightView:
                    return ("z", false, "y", false);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private static LevelCoordinates MakeLevelCoordinates(Camera camera, ViewDirection direction, Vector2 offset)
        {
            var width = camera.aspect * camera.orthographicSize * 2f;
            var height =  width / camera.aspect;
            
            var axes = GetPlottingAxes(direction);
            var coords = new LevelCoordinates
            {
                width = width,
                height = height,
                offsetHorizontal = (axes.reverseHorizontal ? -1f : 1f) * (offset.x - width / 2f),
                offsetVertical = (axes.reverseVertical ? -1f : 1f) * (offset.y + height / 2f),
                viewDirection = direction,
                axisHorizontal = axes.horizonal,
                axisVertical = axes.vertical,
                reverseHorizontal = axes.reverseHorizontal,
                reverseVertical = axes.reverseVertical,
            };
            return coords;
        }

        public static void CaptureImageAndCoordinates(string name, Camera camera, ViewDirection direction, Vector2 offset, float imageWidth = 1000, bool autoMerge = false)
        {
            Directory.CreateDirectory(LevelCaptureFolder);
            
            var filename = Path.Combine(LevelCaptureFolder, name.Contains(direction.ToString()) ? name : $"{name}-{direction}");
            var pngFile = $"{filename}.png";
            var jsonFile = $"{filename}.json";
            
            if (autoMerge && File.Exists(jsonFile) && File.Exists(pngFile))
            {
                //Load image and coordinates from file.
                //Read coordinates
                var coordString = File.ReadAllText(jsonFile);
                var coordinates = JsonUtility.FromJson<LevelCoordinates>(coordString);
                
                //Read Image
                var pngData = File.ReadAllBytes(pngFile);
                //NOTE: LoadImage adjusts the size of the texture when loading.
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(pngData);
                
                var loadedLevelSection = new LevelSection
                {
                    Coordinates = coordinates,
                    Image = tex,                    
                };

                //Capture level image and coordinates
                var capturedTexture = CaptureCamera(camera, imageWidth);
                var coords = MakeLevelCoordinates(camera, direction, offset);    
                
                var levelSection = new LevelSection
                {
                    Coordinates = coords,
                    Image = capturedTexture,
                };

                //Merge the sections and save the merged file
                var sectionList = new List<LevelSection>
                {
                    loadedLevelSection,
                    levelSection
                };

                BuildAndSaveMergedLevelImage(sectionList, $"{name}-{direction}", true);
            }
            else
            {
#if UNITY_EDITOR            
                if (File.Exists(pngFile) && !EditorUtility.DisplayDialog("File already exists!", $"The file '{pngFile}' already exists, would you like to override it?", "Yes", "No"))
                {
                    return;
                }
#endif
                var file = File.Create(pngFile);
                if (!file.CanWrite)
                {
                    Debug.LogError("Unable to capture editor screenshot, failed to create file for writing");
                    return;
                }
                
                //Capture, Encode and write level image to PNG
                var capturedTexture = CaptureCamera(camera, imageWidth);
                var pngData = capturedTexture.EncodeToPNG();
                Object.DestroyImmediate(capturedTexture);
           
                file.Write(pngData, 0, pngData.Length);
                file.Close();
                Debug.Log("Image written to file " + pngFile);

                //Capture and Write level coordinate JSON
                var coords = MakeLevelCoordinates(camera, direction, offset);

                Debug.Log($"Level coordinates (view direction: {direction}): \nwidth: {coords.width} \nheight: {coords.height} \nhorizontal offset: {coords.offsetHorizontal} \nvertical offset: {coords.offsetVertical} \nhorizontal axis [reversed={coords.reverseHorizontal}]: {coords.axisHorizontal} \nvertical axis [reversed={coords.reverseVertical}]: {coords.axisVertical}");

                var jsonCoords = JsonUtility.ToJson(coords, true);
                
                var fileJson = File.CreateText(jsonFile);

                fileJson.Write(jsonCoords);
                fileJson.Close();

                Debug.Log("Coordinates written to file " + jsonFile);
            }
        }
        
        private static Texture2D CaptureCamera(Camera camera, float width = 1000)
        {
            var activeRenderTexture = RenderTexture.active;
            var targetTexture = camera.targetTexture;
            var releaseTemporary = false; 
            if (targetTexture == null)
            {
                releaseTemporary = true;
                var height =  width / camera.aspect;
                targetTexture = RenderTexture.GetTemporary(Mathf.RoundToInt(width),Mathf.RoundToInt(height), 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = targetTexture;
            }
            
            RenderTexture.active = camera.targetTexture;
            camera.Render();

            //Render the level with linear lighting, like you see it in the default scene view (otherwise it can be dark).
            //TODO consider using 'linearLighting = !GraphicsFormatUtility.IsSRGBFormat(rTex.graphicsFormat);'
            const bool linearLighting = true; 
            var image = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false, linearLighting);
            image.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            image.Apply();
            RenderTexture.active = activeRenderTexture;
            camera.targetTexture = targetTexture;
  
            if(releaseTemporary)
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(targetTexture);
            }
                
            return image;
        }

        public static void MergeLevelSections(IEnumerable<string> sectionNames, string mergedName = null)
        {
            var sections = new List<LevelSection>();
            var sectionCount = sectionNames.Count();
            var currentSection = 1f;
            foreach (var sectionName in sectionNames)
            {
#if UNITY_EDITOR
                EditorUtility.DisplayProgressBar("Loading sections", sectionName, (currentSection++ / sectionCount));
#endif
                    if (!File.Exists($"{sectionName}.json") || !File.Exists($"{sectionName}.png"))
                    {
                        Debug.LogWarning($"Could not find {sectionName}.json or {sectionName}.png, skipping!");
                        continue;
                    }

                    var section = new LevelSection();
                    var coordString = File.ReadAllText($"{sectionName}.json");
                    section.Coordinates = JsonUtility.FromJson<LevelCoordinates>(coordString);

                    var pngData = File.ReadAllBytes($"{sectionName}.png");

                    //NOTE: LoadImage adjusts the size of the texture when loading.
                    var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    tex.LoadImage(pngData);
                    section.Image = tex;

                    sections.Add(section);
            }
#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
#endif
                
            if (sections.Count > 0)
            {
                MergeLevelSections(sections, mergedName);
            }
        }

        public static void MergeLevelSections(List<LevelSection> sections, string mergedName = null)
        {
            var currentDirection = 1f;
            foreach (ViewDirection direction in Enum.GetValues(typeof(ViewDirection)))
            {
#if UNITY_EDITOR
                EditorUtility.DisplayProgressBar("Merging sections",  direction.ToString(), (currentDirection++/6));
#endif
                var levelSections = sections.Where(section => section.Coordinates.viewDirection == direction).ToList();
                if (levelSections.Count > 0)
                {
                    if (!string.IsNullOrEmpty(mergedName))
                    {
                        BuildAndSaveMergedLevelImage(levelSections, mergedName.Contains(direction.ToString()) ? mergedName : $"{mergedName}-{direction}");
                    }
                    else
                    {
                        BuildAndSaveMergedLevelImage(levelSections, $"MergedMap-{direction}");
                    }
                }
            }
#if UNITY_EDITOR            
            EditorUtility.ClearProgressBar();
#endif
        }

        private static void BuildAndSaveMergedLevelImage(List<LevelSection> sections, string name = "MergedMap", bool forceOverride = false)
        {
            if (sections.Count == 0)
            {
                throw new ArgumentException("Section list empty!");
            }
            
            Directory.CreateDirectory(LevelCaptureFolder);
            
            var filename = Path.Combine(LevelCaptureFolder, $"{name}.png");
#if UNITY_EDITOR
            if (!forceOverride && File.Exists(filename) && !EditorUtility.DisplayDialog("File already exists!", $"The file '{filename}' already exists, would you like to override it?", "Yes", "No"))
            {
                return;
            }
#endif
            
            var file = File.Create(filename);
            if (!file.CanWrite)
            {
                Debug.LogError($"Unable to capture editor screenshot, failed to create file for writing: {filename}");
                return;
            }
            
            var mergedCoordinates = GetMergedCoordinates(sections);
            var pixelDensity = (sections[0].Image.height / sections[0].Coordinates.height);
            var pixelWidth = mergedCoordinates.width * pixelDensity;
            var pixelHeight = mergedCoordinates.height * pixelDensity;

            var mergedTexture = new Texture2D(
                Mathf.CeilToInt(pixelWidth),
                Mathf.CeilToInt(pixelHeight),
                TextureFormat.ARGB32, false);

            foreach (var levelSection in sections)
            {
                //NOTE: Unity offset is based on bottom-left, Platform offset is based on top-left
                CombineTextures(mergedTexture, mergedCoordinates, levelSection, pixelDensity);
            }
            
            var pngData = mergedTexture.EncodeToPNG();

            file.Write(pngData, 0, pngData.Length);
            file.Close();
             

            var jsonCoords = JsonUtility.ToJson(mergedCoordinates, true);

            var filenameJson = Path.Combine(LevelCaptureFolder, $"{name}.json");
            StreamWriter fileJson = File.CreateText(filenameJson);

            fileJson.Write(jsonCoords);
            fileJson.Close();

            Debug.Log("Coordinates written to file " + filenameJson);
        }

        private static LevelCoordinates GetMergedCoordinates(IReadOnlyList<LevelSection> sections)
        {
            if (sections.Count == 0)
            {
                throw new ArgumentException("Section list empty!");
            }

            //Calculate total width/height and offsets for the merged images. 
            var totalWidth = sections[0].Coordinates.width;
            var totalHeight = sections[0].Coordinates.height;
            var totalHorizontalOffset = sections[0].Coordinates.offsetHorizontal;
            var totalVerticalOffset = sections[0].Coordinates.offsetVertical;

            for (var index = 1; index < sections.Count; index++)
            {
                var levelSectionCoords = sections[index].Coordinates;

                //Check if we need to expand the image left.
                if (levelSectionCoords.offsetHorizontal < totalHorizontalOffset)
                {
                    var diff = totalHorizontalOffset - levelSectionCoords.offsetHorizontal;
                    totalWidth += diff;
                    totalHorizontalOffset = levelSectionCoords.offsetHorizontal;
                }

                //Check if we need to expand the image right.
                if (totalHorizontalOffset + totalWidth < levelSectionCoords.offsetHorizontal + levelSectionCoords.width)
                {
                    var diff = levelSectionCoords.offsetHorizontal + levelSectionCoords.width - (totalHorizontalOffset + totalWidth);
                    totalWidth += diff;
                }

                //Also calculate the vertical platform offset.
                if (!levelSectionCoords.reverseVertical)
                {
                    //Check if we need to expand the image "down".
                    if (levelSectionCoords.offsetVertical - levelSectionCoords.height < totalVerticalOffset - totalHeight)
                    {
                        var diff = (levelSectionCoords.offsetVertical - levelSectionCoords.height) - (totalVerticalOffset - totalHeight);
                        totalHeight -= diff;
                    }

                    //Check if we need to expand the image "up"
                    if (totalVerticalOffset < levelSectionCoords.offsetVertical)
                    {
                        var diff = levelSectionCoords.offsetVertical - totalVerticalOffset;
                        totalHeight += diff;
                        totalVerticalOffset += diff;
                    }
                }
                else
                {
                    //Check if we need to expand the image up.
                    if (levelSectionCoords.offsetVertical < totalVerticalOffset)
                    {
                        var diff = totalVerticalOffset - levelSectionCoords.offsetVertical;
                        totalHeight += diff;
                        totalVerticalOffset = levelSectionCoords.offsetVertical;
                    }

                    //Check if we need to expand the image right.
                    if (totalVerticalOffset + totalHeight < levelSectionCoords.offsetVertical + levelSectionCoords.height)
                    {
                        var diff = levelSectionCoords.offsetVertical + levelSectionCoords.height - (totalVerticalOffset + totalHeight);
                        totalHeight += diff;
                    }
                }
            }
            
            var mergedCoordinates = new LevelCoordinates
            {
                offsetHorizontal = totalHorizontalOffset, 
                offsetVertical = totalVerticalOffset,
                width = totalWidth, 
                height = totalHeight,
                //NOTE: we assume all provided sections use the same coordinate system.
                viewDirection = sections[0].Coordinates.viewDirection,
                axisHorizontal = sections[0].Coordinates.axisHorizontal,
                axisVertical = sections[0].Coordinates.axisVertical,
                reverseHorizontal = sections[0].Coordinates.reverseHorizontal,
                reverseVertical = sections[0].Coordinates.reverseVertical
            };
            
            return mergedCoordinates;
        }

        private static void CombineTextures (Texture2D mergedTexture, LevelCoordinates mergedCoordinates, LevelSection section, float pixelDensity)
        {
            var xCoord = section.Coordinates.reverseHorizontal
                ? (mergedCoordinates.offsetHorizontal + mergedCoordinates.width) -
                  (section.Coordinates.offsetHorizontal + section.Coordinates.width)
                : (section.Coordinates.offsetHorizontal - mergedCoordinates.offsetHorizontal);
           
            //NOTE: This is really mind-bending, because the merged coordinates contain the vertical offset for the platform (top-left, instead of bottom-left for unity)
            var yIsThisSoDifficult = (section.Coordinates.offsetVertical - section.Coordinates.height) -
                                     (mergedCoordinates.offsetVertical - mergedCoordinates.height);

            var yCoord = section.Coordinates.reverseVertical
                ? (mergedCoordinates.offsetVertical + mergedCoordinates.height) -
                  (section.Coordinates.offsetVertical + section.Coordinates.height)
                : yIsThisSoDifficult;
            
            //Adjust to pixel coordinates!
            var x = Mathf.FloorToInt(xCoord * pixelDensity);
            var y = Mathf.FloorToInt(yCoord * pixelDensity);
            
            var width = Mathf.CeilToInt(section.Coordinates.width * pixelDensity);
            var height = Mathf.CeilToInt(section.Coordinates.height * pixelDensity);

            //Debug.Log($"Section size: ({section.Image.width}, {section.Image.height})");
            section.Image = ResizeTexture2D(section.Image, width, height);
            
            //Ensure that we draw inside the merged texture bounds (adjusting for floating point errors). 
            var xClamped = Mathf.RoundToInt(Mathf.Clamp(x, 0, mergedTexture.width - section.Image.width));
            var yClamped = Mathf.RoundToInt(Mathf.Clamp(y, 0, mergedTexture.height - section.Image.height));

            if (x != xClamped || y != yClamped)
            {
                Debug.LogWarning($"Potential misaligned level section: x={x} vs xClamped={xClamped}, y={y} vs yClamped={yClamped}");
            }

            if (Mathf.Abs(xClamped - x) < section.Image.width && Mathf.Abs(yClamped - y) < section.Image.height)
            {
                //NOTE: If the x/y is clamped, adjust the size of the copied texture so it isn't painted out of bounds.
                //This should only happen with 1 pixel overlaps due to floating point errors. 
                var xDiff = Mathf.Abs(xClamped - x);
                var yDiff = Mathf.Abs(yClamped - y);
                if (x > xClamped)
                {
                    xClamped += xDiff;    
                }

                if (y > yClamped)
                {
                    yClamped += yDiff;    
                }
                
                Debug.Log(
                    $"Drawing Texture at: " +
                    $"x={xClamped} " +
                    $"y={yClamped} " +
                    $"width={section.Image.width - xDiff} " +
                    $"height={section.Image.height - yDiff}");
                // Paint in the section into the merged image.
                mergedTexture.SetPixels(
                    xClamped,
                    yClamped,
                    section.Image.width - xDiff,
                    section.Image.height - yDiff,
                    section.Image.GetPixels(x < 0 ? -x : 0, y < 0 ? -y : 0,
                        section.Image.width - xDiff, 
                        section.Image.height - yDiff));

                //TODO: Consider merging the pixels, instead of just overriding them (so the transparent bits don't override opaque things).
                //TODO: This is only really relevant when level elements move around (moving platforms etc.)
                //TODO: If we use this, we could just forego pixels that lie outside the mergedTexture, in case floating point errors have caused slight pixel misalignment.
                // for(var y = 0; y < section.Image.height; y++){
                //     for(var x = 0; x < section.Image.width; x++){
                //         var PixelColorFore = section.Image.GetPixel(x, y)*section.Image.GetPixel(x, y).a;
                //         var PixelColorBack = mergedTexture.GetPixel(Mathf.RoundToInt(x + offset.x), Mathf.RoundToInt(y + offset.y))*(1-PixelColorFore.a);
                //         mergedTexture.SetPixel(Mathf.RoundToInt(x + offset.x), Mathf.RoundToInt(y + offset.y), PixelColorBack + PixelColorFore);
                //     }
                // }
                
                mergedTexture.Apply();
            }
        }

        public static Texture2D ResizeTexture2D(Texture2D texture2D, int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 24);
            RenderTexture.active = renderTexture;
            Graphics.Blit(texture2D, renderTexture);
            var result = new Texture2D(width, height, texture2D.format, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            return result;
        }
    }
}