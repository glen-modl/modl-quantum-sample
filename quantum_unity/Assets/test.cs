using UnityEngine;
using Quantum;

public class test : MonoBehaviour
{
    public bool debug = false;

    private void Update()
    {
        if(debug)
        {
            Debug.Log("Sending Commanding to simulation");

            debug = false;

            CommandSpawnEnemy command = new CommandSpawnEnemy()
            {
                enemyPrototypeGUID = 1231231,
            };

            QuantumRunner.Default.Game.SendCommand(command);
        }
    }
}
