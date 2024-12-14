using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

public class Program
{
    public static void Main()
    {
        const string hostname = "localhost";
        const int port = 3000;
        const int packetSize = 17;

        try
        {
            // Step 1: Connect to the server
            using (TcpClient client = new TcpClient())
            {
                client.Connect(hostname, port);
                Console.WriteLine("Connected to server.");

                using (NetworkStream stream = client.GetStream())
                {
                    // Step 2: Send "Stream All Packets" request
                    byte[] payload = new byte[] { 1, 0 }; // CallType = 1
                    stream.Write(payload, 0, payload.Length);

                    // Step 3: Receive packets
                    List<Packet> packets = new List<Packet>();
                    byte[] buffer = new byte[packetSize];

                    while (true)
                    {
                        int bytesRead = stream.Read(buffer, 0, packetSize);

                        if (bytesRead == 0) // Connection closed by server
                        {
                            Console.WriteLine("Server closed the connection after sending packets.");
                            break;
                        }

                        if (bytesRead != packetSize)
                        {
                            Console.WriteLine("Received incomplete packet, skipping.");
                            continue;
                        }

                        Packet packet = ParsePacket(buffer);
                        packets.Add(packet);
                    }

                    // Step 4: Detect and request missing packets
                    var receivedSequences = packets.Select(p => p.Sequence).OrderBy(s => s).ToList();
                    var missingSequences = Enumerable.Range(receivedSequences.First(), receivedSequences.Last() - receivedSequences.First() + 1)
                                                     .Except(receivedSequences);

                    foreach (int seq in missingSequences)
                    {
                        Console.WriteLine($"Requesting missing packet: Sequence {seq}");

                        byte[] resendPayload = new byte[] { 2, (byte)seq }; // CallType = 2
                        stream.Write(resendPayload, 0, resendPayload.Length);

                        int bytesRead = stream.Read(buffer, 0, packetSize);

                        if (bytesRead == packetSize)
                        {
                            Packet missingPacket = ParsePacket(buffer);
                            packets.Add(missingPacket);
                        }
                        else
                        {
                            Console.WriteLine($"Failed to retrieve missing packet: Sequence {seq}");
                        }
                    }

                    // Step 5: Write packets to JSON
                    packets = packets.OrderBy(p => p.Sequence).ToList();
                    string jsonOutput = JsonConvert.SerializeObject(packets, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(@"C:\Users\Admin\source\repos\ABXMockExchangeClient\ABXMockExchangeClient\output.json", jsonOutput);
                    Console.WriteLine("Data successfully saved to output.json");
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Socket error: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"I/O error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    static Packet ParsePacket(byte[] buffer)
    {
        return new Packet
        {
            Symbol = Encoding.ASCII.GetString(buffer, 0, 4).Trim(),
            BuySellIndicator = (char)buffer[4],
            Quantity = BitConverter.ToInt32(buffer.Skip(5).Take(4).Reverse().ToArray(), 0), // Big-endian to Little-endian
            Price = BitConverter.ToInt32(buffer.Skip(9).Take(4).Reverse().ToArray(), 0),   // Big-endian to Little-endian
            Sequence = BitConverter.ToInt32(buffer.Skip(13).Take(4).Reverse().ToArray(), 0) // Big-endian to Little-endian
        };
    }
}

public class Packet
{
    public string Symbol { get; set; }
    public char BuySellIndicator { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int Sequence { get; set; }
}
