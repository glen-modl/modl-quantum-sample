using System;
using System.Text;
using UnityEngine;

namespace Modl.Internal.Utils.AWS
{

    public class ModlAWSCredentials
    {
        public string scheme;
        public string host;
        public string region;
        public string endpoint;
        public string accessKey;
        public string secretKey;
        public string sessionId;

        public void Copy(ModlAWSCredentials credentials)
        {
            region = credentials.region;
            endpoint = credentials.endpoint;
            accessKey = credentials.accessKey;
            secretKey = credentials.secretKey;
            sessionId = credentials.sessionId;
        }

        public bool IsValid => !string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(endpoint) &&
                               !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey) &&
                               !string.IsNullOrEmpty(sessionId);

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("[Credentials]\n");
            b.AppendFormat("\tscheme : {0},\n", this.scheme);
            b.AppendFormat("\thost : {0},\n", this.host);
            b.AppendFormat("\tregion : {0},\n", this.region);
            b.AppendFormat("\tendpoint : {0},\n", this.endpoint);
            b.AppendFormat("\taccessKey : {0},\n", this.accessKey);
            b.AppendFormat("\tsecretKey : {0},\n", this.secretKey);
            b.AppendFormat("\tsessionId : {0}", this.sessionId);
            return b.ToString();
        }

        public static ModlAWSCredentials FromEnvironment()
        {
            var endPointUriString = Environment.GetEnvironmentVariable("OBS_URL");
            if (string.IsNullOrWhiteSpace(endPointUriString))
            {
                Debug.Log("[MODL] Warning! Missing Environment Variable: 'OBS_URL'");
                //Return empty credentials, which will make IsValid() return false.
                return new ModlAWSCredentials();
            }
                
            var endPointUri = new System.Uri(endPointUriString);
            var endPointPath = endPointUri.AbsolutePath;

            return new ModlAWSCredentials()
            {
                scheme = endPointUri.Scheme,
                host = endPointUri.DnsSafeHost,
                region = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION"),
                endpoint = endPointPath,
                accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                sessionId = Environment.GetEnvironmentVariable("OBS_SESSION")
            };
        }

    }
}