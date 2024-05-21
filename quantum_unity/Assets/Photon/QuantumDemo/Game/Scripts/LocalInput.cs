using System;

using Photon.Deterministic;
using Quantum;
using UnityEngine;
using Modl;

public class LocalInput : MonoBehaviour 
{
    public Vector2 ModlRawMove;
    private void Start()
    {
        QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));

#if MODL_AUTOMATIC_TESTING
        if (!ModlPublicController.IsTransmitting)
        {
            Debug.Log("Starting to transmit");
            ModlPublicController.Start();
        }
#endif
    }

    public void PollInput(CallbackPollInput callback)
    {
        Quantum.Input input = new Quantum.Input();

        // Note: Use GetButton not GetButtonDown/Up Quantum calculates up/down itself.
        input.Jump = UnityEngine.Input.GetButton("Jump");

#if MODL_AUTOMATIC_TESTING
        var x = ModlRawMove.x;
        var y = ModlRawMove.y;
#endif
#if !MODL_AUTOMATIC_TESTING
        var x = UnityEngine.Input.GetAxis("Horizontal");
        var y = UnityEngine.Input.GetAxis("Vertical");
#endif
        // Input that is passed into the simulation needs to be deterministic that's why it's converted to FPVector2.
        input.Direction = new Vector2(x, y).ToFPVector2();

        callback.SetInput(input, DeterministicInputFlags.Repeatable);
    }
}
