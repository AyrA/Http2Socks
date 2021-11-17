using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace H2S
{
    public static class Tools
    {
        public const int DEFAULT_TIMEOUT = 5000;
        public static void Connect(this Socket S, EndPoint EP, int Timeout)
        {
            if (S is null)
            {
                throw new ArgumentNullException(nameof(S));
            }

            if (EP is null)
            {
                throw new ArgumentNullException(nameof(EP));
            }

            if (Timeout < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Value must be at least 1");
            }

            if (Task.WaitAny(S.ConnectAsync(EP), Task.Delay(Timeout)) != 0)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
        }

        public static void Cat(Socket A, Socket B)
        {
            using (var StreamA = new NetworkStream(A, true))
            {
                using (var StreamB = new NetworkStream(B, true))
                {
                    Log("Forwarder", $"Begin forwarding: {A.RemoteEndPoint}<-->{B.RemoteEndPoint}");
                    try
                    {
                        Task.WaitAny(StreamA.CopyToAsync(StreamB), StreamB.CopyToAsync(StreamA));
                        Log("Forwarder", $"Completed forwarding: {A.RemoteEndPoint}<-->{B.RemoteEndPoint}");
                    }
                    catch (Exception ex)
                    {
                        LogEx("HTTP<-->SOCKS copy error", ex);
                    }
                }
            }
        }

        public static void LogEx(string Message, Exception ex)
        {
            if (ex is null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            Console.Error.WriteLine("== Begin Error report ==");
            Console.Error.WriteLine(Message);
            Console.Error.WriteLine("Type: {0}", ex.GetType());
            Console.Error.WriteLine("Desc: {0}", ex.Message);
            Console.Error.WriteLine("== End Error report ==");
        }

        public static void Log(string Component, string Message)
        {
            Console.WriteLine("[{0}]: {1}", Component, Message);
        }
    }
}
