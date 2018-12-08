using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
        private static readonly string PROCESS_MESSAGE = "Process;";

        private static volatile List<string> all;
        private static volatile List<string> live;
        private static volatile List<string> dead;
        private static volatile string leader = string.Empty;

        private static volatile Mutex mutexFailure = new Mutex();
        private static bool isIdle = true;
        private static Thread ProcessThread;

        public static void Main(string[] args)
        {
            all = new List<string>();
            all.Add("172.18.2.234");
            all.Add("172.18.1.51");

            live = new List<string>(all);
            dead = new List<string>();

            leader = live.FirstOrDefault();

            new Thread(() => Sender()).Start();
            new Thread(() => Receiver()).Start();
        }

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
                    SetLeader(all.Except(dead).FirstOrDefault());
                    Console.WriteLine(string.Format("\n\n***** LIDER: {0} - {1} ****\n\n", all.IndexOf(leader) + 1, leader));

                    foreach (var ip in all)
                    {
                        SendMessage(ip, REQUEST_MESSAGE);
                    }
                    live.Clear();
                    if (isIdle)
                    {
                        Console.WriteLine(string.Format("Enviando {0} para {1}", PROCESS_REQUEST_MESSAGE, leader));
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

        private static void SetLeader(string newLeader)
        {
            if (ProcessThread != null)
            {
                ProcessThread.Abort();
                ProcessThread = null;
                Console.WriteLine(string.Format("Troca de lider {0} para {1}", leader, newLeader));
            }
            isIdle = true;
            leader = newLeader;
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
                    else if (message == REPLY_MESSAGE)
                    {
                        Console.WriteLine(string.Format("Host {0} respondeu {1}", ip, REPLY_MESSAGE));
                        live.Add(ip);
                        if (!dead.Contains(ip))
                            dead.Remove(ip);
                    }
                    else if (message == PROCESS_INTERRUPT_MESSAGE && ProcessThread != null)
                    {
                        Console.WriteLine(string.Format("Host {0} respondeu {1}", leader, PROCESS_INTERRUPT_MESSAGE));
                        ProcessThread.Abort();
                        ProcessThread = null;
                        isIdle = true;
                    }
                    else if (message.Contains(PROCESS_MESSAGE) && isIdle && (ProcessThread == null || (ProcessThread != null && !ProcessThread.IsAlive)))
                    {
                        Console.WriteLine(string.Format("Host {0} respondeu {1}", leader, message));
                        isIdle = false;
                        ProcessThread = new Thread(() =>
                        {
                            var splitedValues = message.Split(';');
                            var begin = int.Parse(splitedValues[1]);
                            var end = int.Parse(splitedValues[2]);
                            var timeStamp = splitedValues[3];
                            var hash = splitedValues[4];
                            var zeros = int.Parse(splitedValues[5]);

                            using (SHA256 sha = SHA256.Create())
                            {
                                bool hashFound = false;
                                for (int i = begin; i < end; i++)
                                {
                                    var shaHash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Format("{0}{1}{2}", hash, i, timeStamp)));
                                    for (int j = 0; j < zeros; j++)
                                    {
                                        if (shaHash[j] != 0)
                                            break;
                                        else if (j == (zeros - 1))
                                        {
                                            hashFound = true;
                                            Console.WriteLine(string.Format("Hash encontrada em '{0}'!", i));
                                            SendMessage(leader, string.Format(PROCESS_ANSWER_YES_MESSAGE, i));
                                            break;
                                        }
                                    }
                                    if (hashFound)
                                        break;
                                }
                                if (!hashFound)
                                    SendMessage(leader, PROCESS_ANSWER_NO_MESSAGE);
                            }
                            isIdle = true;
                        });
                        ProcessThread.Start();
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
            if (!string.IsNullOrEmpty(ip))
                using (UdpClient server = new UdpClient(ip, SEND_PORT))
                {
                    var bytes = Encoding.ASCII.GetBytes(message);
                    server.Send(bytes, bytes.Length);
                }
        }
    }
}
