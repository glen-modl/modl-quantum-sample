using Modl.Proto;
using System;
using Modl.Internal.Utils.AWS;
using UnityEngine;
using ENV = System.Environment;

namespace Modl.Internal
{

    public interface IObservationConsumer
    {
        bool Initialize();
        void OnObservation(Observation observation);
        void Deinitialize();
        bool IsDone();
    }

    
    public interface IObservationConsumerTest
    {
        int CurrentBufferSize();
        bool WasAnyMessageRefused();
    }

    public static class ObservationConsumerFactory
    {
        public static IObservationConsumer Create()
        {
            var endPoint = ENV.GetEnvironmentVariable("OBS_URL");
            var sessionId = ENV.GetEnvironmentVariable("OBS_SESSION");
            
            if (endPoint == null || sessionId == null)
            {
                return new LocalObservationConsumer();
            }

            return new SQSObservationConsumer();
        }

    }

    public class WaitForConsumer : CustomYieldInstruction
    {
        private readonly IObservationConsumer _consumer;
        private readonly DateTime _start;

        public WaitForConsumer(IObservationConsumer consumer)
        {
            _consumer = consumer;
            _start = DateTime.UtcNow;
            Debug.Log("Waiting for consumer shut down.");
        }

        public override bool keepWaiting
        {
            get
            {
                if (_consumer.IsDone())
                {
                    Debug.Log($"Done waiting for consumer ({(DateTime.UtcNow - _start).TotalSeconds} s).");
                }

                return !_consumer.IsDone();
            }
        }
    }
}