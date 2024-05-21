using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Modl.Proto;
using UnityEngine;
using UnityEngine.Networking;

namespace Modl.Internal.Utils.AWS
{
    public class SQSObservationConsumer : IObservationConsumer, IObservationConsumerTest
    {
        public static bool VERBOSE = false;
        private const string pluginIdentity = "UnityPlugin";

        public int BatchSize
        {
            get => _batchSize;
            set
            {
                if (value >= 1)
                {
                    _batchSize = value;
                }
            }
        }

        private ModlAWSCredentials sharedCredentials;
        private int _batchSize = 1;
        public List<Message> buffer;
        private int totalObservations;

        public Action<IAWSRequest, SendTask> _taskOutcome;
        public List<SendTask> unfinishedTasks;
        private bool _wasMessageRefused;

        public bool Initialize()
        {
            _taskOutcome = TaskOutcome;
            _wasMessageRefused = false;
            buffer = new List<Message>();
            totalObservations = 0;
            unfinishedTasks = new List<SendTask>();
            sharedCredentials = ModlAWSCredentials.FromEnvironment();

            if (VERBOSE)
            {
                Debug.Log($"AWS settings: {sharedCredentials}");
            }
            
            if (!sharedCredentials.IsValid)
            {
                return false;
            }

            return true;
        }

        public void Deinitialize()
        {
            // checking for remaining messages to be sent
            if (buffer.Count > 0)
            {
                var bufferTask = new SendTask(buffer, sharedCredentials);
                ManageTask(bufferTask);
                buffer = new List<Message>();
            }

            // creating DONE message for SQS
            Message stop = new StopMessage(totalObservations)
            {
                timestamp = DateTime.UtcNow,
                sessionId = sharedCredentials.sessionId
            };
            var stopTask = new SendTask(new List<Message> {stop}, sharedCredentials);
            ManageTask(stopTask);

            // Resetting attributes
            sharedCredentials = new ModlAWSCredentials();
            totalObservations = 0;
            buffer = new List<Message>();
        }

        public void OnObservation(Observation observation)
        {
            var message = new ObservationMessage(observation)
            {
                timestamp = DateTime.UtcNow,
                sessionId = sharedCredentials.sessionId
            };

            buffer.Add(message);
            totalObservations++;

            if (buffer.Count >= BatchSize)
            {
                var bufferTask = new SendTask(buffer, sharedCredentials);
                ManageTask(bufferTask);
                buffer = new List<Message>();
            }

        }

        private void ManageTask(SendTask task)
        {
            unfinishedTasks.Add(task);
            // Debug.Log($"Doing Task {task} [{unfinishedTasks.Count}]");
            task.DoTask(_taskOutcome);
        }

        private void TaskOutcome(IAWSRequest request, SendTask task)
        {
            if (unfinishedTasks.Contains(task))
            {
                unfinishedTasks.Remove(task);
                // Debug.Log($"Finished Task {task} [{unfinishedTasks.Count}]");
            }
            else
            {
                Debug.LogError($"[WARNING] Unknown SendTask!");
            }

            if (request is ModlAWSRequest requestOperation)
            {
                var webRequest = requestOperation.webRequest;
                LogWebRequest(webRequest);
                webRequest.Dispose(); //TODO: this should probably go into ModlAWSRequest
            }
        }

        private void LogWebRequest(UnityWebRequest request)
        {
            if (request.responseCode != 200)
            {
                _wasMessageRefused = true;
            }
            
            if (!VERBOSE)
            {
                return;
            }

            switch (request.responseCode)
            {
                case 0:
                    Debug.Log($"[{request.responseCode}] Aborted - {request.url}");
                    break;
                case 200:
                    Debug.Log($"[{request.responseCode}] Ok - {request.url}");
                    break;
                case 400:
                    Debug.LogError($"[{request.responseCode}] Bad Request - {request.url}");
                    break;
                case 403:
                    Debug.LogError($"[{request.responseCode}] Forbidden - {request.url}");
                    break;
                case 404:
                    Debug.LogError($"[{request.responseCode}] Not Found - {request.url}");
                    break;
                case 500:
                    Debug.LogError($"[{request.responseCode}] Internal Server Error - {request.url}");
                    break;
                default:
                    Debug.LogError($"[Response Code: {request.responseCode}] {request.url}");
                    break;
            }

            if (_wasMessageRefused)
            {
                if (request.uploadHandler?.data != null)
                {
                    string dataSent = Encoding.UTF8.GetString(request.uploadHandler.data);
                    Debug.Log($"DataSent: {dataSent}");
                }

                var headers = request.GetResponseHeaders();
                var headersString = string.Empty;
                if(request.GetResponseHeaders() != null)
                {
                    headersString = string.Join(";", headers.Select(pair => $"{pair.Key}:{pair.Value}"));
                }
                
                var responseBody = request.downloadHandler.data != null ? Encoding.UTF8.GetString(request.downloadHandler.data) : string.Empty;
                Debug.Log($"Response received with\n\tcode {request.responseCode}\n\theaders {headersString}\n\tbody {responseBody}");
            }
        }

        public bool WasAnyMessageRefused() => _wasMessageRefused;

        public bool IsDone() => unfinishedTasks.Count == 0;

        public int CurrentBufferSize() => buffer.Count;

        public override string ToString()
        {
            return $"Observations received: {totalObservations}; Sending {CurrentBufferSize()} remaining messages to AWS SQS in batches of size: {_batchSize}.";
        }

        public abstract class Message
        {
            public DateTime timestamp;
            public string sessionId;
            public abstract override string ToString();
        }

        public class ObservationMessage : Message
        {
            public readonly Observation observation;

            public ObservationMessage(Observation observation)
            {
                this.observation = observation;
            }

            public override string ToString()
            {
                string obsEscaped = JsonFormatter.ToDiagnosticString(observation);
                return $"{sessionId}; {timestamp.ToUnixTime()}; {pluginIdentity}; OBSERVATION; {obsEscaped}";
            }
        }

        public class StopMessage : Message
        {
            public readonly int totalObservations;

            public StopMessage(int totalObservations)
            {
                this.totalObservations = totalObservations;
            }

            public override string ToString()
            {
                return
                    $"{sessionId}; {timestamp.ToUnixTime()}; {pluginIdentity}; DONE; {{\"total\":{totalObservations}}}";
            }
        }

        public class SendTask
        {
            public static readonly string service = "sqs";
            public static int taskIdGlobal = 0;
            public static IAWSClientFactory clientFactory = new ModlAWSClientFactory();

            public readonly IAWSClient client;
            public readonly ModlAWSCredentials credentials;
            public readonly List<Message> messages;
            public readonly int taskId;

            public SendTask(List<Message> messages, ModlAWSCredentials credentials)
            {
                this.messages = messages;
                this.credentials = credentials;
                this.client = clientFactory.get(credentials, service);
                this.taskId = taskIdGlobal++;
            }

            public override string ToString()
            {
                return $"SendTask[id:{taskId}]";
            }

            public void DoTask(Action<IAWSRequest, SendTask> action = null)
            {
                if (messages?.Count == 0)
                {
                    Debug.LogError("Trying to send a task with no messages!");
                    return;
                }

                IAWSRequest awsRequest;

                if (messages?.Count > 1)
                {
                    // BATCH of Messages
                    string jsonData;
                    string queueUrl = $"https://{service}.{credentials.region}.amazonaws.com/{credentials.endpoint}";
                    int id = 0;
                    var jsonMessages = new List<string>();
                    foreach (var message in messages)
                    {
                        string escapedMessage = HttpUtility.JavaScriptStringEncode(message.ToString());
                        jsonMessages.Add($"{{ \"MessageBody\":\"{escapedMessage}\",\"Id\":\"{id++}\"}}");
                    }

                    jsonData = $"{{ \"Entries\": [{string.Join(",", jsonMessages)}], \"QueueUrl\": \"{queueUrl}\"}}";
                    awsRequest = client.POSTRequest("SendMessageBatch", jsonData);
                }
                else
                {
                    // SINGLE Message
                    string messageString = messages[0].ToString();
                    var parameters = new SortedDictionary<string, string> {{"MessageBody", messageString}};
                    awsRequest = client.GETRequest("SendMessage", parameters);
                }

                // Start request and subscribe to action completion
                if (action != null)
                {
                    awsRequest.completed += request => action.Invoke(request, this);
                }
                awsRequest.SendRequest();
            }
        }
    }
}