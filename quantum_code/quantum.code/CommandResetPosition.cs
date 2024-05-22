using Photon.Deterministic;

namespace Quantum
{
    public class CommandResetPosition : DeterministicCommand
    {
        public FPVector3 Position;

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref Position);
        }

        public void Execute(Frame f)
        {
        }
    }
}