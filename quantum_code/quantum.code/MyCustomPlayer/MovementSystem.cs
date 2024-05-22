
using Photon.Deterministic;

namespace Quantum
{
    public unsafe class MovementSystem : SystemMainThreadFilter<MovementSystem.Filter>,  ISignalOnPlayerPositionReset
    {
        public struct Filter
        {
            public EntityRef Entity;
            public CharacterController3D* CharacterController;
        }

        public override void Update(Frame f, ref Filter filter)
        {
            Input input = default;
            if (f.Unsafe.TryGetPointer(filter.Entity, out PlayerLink* playerLink))
            {
                input = *f.GetPlayerInput(playerLink->Player);
            }

            if (input.Jump.WasPressed)
            {
                filter.CharacterController->Jump(f);
            }

            filter.CharacterController->Move(f, filter.Entity, input.Direction.XOY);
        }

        public void OnPlayerPositionReset(Frame f, FPVector3 resetPosition)
        {
            foreach (var (entity, actor) in f.Unsafe.GetComponentBlockIterator<PlayerLink>())
            {
                if (f.Unsafe.TryGetPointer<Transform3D>(entity, out var transform))
                {
                    transform->Position = resetPosition;
                }

                Log.Info($"OnPlayerPositionReset received value {resetPosition}");
            }
        }
    }
}
