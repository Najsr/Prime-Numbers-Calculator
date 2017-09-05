using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using System.Diagnostics;
using System.Timers;

namespace Prime_Numbers_Server
{
    class Server
    {
        static List<Connecting_Client> connecting_clients = new List<Connecting_Client>();
        static List<Connection> connections = new List<Connection>();
        static int count = 0;
        private static int prime_count = -1;
        private static Timer timer = new Timer(1000);
        private static int results_received = 0;
        private static ulong min = 2;
        private static ulong max = 3;
        private static Stopwatch sw = new Stopwatch();
        private static uint port = 0;
        private static bool isRunning = false;
        static void Main(string[] args)
        {
            Console.SetOut(new PrefixedWriter());
            Console.WriteLine("Enter port to listen on");
            try
            {
                port = uint.Parse(Console.ReadLine());
                Connection.StartListening(ConnectionType.TCP, new IPEndPoint(IPAddress.Any, (int)port));
                timer.Elapsed += TimerPopped;
                timer.Enabled = true;
            }
            catch
            {
                Console.WriteLine("You have entered an invalid port");
                Console.ReadLine();
                return;
            }
            NetworkComms.AppendGlobalIncomingPacketHandler<string>("Primes", PrimeNumberReceived);
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("HS1", HSReceived);
            NetworkComms.AppendGlobalConnectionEstablishHandler(ClientConnected);
            NetworkComms.AppendGlobalConnectionCloseHandler(ClientDisconnected);
            Console.Title = "SERVER - Count: " + count;
            Console.WriteLine("for commands type: help");
            while (true)
            {
                if (isRunning)
                {
                    Console.ReadKey(true);
                    continue;
                }
                string command = Console.ReadLine();
                command = System.Text.RegularExpressions.Regex.Replace(command, @"\s+", "");
                if (command.Equals("count", StringComparison.InvariantCultureIgnoreCase))
                    Console.WriteLine("Number of connected clients: " + NetworkComms.GetExistingConnection().Count);
                else if (command.Equals("prime_start", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (connections.Count.Equals(0))
                    {
                        Console.WriteLine("You can't start without clients!");
                        continue;
                    }
                    string[] data = Divide(min, max, (ulong)connections.Count);
                    if (data == null)
                    {
                        Console.WriteLine("Invalid number range!");
                        continue;
                    }
                    results_received = 0;
                    prime_count = -1;
                    if (sw.IsRunning) sw.Stop();
                    sw.Reset();
                    Console.WriteLine("Sending {0} intervals - {1}", data.Count(), string.Join(", ", data));
                    isRunning = true;
                    sw.Start();
                    for (int i = 0; i < connections.Count; i++)
                    {
                        connections[i].SendObject("Data", data[i]);
                    }
                }
                else if (command.Equals("prime_set", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Type range in following format: 2:100000 - Actual range: {0}:{1}", min, max);
                    string rozsah = Console.ReadLine();
                    try
                    {
                        min = ulong.Parse(rozsah.Split(':').First());
                        max = ulong.Parse(rozsah.Split(':').Last());
                        if (min > max || min == max || min < 2 || max < 3)
                        {
                            min = 2;
                            max = 3;
                            Console.WriteLine("Invalid range! Smallest possible is: 2:3");
                            continue;
                        }
                        Console.WriteLine("Range successfully set {0}:{1}", min, max);
                    }
                    catch
                    {
                        Console.WriteLine("Invalid format");
                    }
                }
                else if (command.Equals("result", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (prime_count != -1)
                        Console.WriteLine("Latest calculation within interval {0}:{1} found {2} prime numbers. It took {3}", min, max, prime_count, sw.Elapsed);
                    else
                        Console.WriteLine("There was no calculation started yet");
                }
                else if (command.Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("\r\n'count' - will print count of currently connected clients \r\n'prime_set' - enables range input \r\n'prime_start' - will start calculation \r\n'result' - will print latest count of prime numbers");
                }
            }
        }

        private static void TimerPopped(object sender, ElapsedEventArgs e)
        {
            int second = DateTime.Now.Second;
            Connection[] clients = new Connection[connecting_clients.Count];
            int i = 0;
            foreach (Connecting_Client client in connecting_clients)
            {
                if (second > client.seconds + 1 || (client.seconds >= 58 && client.seconds > second))
                {
                    clients[i] = client.connection;
                }
                i++;
            }
            foreach (var cl in clients)
            {
                cl.CloseConnection(false);
                connecting_clients.RemoveAll(x => x.connection == cl);
            }
        }
        private static byte[] InitHS = new byte[] { 0x52, 0x45, 0x41, 0x44, 0x59 };

        private static void HSReceived(PacketHeader packetHeader, Connection connection, byte[] incomingObject)
        {
            if (incomingObject.Length == 5 && incomingObject.SequenceEqual(InitHS))
            {
                connecting_clients.Find(x => x.connection == connection).SendHandShake1();
                return;
            }
            if (incomingObject.Length == 4)
            {
                var conn = connecting_clients.Find(x => x.connection == connection);
                if (conn.ProcessHandShake1(incomingObject))
                {
                    connecting_clients.Remove(conn);
                    connections.Add(connection);
                    count++;
                    Console.WriteLine("Client " + connection.ConnectionInfo.RemoteEndPoint + " conected");
                    Console.Title = "SERVER - Count: " + count;
                }
                else
                    connection.CloseConnection(true);
            }
        }

        private static void ClientConnected(Connection connection)
        {
            if (isRunning)
            {
                connection.CloseConnection(false);
                return;
            }
            connecting_clients.Add(new Connecting_Client(connection, DateTime.Now.Second));
        }
        private static bool isRemoving = false;

        private static void ClientDisconnected(Connection connection)
        {
            if (isRunning && connections.Any(x => x == connection))
            {
                Console.WriteLine("Calculation was interrupted by {0} (disconnected)! Disconnecting all clients", connection.ConnectionInfo.RemoteEndPoint);
                connections.Remove(connection);
                count--;
                Console.Title = "SERVER - Count: " + count;
                isRunning = false;
                isRemoving = true;
                foreach (Connection c in connections)
                    c.CloseConnection(false);
                isRemoving = false;
                connections.Clear();
                return;
            }
            else if (!connections.Any(x => x == connection))
            {
                return;
            }
            else if (isRemoving)
            {
                Console.WriteLine("Client " + connection.ConnectionInfo.RemoteEndPoint + " disconnected - CANCELLING OPERATION");
                count--;
                Console.Title = "SERVER - Count: " + count;
                return;
            }
            Console.WriteLine("Client " + connection.ConnectionInfo.RemoteEndPoint + " disconnected");
            connections.Remove(connection);
            count--;
            Console.Title = "SERVER - Count: " + count;
        }

        private static void PrimeNumberReceived(PacketHeader header, Connection connection, string message)
        {
            if (prime_count == -1)
                prime_count = 0;
            prime_count += int.Parse(message);
            results_received++;
            if (results_received == connections.Count)
            {
                sw.Stop();
                isRunning = false;
                Console.WriteLine("We found {0} prime numbers within interval {1}:{2}. It took {3}", prime_count, min, max, sw.Elapsed);
            }
        }

        private static string[] Divide(ulong min, ulong max, ulong parts)
        {
            ulong stepSize = (max - min) / parts;
            if (stepSize <= 0) return null;

            ulong mod = (max - min) % parts;
            var result = new List<Tuple<ulong, ulong>>();
            ulong begin = min;
            ulong end;
            for (ulong i = 0; i < parts; i++)
            {
                end = begin + stepSize;
                if (mod > 0)
                {
                    mod--;
                    end += 1;
                }
                var t = new Tuple<ulong, ulong>(begin, end);
                result.Add(t);
                begin = end;
            }
            return result.Select(x => string.Format("{0}:{1}", x.Item1.ToString(), x.Item2.ToString())).ToArray();
        }
    }
}
