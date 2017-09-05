using System;
using System.Linq;
using NetworkCommsDotNet.Connections;

namespace Prime_Numbers_Server
{
    class Connecting_Client
    {
        private byte[] ver = new byte[4];

        public byte seconds { get; private set; }

        public Connecting_Client(Connection con, int secs)
        {
            connection = con;
            seconds = (byte)secs;
            new Random().NextBytes(ver);
        }

        public void SendHandShake1()
        {
            if (ver.Length != 4)
                throw new Exception("Size of HS must be 4 bytes!");

            connection.SendObject("HS2", ver);
        }


        public bool ProcessHandShake1(byte[] bytes)
        {
            byte[] check = ver;
            Array.Reverse(check);
            if (check.SequenceEqual(bytes))
            {
                return true;
            }
            return false;
        }

        public Connection connection { get; private set; }
    }
}
