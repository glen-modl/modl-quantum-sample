
using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Protobuf;
using Modl.Internal.Utils;
using Modl.Proto;
using UnityEngine;

namespace Modl.Internal.DataCommunication
{
    public class CommunicatorSocket : ICommunicator

    {
    private const string DEFAULT_PORT = "4242";
    private const string ENCODING_KEY = "UTF-8";
    private const char END_TOKEN = (char) 3;
    private const string IP = "127.0.0.1";
    private const int BUFFER_SIZE = 1024;

    private readonly Socket _socket;
    private readonly Encoding _encoding;
    private readonly IPEndPoint _localEndPoint;

    public CommunicatorSocket()
    {
        int port = ParsePort(UtilsEnvironment.GetEnvVariable("SOCKET_PORT"));

        _localEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);
        _socket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _encoding = Encoding.GetEncoding(ENCODING_KEY);
    }

    public bool Connect()
    {
        // at the moment there is no reason to continue if we can't connect
        // so we may as well just ignore the exception and die
        _socket.Connect(_localEndPoint);
        return true;
    }
    
    public void Close()
    {
        Debug.Log("Shutting down socket connection, with a 2 second timeout.");
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            const int milliSecondsBeforeClose = 2000;
            _socket.Close(milliSecondsBeforeClose);
        }
    }

    public bool Send(Observation observation)
    {
        string msg = JsonFormatter.Default.Format(observation);
        msg += END_TOKEN;

        byte[] byteData = _encoding.GetBytes(msg);
        _socket.Send(byteData);

        return true;
    }

    public Initialization ReceiveInit()
    {
        return Receive<Initialization>();
    }

    public Command ReceiveCommand()
    {
        return Receive<Command>();
    }

    private T Receive<T>() where T : IMessage, new()
    {
        T ret = new T();
        string msg = "";
        byte[] buffer = new byte[BUFFER_SIZE];
        while (msg.IndexOf(END_TOKEN) == -1)
        {
            _socket.Receive(buffer);
            msg += _encoding.GetString(buffer);
        }

        // you should never have multiple messages in the queue. Either wrong or we need to sync better
        // if things change (aka, multiple commands and such) need to negotiate how
        var msgList = msg.Split(END_TOKEN);
        msg = msgList[msgList.Length - 2];

        ret = JsonParser.Default.Parse<T>(msg);
        return ret;
    }

    private static int ParsePort(string port) => int.Parse(port ?? DEFAULT_PORT);
    
    }
}
