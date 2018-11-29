using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DetectorDeFalhasUDP
{
    public class Program
    {
        public static void Main(string[] args)
        {
            all = new List<string>(all);
            all.Add("172.18.2.217");
            all.Add("172.18.3.162");
            all.Add("172.18.1.41");
            all.Add("172.18.1.52");
            all.Add("172.17.128.81");
            all.Add("172.18.3.79");
            all.Add("172.18.3.78");
            live = new List<string>(all);
            dead = new List<string>();

            leader = live.FirstOrDefault();

            new Thread(() => Sender()).Start();
            new Thread(() => Receiver()).Start();
        }

        private static readonly int SEND_PORT = 6001;
        private static readonly int RECEIVE_PORT = 6000;

        private static readonly string REQUEST_MESSAGE = "HeartbeatRequest";
        private static readonly string REPLY_MESSAGE = "HeartbeatReply";

        private static readonly string PROCESS_REQUEST_MESSAGE = "ProcessRequest";
        private static readonly string PROCESS_INTERRUPT_MESSAGE = "ProcessInterruptRequest";
        private static readonly string PROCESS_ANSWER_NO_MESSAGE = "ProcessAnswerNo";
        private static readonly string PROCESS_ANSWER_YES_MESSAGE = "ProcessAnswerYes;{0}";
        private static readonly string PROCESS_MESSAGE = "Process;";

        private static volatile List<string> all;
        private static volatile List<string> live;
        private static volatile List<string> dead;
        private static string leader = string.Empty;

        private static volatile Mutex mutexFailure = new Mutex();
        private static bool isIdle = true;

        private static void Sender()
        {
            while (true)
            {
                mutexFailure.WaitOne();
                try
                {
                    dead.RemoveAll(x => live.Contains(x));
                    var except = all.Except(live).ToList();
                    except.ForEach(ip =>
                    {
                        if (!dead.Contains(ip))
                        {
                            dead.Add(ip);
                            Console.WriteLine(string.Format("Host {0} parou de responder!", ip));
                        }
                    });
                    leader = all.Except(dead).FirstOrDefault();
                    Console.WriteLine(string.Format("\n\n***** LIDER: {0} - {1} ****\n\n", all.IndexOf(leader) + 1, leader));

                    foreach (var ip in all)
                    {
                        SendMessage(ip, REQUEST_MESSAGE);
                    }
                    live.Clear();
                    if (isIdle)
                    {
                        isIdle = false;
                        SendMessage(leader, PROCESS_REQUEST_MESSAGE);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    mutexFailure.ReleaseMutex();
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
                mutexFailure.WaitOne();
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
                    if (message == REPLY_MESSAGE)
                    {
                        Console.WriteLine(string.Format("Host {0} respondeu {1}", ip, REPLY_MESSAGE));
                        live.Add(ip);
                        if (!dead.Contains(ip))
                            dead.Remove(ip);
                    }
                    if (message.Contains(PROCESS_MESSAGE))
                    {
                        // TODO
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    mutexFailure.ReleaseMutex();
                }
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
