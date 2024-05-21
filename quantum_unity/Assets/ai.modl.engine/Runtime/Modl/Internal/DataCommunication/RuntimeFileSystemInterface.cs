using System;
using System.IO;
using Modl.Proto;
using UnityEngine;
using Google.Protobuf;

namespace Modl.Internal.DataCommunication
{
    public class RuntimeFileSystemInterface
    {
        /// <summary>
        /// TODO platform depends on the json file name, where's the source of truth?
        /// RuntimeFileSystemInterface should still define the path leading to such file, but they should be
        /// available separately
        /// </summary>
        public const string CONFIG_PATH = "game_config.json";
        private readonly JsonFormatter _jsonFormatter;

        public RuntimeFileSystemInterface()
        {
            
            JsonFormatter.Settings settings = JsonFormatter.Settings.Default.
                WithIndentation().
                WithPreserveProtoFieldNames(true);
            _jsonFormatter = new JsonFormatter(settings);
        }
        
        public GameConfig ReadConfigFile()
        {
            GameConfig config = new GameConfig();
            
            try
            {
                string json = File.ReadAllText(CONFIG_PATH);
                config = Google.Protobuf.JsonParser.Default.Parse<GameConfig>(json);
            }
            catch (Exception e)
            {   
#if UNITY_EDITOR
                Debug.Log($"Initializing empty modl game_config.json\n\n{e}");

                // initialize empty spaces
                config.ActionSpace = new ValueRange();
                config.ObjectSpace = new ValueRange();
                config.FeatureSpace = new ValueRange();
                config.SensorSpace = new ValueRange();
#else
                throw;
#endif
            }

            
            config.BrainVersion = ModlPluginManager.BrainVersion;
            return config;
        }

        public void WriteConfigFile(GameConfig config)
        {
            //var json = Google.Protobuf.JsonFormatter.Default.Format(config);
            var json = _jsonFormatter.Format(config);
            
            File.WriteAllText(CONFIG_PATH, json);
            Debug.Log($"Game config saved to {CONFIG_PATH}");
        }
    }
}
