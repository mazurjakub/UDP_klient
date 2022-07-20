using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Open.Nat;


namespace UDP_klient
{
    public class Program
    {
        public static IPEndPoint ServerEndPoint = new IPEndPoint(IPAddress.Parse("3.143.208.24"), 1700);
        public static UdpClient server = new UdpClient();
        //public static UdpClient connectedClient = new UdpClient();
        public static TcpClient connectedClient;


        public static Socket socket = new Socket(AddressFamily.Unspecified, SocketType.Dgram, ProtocolType.Udp);

        public static bool hasSecondClient = false;
        public static string secondClientIP;
        public static string secondClientPort;


        static void Main(string[] args)
        {
            server.AllowNatTraversal(true);
            server.ExclusiveAddressUse = false;
            server.Client.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
            server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Connect(ServerEndPoint);

            //Start recieving data from server
            Thread ThreadListen = new Thread(() => ReceiveDataFromEP(ServerEndPoint, server));
            ThreadListen.IsBackground = true;
            ThreadListen.Start();

            string key = null;
            string message = null;

            
        server:
            //server.Connect(ServerEndPoint);
            while (true)
            {
                Thread.Sleep(100);
                if (hasSecondClient) goto client;
                // send data
                if (key == null)
                {
                reading:
                    Console.WriteLine("Zadej klic v hodnotach od 1-999 pro pripojeni ke klientovi");
                    key = Console.ReadLine();

                    if (!IsDigitsOnly(key))
                    {
                        Console.WriteLine("Klic musi obsahovat pouze cislice");
                        goto reading;
                    }

                    if (int.Parse(key) < 1 || int.Parse(key) > 999)
                    {
                        Console.WriteLine("Zadana spatna hodnota klice");
                        goto reading;
                    }
                    SendDataToServer(key, server);
                    Console.WriteLine("\n");
                }
                else
                {
                    Console.WriteLine("Pro vymazani ze serveru zadejte '0'\n");
                    key = Console.ReadLine();
                    SendDataToServer(key, server);
                    Console.WriteLine("\n");
                }

            }

        client:
            
            Thread.Sleep(100);
            /*IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Parse(secondClientIP), 53);

            connectedClient.AllowNatTraversal(true);
            connectedClient.Client.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
            connectedClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            connectedClient.Connect(clientEndPoint);*/

            hasSecondClient = false;
            /*Thread thread = new Thread(() => ReceiveDataFromEP(clientEndPoint, connectedClient));
            thread.Start();*/
            

            _ = OpenPort();

            Thread.Sleep(100);

            connectedClient = new TcpClient(secondClientIP, 1602);

            Thread thread = new Thread(() => ReceiveDataFromClient());
            thread.Start();

            while (true)
            {
                try
                {

                    //message = Console.ReadLine();
                    StreamReader reader = new StreamReader(connectedClient.GetStream());
                    SendDataToClient("Hello");
                    Thread.Sleep(100);

                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }



            }
        }

        //Sends string to connected server (ServerEndPoint)
        public static void SendDataToServer(string dataToSend, UdpClient receiver)
        {
            try
            {
                int byteCount = Encoding.ASCII.GetByteCount(dataToSend);
                byte[] sendData = Encoding.ASCII.GetBytes(dataToSend);
                receiver.Send(sendData, byteCount);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            
        }


        public static void SendDataToClient(string dataToSend)
        {
            try
            {
                NetworkStream stream = connectedClient.GetStream();
                StreamWriter writer = new StreamWriter(connectedClient.GetStream());
                int byteCount = Encoding.ASCII.GetByteCount(dataToSend);
                byte[] sendData = Encoding.ASCII.GetBytes(dataToSend);
                writer.WriteLine(dataToSend);
                writer.Flush();

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        public static void ReceiveDataFromClient()
        {
            NetworkStream stream = connectedClient.GetStream();
            try
            {
                byte[] buffer = new byte[1024];
                stream.Read(buffer, 0, buffer.Length);
                int recv = 0;
                foreach (byte b in buffer)
                {
                    if (b != 0)
                    {
                        recv++;
                    }
                }
                string request = Encoding.UTF8.GetString(buffer, 0, recv);
                Console.WriteLine("request received: " + request);
            }
            catch (Exception e)
            {
                Console.WriteLine("Something went wrong.");
            }

        }

        public static void ReceiveDataFromEP(IPEndPoint endPoint, UdpClient sender)
        {
            while (!hasSecondClient)
            {
                byte[] receivedData;

                Console.WriteLine("Posloucham na: " + endPoint.Address.ToString() + " Port: " + endPoint.Port.ToString());
                receivedData = sender.Receive(ref endPoint);

                string request = Encoding.UTF8.GetString(receivedData);


                Console.WriteLine("Prichozi zprava z IP: " + endPoint.Address.ToString() + " Port: " + endPoint.Port.ToString());
                Console.WriteLine("Obsah zpravy: " + request);
                Console.WriteLine("\n");

                // If server sends IP address, initiate connection to that IP
                if (IsIPAddress(request) && !hasSecondClient)
                {
                    secondClientIP = request;
                    hasSecondClient = true;
                    receivedData = null;

                    receivedData = server.Receive(ref endPoint);
                    request = Encoding.UTF8.GetString(receivedData);

                    Console.WriteLine("Prichozi zprava z IP: " + endPoint.Address.ToString() + " Port: " + endPoint.Port.ToString());
                    Console.WriteLine("Obsah zpravy: " + request);

                    secondClientPort = request;
                }
            }
        }

        public static async Task OpenPort()
        {
            var discoverer = new NatDiscoverer();

            // using SSDP protocol, it discovers NAT device.
            var device = await discoverer.DiscoverDeviceAsync();

            // display the NAT's IP address
            Console.WriteLine("The external IP Address is: {0} ", await device.GetExternalIPAsync());

            // create a new mapping in the router [external_ip:1702 -> host_machine:1602]
            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1602, 1702, "P2P"));

            
            
        }





        // From https://stackoverflow.com/questions/7461080/fastest-way-to-check-if-string-contains-only-digits-in-c-sharp
        public static bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }

        //From https://morgantechspace.com/2016/01/check-string-is-ip-address-in-c-sharp.html
        public static bool IsIPAddress(string ipAddress)
        {
            bool retVal = false;

            try
            {
                IPAddress address;
                retVal = IPAddress.TryParse(ipAddress, out address);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return retVal;
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
