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
        public static IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("13.58.62.170"), 1700);
        public static IPEndPoint recieveFrom = new IPEndPoint(IPAddress.Any, 0);

        public static UdpClient client = new UdpClient();

        public static bool hasSecondClient = false;
        public static string secondClientIP;
        public static string secondClientPort;


        static void Main(string[] args)
        {
            try
            {
                client.AllowNatTraversal(true);
                client.ExclusiveAddressUse = false;
                client.Client.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Connect(serverEndPoint);
            }
            catch(Exception e)
            {
                Console.WriteLine("Unable to connect to the server. Exception: " + e.Message);
                return;
            }

            //Start recieving data from server
            Thread serverThread = new Thread(() => ReceiveDataFromEP(serverEndPoint));
            serverThread.IsBackground = true;
            serverThread.Start();

            string key = null;
            string message = null;


            
            while (!hasSecondClient)
            {
                Thread.Sleep(100);
                // send data
                if (key == null)
                {
                    Console.WriteLine("Zadej klic v hodnotach od 1-999 pro pripojeni ke klientovi");
                    key = Console.ReadLine();

                    if (!IsDigitsOnly(key) || key.Length == 0)
                    {
                        Console.WriteLine("Klic musi obsahovat pouze cislice\n");
                        key = null;
                        continue;
                    }

                    if (int.Parse(key) < 1 || int.Parse(key) > 999)
                    {
                        Console.WriteLine("Zadana spatna hodnota klice");
                        key = null;
                        continue;
                    }
                    SendDataToEP(key, serverEndPoint);
                    Console.WriteLine("\n");
                }
                else
                {
                    Console.WriteLine("Pro vymazani ze serveru zadejte '0'\n");
                    key = Console.ReadLine();
                    SendDataToEP(key, serverEndPoint);
                    Console.WriteLine("\n");
                }

            }


            Thread.Sleep(100);
            //hasSecondClient = false;

            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Parse(secondClientIP), int.Parse(secondClientPort));
            client.Client.Connect(clientEndPoint);

            serverThread.Join();

            Thread clientThread = new Thread(() => ReceiveDataFromEP(recieveFrom));
            clientThread.Start();

            while (client.Client.Connected)
            {
                try
                {
                    Console.WriteLine("Zadej zpravu k odeslani");
                    message = Console.ReadLine();

                    SendDataToEP(message, clientEndPoint);
                    Thread.Sleep(100);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    hasSecondClient = false;
                    break;
                }
            }
        }

        


        //Sends string to given endpoint
        public static void SendDataToEP(string dataToSend, IPEndPoint ep)
        {
            try
            {
                if(dataToSend.Length > 0)
                {
                    int byteCount = Encoding.ASCII.GetByteCount(dataToSend);
                    byte[] sendData = Encoding.ASCII.GetBytes(dataToSend);
                    client.Send(sendData, byteCount, ep);
                }
                else
                {
                    Console.WriteLine("Nezadal jste zadnou zpravu\n");
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            
        }


        public static void ReceiveDataFromEP(IPEndPoint endPoint)
        {
            while (true)
            {
                if (client.Client.Connected)
                {
                    byte[] receivedData;

                    Console.WriteLine("Posloucham na: " + endPoint.Address.ToString() + " Port: " + endPoint.Port.ToString());
                    receivedData = client.Receive(ref endPoint);

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

                        receivedData = client.Receive(ref endPoint);
                        request = Encoding.UTF8.GetString(receivedData);

                        Console.WriteLine("Prichozi zprava z IP: " + endPoint.Address.ToString() + " Port: " + endPoint.Port.ToString());
                        Console.WriteLine("Obsah zpravy: " + request);

                        secondClientPort = request;
                    }
                }
                else
                {
                    Console.WriteLine("The client is not connected to a remote endpoint");
                    return;
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
