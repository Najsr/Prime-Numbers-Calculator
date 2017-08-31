using System;
using System.Collections.Concurrent;
using System.Threading;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using NetworkCommsDotNet.DPSBase;

namespace Prime_Numbers_Client
{
    class Client
    {
        private const ulong min = 2;
        private static ConcurrentQueue<ulong> primes = new ConcurrentQueue<ulong>();
        private static Connection newTCPConn;
        private static Thread[] threads;
        private static void GeneratePrimes(ulong start, ulong range)
        {
            var isPrime = true;
            var end = start + range;
            for (var i = start; i < end; i++)
            {
                for (var j = min; j < Math.Sqrt(end); j++)
                {
                    if (i != j && i % j == 0)
                    {
                        isPrime = false;
                        break;
                    }
                }
                if (isPrime)
                {
                    primes.Enqueue(i);
                }
                isPrime = true;
            }
        }
        static void Main(string[] args)
        {
            Console.SetOut(new PrefixedWriter());
            Console.WriteLine("Type server's ip with desired port in following format: 192.168.1.1:8001");
            try
            {
                string[] conn_string = Console.ReadLine().Split(':');
                newTCPConn = TCPConnection.GetConnection(new ConnectionInfo(conn_string[0], int.Parse(conn_string[1])));
                newTCPConn.EstablishConnection();

            } catch
            {
                Console.WriteLine("We couldn't connected, please check your connection!");
                Console.ReadLine();
                return;
            }
            newTCPConn.AppendIncomingPacketHandler<string>("Data", OnDataArrive);
            newTCPConn.AppendShutdownHandler(OnShutdown);
            RijndaelPSKEncrypter.AddPasswordToOptions(newTCPConn.ConnectionDefaultSendReceiveOptions.Options, "Strong Password xd");
            Console.Title = "CLIENT " + newTCPConn.ConnectionInfo.LocalEndPoint;
            Console.WriteLine("Connected to {0}", newTCPConn.ConnectionInfo.RemoteEndPoint);
            while (true)
                Console.ReadKey(true);
        }

        private static void OnDataArrive(PacketHeader header, Connection connection, string message)
        {
            if (!connection.ConnectionAlive()) return;
            ulong[] rozsah = Array.ConvertAll(message.Split(':'), s => ulong.Parse(s));
            Console.WriteLine("I received interval {0}:{1} - calculating", rozsah[0], rozsah[1]);
            CountPrimes(rozsah[0], rozsah[1]);
            try
            {
                newTCPConn.SendObject("Primes", primes.Count.ToString());
                Console.WriteLine("Results successfully sent. We found " + primes.Count + " prime numbers");
            }
            catch (CommunicationException)
            {
                Console.WriteLine("We failed to send " + primes.Count + " prime numbers :(");
            }
        }

        private static void OnShutdown(Connection connection)
        {
            Console.WriteLine("Connection to the server has been lost!");
            Console.Title = "CLIENT - DISCONNECTED";
            foreach (Thread thread in threads)
                thread.Abort();
            Console.ReadKey(true);
            Environment.Exit(0);
        }

        private static void CountPrimes(ulong min, ulong max)
        {
            primes = new ConcurrentQueue<ulong>();
            var threadCount = (ulong)Environment.ProcessorCount;
            threads = new Thread[threadCount];
            var range = (max - min) / threadCount;
            var start = min;
            for (ulong i = 0; i < threadCount - 1; i++)
            {
                var startl = start;
                threads[i] = new Thread(() => GeneratePrimes(startl, range));
                start += range;
                threads[i].Start();
            }
            threads[threadCount - 1] = new Thread(() => GeneratePrimes(start, range + (max - min) % threadCount));
            threads[threadCount - 1].Start();

            for (ulong i = 0; i < threadCount; i++)
                threads[i].Join();
        }
    }
}
