using Modl.Proto;

namespace Modl.Internal.DataCommunication
{
    public interface ICommunicator
    {
        bool Connect();
        bool Send(Observation observation);
        Command ReceiveCommand();
        Initialization ReceiveInit();
        void Close();
    }
}