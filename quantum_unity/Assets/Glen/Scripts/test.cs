using UnityEngine;
using Quantum;
using Photon.Deterministic;

public class test : MonoBehaviour
{
    public bool debug = false;

    private void Update()
    {
        if(debug)
        {

            
            Debug.Log("Sending Commanding to simulation");

            debug = false;
            /*
             CommandResetPosition command = new CommandResetPosition()
             {
                //Position = new Photon.Deterministic.FPVector3(
             };

             QuantumRunner.Default.Game.SendCommand(command);
            */

            float a = 1.12345678910f;

            UnityEngine.Debug.Log($"a == {a} before ");


            FP converted = FixedPointMath.FloatToFixed(a);

            

            UnityEngine.Debug.Log($"converted == {converted} after a ");
        }
    }
}
