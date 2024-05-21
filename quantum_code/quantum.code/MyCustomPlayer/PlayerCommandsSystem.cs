using Photon.Deterministic;

namespace Quantum
{
    public class PlayerCommandsSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            for (int i = 0; i < f.PlayerCount; i++)
            {
                var command = f.GetPlayerCommand(i) as CommandSpawnEnemy;
                command?.Execute(f);
            }
        }
    }
}
