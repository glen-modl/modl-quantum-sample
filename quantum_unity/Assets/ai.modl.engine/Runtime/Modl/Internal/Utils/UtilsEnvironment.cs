using System;
using System.IO;
using System.Linq;

using UnityEngine;

namespace Modl.Internal.Utils
{
    public static class UtilsEnvironment
    {
        private const string STR_PATH = "path";
        private const string STR_GENERATE = "--generate_config";
        
        public static string GetEnvVariable(string variable)
        {
            return Environment.GetEnvironmentVariable(variable);
        }
        
        /// <summary>
        /// Checks if the generator parameter was passed, as well as a path to create the file
        /// </summary>
        /// <returns></returns>
        public static string CheckIfShouldGenerate()
        {
            //checks if received parameter to generate config Files
            string[] arguments = Environment.GetCommandLineArgs();
            bool shouldWrite = arguments.Any(t => t.ToLower().Equals(STR_GENERATE));

            if (!shouldWrite) return null;
            
            string path = GetFilePath(arguments);
            
            if (!string.IsNullOrEmpty(path)) return path;
            
            //write error log to local file
            File.WriteAllText(Directory.GetCurrentDirectory() + " / ERROR.log",
                "Path to save configuration files was not provided. Provide path with arguments path" +
                " \"path of file\", example: Build.exe --generate_config path \"local\file\"");
            Application.Quit();
            return null;
        }
        
        
        /// <summary>
        /// Get file path from arguments
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static string GetFilePath(string[] arguments)
        {
            string path = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].ToLower().Equals(STR_PATH) && arguments.Length > i + 1)
                {
                    path = arguments[i + 1];
                }
            }
            return path;
        }
    }
}