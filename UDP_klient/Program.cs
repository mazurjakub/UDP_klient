using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UDP_klient
{
    public class Program
    {
        public static IPEndPoint ServerEndPoint = new IPEndPoint(IPAddress.Parse("3.143.208.24"), 1700);
        public static UdpClient UDPClient = new UdpClient();
        public static Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        static void Main(string[] args)
        {
            UDPClient.AllowNatTraversal(true);
            UDPClient.Client.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
            UDPClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UDPClient.Connect(ServerEndPoint);

            //Start recieving data from server
            Thread recieve = new Thread(() => RecieveDataFromEP(ServerEndPoint));
            recieve.Start();

            string key = null;

            while (true)
            {
                
                // send data
                if (key == null)
                {
                    Console.WriteLine("Zadej klic v hodnotach od 0-999 pro pripojeni ke klientovi");
                    key = Console.ReadLine();
                    SendDataToServer(key);
                    Console.WriteLine("\n");
                }
                else
                {
                    Console.WriteLine("Pro vymazani ze serveru zadejte '0'\n");
                    key = Console.ReadLine();
                    SendDataToServer(key);
                    Console.WriteLine("\n");
                }
            }
        }

        //Sends string to connected server (ServerEndPoint)
        public static void SendDataToServer(string dataToSend)
        {
            try
            {
                int byteCount = Encoding.ASCII.GetByteCount(dataToSend);
                byte[] sendData = Encoding.ASCII.GetBytes(dataToSend);
                UDPClient.Send(sendData, byteCount);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            
        }

        public static void SendDataToServer(int dataToSend)
        {
            try
            {
                byte[] sendData = BitConverter.GetBytes(dataToSend);
                UDPClient.Send(sendData, sendData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

        }

        public static void RecieveDataFromEP(IPEndPoint endPoint)
        {
            while (true)
            {
                byte[] receivedData;
                int recv = 0;
                receivedData = UDPClient.Receive(ref endPoint);

                foreach (byte b in receivedData)
                {
                    if (b != 0)
                    {
                        recv++;
                    }
                }

                string request = Encoding.UTF8.GetString(receivedData, 0, recv);

                Thread.Sleep(100);

                Console.WriteLine("Prichozi zprava z IP: " + endPoint.Address.ToString() + " Port: " + endPoint.Port.ToString());
                Console.WriteLine("Obsah zpravy: " + request);
            }
        }


        // From http://stackoverflow.com/questions/6803073/get-local-ip-address
        public string GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Failed to get local IP");
        }

        public static string GetExternalIp()
        {
            for (int i = 0; i < 2; i++)
            {
                string res = GetExternalIpWithTimeout(400);
                if (res != "")
                {
                    return res;
                }
            }
            throw new Exception("Failed to get external IP");
        }
        private static string GetExternalIpWithTimeout(int timeoutMillis)
        {
            string[] sites = new string[] {
                "http://ipinfo.io/ip",
                "http://icanhazip.com/",
                "http://ipof.in/txt",
                "http://ifconfig.me/ip",
                "http://ipecho.net/plain"
            };
            foreach (string site in sites)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(site);
                    request.Timeout = timeoutMillis;
                    using (var webResponse = (HttpWebResponse)request.GetResponse())
                    {
                        using (Stream responseStream = webResponse.GetResponseStream())
                        {
                            using(StreamReader responseReader = new StreamReader(responseStream, Encoding.UTF8)){
                                return responseReader.ReadToEnd().Trim();
                            }
                            
                        }
                    }
                    
                }
                catch
                {
                    continue;
                }
            }

            return "";

        }

    }
}
