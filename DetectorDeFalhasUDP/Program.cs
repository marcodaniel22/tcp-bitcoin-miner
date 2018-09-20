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

        private static readonly string REQUEST_MESSAGE = "HeartbeatRequest";
        private static readonly string REPLY_MESSAGE = "HeartbeatReply";
        private static readonly string PROCESS_REQUEST_MESSAGE = "ProcessRequest";
        private static readonly string PROCESS_INTERRUPT_MESSAGE = "ProcessInterruptRequest";
        private static readonly string PROCESS_ANSWER_NO_MESSAGE = "ProcessAnswerNo";
        private static readonly string PROCESS_ANSWER_YES_MESSAGE = "ProcessAnswerYes;{0}";
        private static readonly string PROCESS_MESSAGE = "Process;{0};{1}";

        private static volatile List<string> all = new List<string>();
        private static volatile List<string> live = new List<string>();
        private static volatile List<string> dead = new List<string>();
        private static volatile string leader = string.Empty;

        private static volatile Mutex mutex = new Mutex();

        public static void Main(string[] args)
        {
            all.Add("172.18.2.217");
            all.Add("172.18.3.162");
            all.Add("172.18.1.41");
            all.Add("172.18.1.52");
            all.Add("172.17.128.81");
            all.Add("172.18.3.79");
            all.Add("172.18.3.78");
            
            live.AddRange(all);
            leader = live.FirstOrDefault();

            new Thread(() =>
            {
                Sender();
            }).Start();
            new Thread(() =>
            {
                Receiver();
            }).Start();
        }

        private static void Sender()
        {
            while (true)
            {
                mutex.WaitOne();
                try
                {
                    dead.RemoveAll(x => live.Contains(x));
                    var except = all.Except(live).ToList();
                    except.ForEach(x => DeadHost(x));
                    leader = all.Except(dead).FirstOrDefault();
                    Console.WriteLine(string.Format("\n\n***** LIDER: {0} - {1} ****\n\n", all.IndexOf(leader) + 1, leader));

                    foreach (var ip in all)
                    {
                        SendMessage(ip, REQUEST_MESSAGE);
                    }
                    live.Clear();
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
                    var message = Encoding.ASCII.GetString(receiveBytes);
                    var ip = RemoteIpEndPoint.Address.ToString();

                    if (message == REQUEST_MESSAGE)
                    {
                        Console.WriteLine(string.Format("Respondido {0} para {1}", REPLY_MESSAGE, ip));
                        SendMessage(ip, REPLY_MESSAGE);
                    }
                    else if (message == REPLY_MESSAGE)
                    {
                        Console.WriteLine(string.Format("Host {0} respondeu {1}", ip, REPLY_MESSAGE));
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

        private static void DeadHost(string ip)
        {
            if (!dead.Contains(ip))
            {
                dead.Add(ip);
                Console.WriteLine(string.Format("Host {0} parou de responder!", ip));
            }

        }

        private static void SendMessage(string ip, string message)
        {
            using (UdpClient server = new UdpClient(ip, SEND_PORT))
            {
                var bytes = Encoding.ASCII.GetBytes(message);
                server.Send(bytes, bytes.Length);
            }
        }
    }
}
