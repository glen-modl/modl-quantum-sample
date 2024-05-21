using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Modl.Internal.Utils.AWS;
using UnityEngine.Networking;

namespace Modl.Internal.Utils.AWS
{
    public class ModlAWSClient : IAWSClient
    {
        private const string ISOFormat = "yyyyMMdd'T'HHmmss'Z'";
        private const string dateOnlyFormat = "yyyyMMdd";
        private readonly ModlAWSCredentials credentials;
        private readonly string service;

        struct RequestMetaData
        {
            public string region;
            public string canonicalUri;
            public string canonicalQueryString;
            public string accessKey;
            public string secretKey;
            public string algorithm;
            public DateTime date;
            public string credentialScope;
            public string sessionToken;

            public override string ToString()
            {
                return $"\tregion: {region}\n\tcanonicalUri: {canonicalUri}\n" +
                       $"\tcanonicalQueryString: {canonicalQueryString}\n\taccessKey: {accessKey}\n" +
                       $"\tsecretKey: {secretKey}\n\talgorithm: {algorithm}\n" +
                       $"\tdate: {date}\n\tcredentialScope: {credentialScope}\n" +
                       $"\tsessionToken: {sessionToken}";
            }
        }

        public ModlAWSClient(ModlAWSCredentials credentials, string service)
        {
            this.credentials = credentials;
            this.service = service;
            if (credentials.endpoint.StartsWith("/"))
            {
                credentials.endpoint = credentials.endpoint.Remove(0, 1);
            }
        }

        private RequestMetaData BuildMetaData(string action, SortedDictionary<string, string> parameters = null)
        {
            var meta = new RequestMetaData
            {
                region = credentials.region,
                accessKey = credentials.accessKey,
                secretKey = credentials.secretKey,
                canonicalUri = credentials.endpoint,
                algorithm = "AWS4-HMAC-SHA256",
                date = DateTime.UtcNow
            };

            meta.credentialScope = string.Join("/",
                meta.date.ToString(dateOnlyFormat),
                credentials.region, service,
                "aws4_request"
            );

            var builder = new StringBuilder();
            builder.Append($"Action={action}");
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    builder.AppendFormat($"&{AWSEscape(pair.Key)}={AWSEscape(pair.Value)}");
                }
            }

            meta.canonicalQueryString = builder.ToString();

            return meta;
        }

        private string BuildCanonicalQueryString(RequestMetaData meta, SortedDictionary<string, string> headers)
        {
            var builder = new StringBuilder();

            builder.Append(meta.canonicalQueryString);
            builder.Append($"&X-Amz-Algorithm={meta.algorithm}");
            builder.AppendFormat("&X-Amz-Credential={0}", AWSEscape($"{meta.accessKey}/{meta.credentialScope}"));
            builder.AppendFormat("&X-Amz-Date={0}", meta.date.ToString(ISOFormat));
            builder.Append("&X-Amz-Expires=30");

            if (!string.IsNullOrEmpty(meta.sessionToken))
            {
                builder.AppendFormat("&X-Amz-Security-Token={0}", AWSEscape(meta.sessionToken));
            }

            builder.AppendFormat("&X-Amz-SignedHeaders={0}", string.Join(",", headers.Keys));

            return builder.ToString();
        }

        public IAWSRequest GETRequest(string action, SortedDictionary<string, string> parameters)
        {
            // *************      TASK 1: CREATE A CANONICAL REQUEST        *************
            var metaData = BuildMetaData(action, parameters);
            var headers = new SortedDictionary<string, string>
            {
                {"host", credentials.host}
            };
            string canonicalQueryString = BuildCanonicalQueryString(metaData, headers);
            string canonicalRequest = BuildCanonicalRequestString(
                "GET",
                metaData.canonicalUri,
                canonicalQueryString,
                headers
            );

            // *************       TASK 2: CREATE THE STRING TO SIGN        *************
            string stringToSign = BuildStringToSign(metaData, canonicalRequest);

            // *************        TASK 3: CALCULATE THE SIGNATURE         *************
            byte[] signingKey = BuildSignatureKey(metaData, service);
            string signature = BuildSignature(signingKey, stringToSign);


            // ************* TASK 4: ADD SIGNING INFORMATION TO THE REQUEST *************
            var signedCanonicalQuerystring = canonicalQueryString + "&X-Amz-Signature=" + signature;
            //Keeping the string.Format below to make the url format more readable
            var url = $"{credentials.scheme}://{credentials.host}/{credentials.endpoint}?{signedCanonicalQuerystring}";
            var webRequest = UnityWebRequest.Get(url);
            var awsRequest = new ModlAWSRequest(webRequest);
            
            return awsRequest;
        }

        private string BuildStringToSign(RequestMetaData meta, string canonicalRequest)
        {
            var builder = new StringBuilder();
            builder.Append($"{meta.algorithm}\n");
            builder.Append($"{meta.date.ToString(ISOFormat)}\n");
            builder.Append($"{meta.credentialScope}\n");
            var hash = Hash256(canonicalRequest);
            builder.Append(BytesToHexLower(hash));

            return builder.ToString();
        }

        public IAWSRequest POSTRequest(string action, string body)
        {
            // *************      TASK 1: CREATE A CANONICAL REQUEST        *************
            RequestMetaData metaData = BuildMetaData(action);
            var headers = new SortedDictionary<string, string>
            {
                {"Content-Type", "application/x-amz-json-1.0"},
                {"X-Amz-Date", metaData.date.ToString(ISOFormat)},
                {"X-Amz-Target", $"Amazon{service.ToUpper()}.{action}"},
                {"Host", credentials.host}
            };
            var canonicalRequest = BuildCanonicalRequestString("POST", string.Empty, string.Empty, headers, body);

            // *************       TASK 2: CREATE THE STRING TO SIGN        *************
            var stringToSign = BuildStringToSign(metaData, canonicalRequest);

            // *************        TASK 3: CALCULATE THE SIGNATURE         *************
            var signingKey = BuildSignatureKey(metaData, service);
            var signature = BuildSignature(signingKey, stringToSign);

            // ************* TASK 4: ADD SIGNING INFORMATION TO THE REQUEST *************
            var url = $"{credentials.scheme}://{credentials.host}/";
            var lowerCaseHeaders = headers.Keys.Select(hKey => hKey.ToLower()).ToList();

            var authorizationValue = string.Format( //keeping the string.Format to make the format more readable
                "{0} Credential={1}/{2}/{3}/{4}/aws4_request, SignedHeaders={5}, Signature={6}",
                metaData.algorithm,
                metaData.accessKey,
                metaData.date.ToString(dateOnlyFormat),
                metaData.region,
                service.ToLower(),
                string.Join(";", lowerCaseHeaders),
                signature
            );

            var webRequest = new UnityWebRequest(url)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                method = UnityWebRequest.kHttpVerbPOST
            };

            foreach (var headerEntry in headers)
            {
                webRequest.SetRequestHeader(headerEntry.Key, headerEntry.Value);
            }

            webRequest.SetRequestHeader("Authorization", authorizationValue);
            return new ModlAWSRequest(webRequest);
        }

        private string BuildCanonicalRequestString(string verb, string canonicalUrl, string canonicalQueryString,
            SortedDictionary<string, string> headers, string payloadMessage = "")
        {
            // We may need to handle this more gracefully
            if (verb == null || canonicalUrl == null)
            {
                return string.Empty;
            }

            if (!verb.Equals("GET") && !verb.Equals("POST") && !verb.Equals("PUT"))
            {
                return string.Empty;
            }

            if (canonicalUrl.StartsWith("/"))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            // 1 HTTP verb
            builder.Append($"{verb}\n");

            // 2 Canonical URI
            builder.Append($"/{canonicalUrl}\n");

            // 3 Canonical query string
            builder.Append($"{canonicalQueryString}\n");

            // 4 Canonical headers
            foreach (var entryPair in headers)
            {
                builder.AppendFormat($"{entryPair.Key.ToLower()}:{entryPair.Value.Trim()}\n");
            }

            builder.Append("\n");

            // 5 Signed headers
            var lowerCaseHeaders = headers.Keys.Select(hKey => hKey.ToLower()).ToList();
            string signedHeaders = string.Join(";", lowerCaseHeaders);
            builder.Append($"{signedHeaders}\n");

            // 6 Hashed payload
            var hash = Hash256(payloadMessage);
            var hashString = BytesToHexLower(hash);
            builder.Append(hashString);

            return builder.ToString();
        }

        private byte[] BuildSignatureKey(RequestMetaData meta, string service)
        {
            var key = "AWS4" + meta.secretKey;
            var encodedString = Encoding.UTF8.GetBytes(key);
            var dateSigned = Sign(encodedString, meta.date.ToString(dateOnlyFormat));
            var regionSigned = Sign(dateSigned, meta.region);
            var serviceSigned = Sign(regionSigned, service);
            return Sign(serviceSigned, "aws4_request");
        }

        private string BuildSignature(byte[] key, string toSign)
        {
            return BytesToHexLower(Sign(key, toSign));
        }

        private static byte[] Hash256(string stringToHash)
        {
            var encodedString = Encoding.UTF8.GetBytes(stringToHash);
            var sha256 = new SHA256Managed();
            return sha256.ComputeHash(encodedString);
        }

        private static byte[] Sign(byte[] key, string dataToSign)
        {
            var dataToBytes = Encoding.UTF8.GetBytes(dataToSign);
            var hmac = new HMACSHA256(key);
            var signedData = hmac.ComputeHash(dataToBytes);
            return signedData;
        }

        private static string BytesToHexLower(byte[] data)
        {
            return string.Join("", data.Select(b => b.ToString("X2"))).ToLower();
        }

        private static string AWSEscape(string stringToEscape)
        {
            var builder = new StringBuilder();
            var escaped = UnityWebRequest.EscapeURL(stringToEscape).Replace("+", "%20");
            for (var i = 0; i < escaped.Length; i++)
            {
                var curr = escaped.ElementAt(i);
                if (ShouldSkip(curr))
                {
                    builder.Append(curr);
                }
                else if (curr == '%')
                {
                    var code = escaped.Substring(i, 3);
                    builder.Append(code.ToUpper());
                    i += 2;
                }
                else
                {
                    string currEscaped;
                    try
                    {
                        currEscaped = Uri.HexEscape(curr);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        currEscaped = Uri.EscapeUriString(curr.ToString());
                    }

                    builder.Append(currEscaped);
                }
            }

            return builder.ToString();
        }

        private static bool ShouldSkip(char c)
        {
            // 'A'-'Z' + 'a'-'z' + '0'-'9'
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }

            // '-' + '.', '_', and '~'.
            return c == '-' || c == '.' || c == '_' || c == '~';
        }
    }
}

public class ModlAWSClientFactory:IAWSClientFactory
{
    public IAWSClient get(ModlAWSCredentials credentials, string service)
    {
        return new ModlAWSClient(credentials, service);
    }
}