using System.Collections.Generic;
using System.Text;
using Modl.Proto;
using Google.Protobuf;

namespace Modl.Internal
{
    public class LocalObservationConsumer : IObservationConsumer
    {
        public List<string> Observations { get; private set; }

        public bool Initialize()
        {
            Observations = new List<string>();
            return true;
        }

        public void OnObservation(Observation observation)
        {
            var jsonObservation = JsonFormatter.Default.Format(observation);
            Observations.Add(jsonObservation);
        }

        public void Deinitialize()
        {
            Observations.Clear();
        }

        public bool IsDone() => true;

        public override string ToString()
        {
            var builder = new StringBuilder();
            var progressiveId = 0;
            foreach (var obs in Observations)
            {
                builder.Append($"[{progressiveId++}] {obs}\n");
            }

            return builder.ToString();
        }
    }
}
