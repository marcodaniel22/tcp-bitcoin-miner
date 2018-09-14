using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DetectorDeFalhasUDP
{
    public class Program
    {
        private static readonly int SEND_PORT = 6001;
        private static readonly int RECEIVE_PORT = 6000;

        private static volatile List<string> all = new List<string>();
        private static volatile List<string> live = new List<string>();
        private static volatile List<string> dead = new List<string>();

        private static volatile Mutex mutex = new Mutex();

        public static void Main(string[] args)
        {
            all.Add("172.18.2.208");
            all.Add("172.18.2.209");
            all.Add("172.18.3.100");
            all.Add("172.17.98.96");
            all.Add("172.18.1.52");
            live.AddRange(all);

            new Thread(() =>
            {
                Sender();
            }).Start();
            new Thread(() =>
            {
                Receiver();
            }).Start();
        }

        private static void Receiver()
        {
            UdpClient receivingUdpClient = new UdpClient(SEND_PORT);
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, RECEIVE_PORT);
            while (true)
            {
                mutex.WaitOne();
                try
                {
                    Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);
                    string returnData = receiveBytes[0].ToString();
                    var message = returnData.ToString();
                    var ip = RemoteIpEndPoint.Address.ToString();

                    if (message == "1")
                    {
                        Console.WriteLine(string.Format("Respondido '2' para {0}", ip));
                        SendMessage(ip, 2);
                    }
                    else if (message == "2")
                    {
                        Console.WriteLine(string.Format("Host {0} respondeu '2'", ip));
                        live.Add(ip);
                        if (!dead.Contains(ip))
                            dead.Remove(ip);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void Sender()
        {
            while (true)
            {
                mutex.WaitOne();
                try
                {
                    var except = all.Except(live).ToList();
                    except.ForEach(x => DeadHost(x));

                    foreach (var ip in all)
                    {
                        var message = 1;
                        Console.WriteLine(string.Format("Enviando '1' para o host {0}", ip));
                        SendMessage(ip, message);
                    }
                    live = new List<string>();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    mutex.ReleaseMutex();
                    Thread.Sleep(2000);
                }
            }
        }

        private static void DeadHost(string ip)
        {
            if (!dead.Contains(ip))
            {
                dead.Add(ip);
                Console.WriteLine(string.Format("Host {0} parou de responder!", ip));
            }

        }

        private static void SendMessage(string ip, int message)
        {
            using (UdpClient server = new UdpClient(ip, SEND_PORT))
            {
                byte[] senBytes = new byte[]{(byte)message};
                server.Send(senBytes, senBytes.Length);
            }
        }
    }
}
