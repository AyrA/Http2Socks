using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace H2S
{
    public static class SocksClient
    {
        public static Socket Open(IPEndPoint ServerAddress, string Ident, string Host, int Port, int Timeout = Tools.DEFAULT_TIMEOUT)
        {
            if (ServerAddress is null)
            {
                throw new ArgumentNullException(nameof(ServerAddress));
            }

            if (Ident is null)
            {
                throw new ArgumentNullException(nameof(Ident));
            }

            if (string.IsNullOrEmpty(Host))
            {
                throw new ArgumentException($"'{nameof(Host)}' cannot be null or empty.", nameof(Host));
            }

            if (Port < IPEndPoint.MinPort || Port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(Port));
            }

            var HostBytes = Encoding.UTF8.GetBytes(Host);
            if (HostBytes.Length > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(Host), "Host name too long");
            }

            Socket S = new Socket(ServerAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                S.Connect(ServerAddress, Timeout);
            }
            catch
            {
                S.Dispose();
                return null;
            }
            //Basic Request
            using (var NS = new NetworkStream(S, false))
            {
                using (var BW = new BinaryWriter(NS, Encoding.UTF8, true))
                {
                    //Version
                    BW.Write((byte)0x04);
                    //Request to connect to TCP host
                    BW.Write((byte)0x01);
                    //TCP Port
                    BW.Write((byte)(Port >> 8));
                    BW.Write((byte)(Port & 0xFF));
                    //Fake IP
                    BW.Write(new byte[] { 0, 0, 0, (byte)HostBytes.Length });
                    //Ident
                    BW.Write(Encoding.UTF8.GetBytes(Ident));
                    BW.Write((byte)0);
                    //Hostname
                    BW.Write(HostBytes);
                    BW.Write((byte)0);
                    BW.Flush();
                }
                NS.Flush();
                using (var BR = new BinaryReader(NS, Encoding.UTF8, true))
                {
                    var Response = BR.ReadBytes(8);
                    if (Response[0] != 0)
                    {
                        throw new FormatException("Invalid SOCKS response");
                    }
                    if (Response[1] != 0x5A)
                    {
                        throw new FormatException("Request rejected or failed");
                    }
                }
            }
            return S;
        }
    }
}
