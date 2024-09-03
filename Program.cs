// Program.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

namespace ABXExchangeClient
{
    class Program
    {
        // Server configuration
        private const string ServerAddress = "localhost"; // localhost or ip
        private const int ServerPort = 3000; //port number
        private const int PacketSize = 17; // Each packet is 17 bytes

        static async Task Main(string[] args)
        {
            Console.WriteLine("...xxx... Starting ABX Exchange Client ...xxx...");
            try
            {
                // Initialize the socket
                using (Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    await ConnectAsync(clientSocket, ServerAddress, ServerPort);  // Connecting to the server asynchronously
                    Console.WriteLine($"Connected to server at {ServerAddress}:{ServerPort}");

                    // sending request to stream all packets
                    byte[] streamAllRequest = new byte[] { 1, 0 }; // 1 represents the type of request means stream all packets type and 0 represents the placeholder
                    await SendAsync(clientSocket, streamAllRequest);
                    Console.WriteLine("Sent stream all packets request");

                    // receiving all packets
                    List<Packet> receivedPackets = await ReceiveAllPacketsAsync(clientSocket); //method to receive all packets from the server asynchronously and stores them in a list.
                    Console.WriteLine($"Received {receivedPackets.Count} packets from server");

                    // identify missing sequences
                    List<int> missingSequences = GetMissingSequences(receivedPackets); // method to identify missing sequence numbers in the received packets.
                    Console.WriteLine($"Identified {missingSequences.Count} missing sequences");

                    // requesting and receiving missing packets
                    foreach (int seq in missingSequences)     // iteration over each missing sequence number
                    {
                        byte[] resendRequest = new byte[] { 2, (byte)seq }; // creates a request to resend the specific packet for the missing sequence.
                        await SendAsync(clientSocket, resendRequest);    // method asynchronously sends the request to the server
                        Console.WriteLine($"Requested resend for sequence: {seq}");

                        Packet missedPacket = await ReceiveSinglePacketAsync(clientSocket); // method asynchronously receives the missing packet
                        if (missedPacket != null)      // check if the packet received successfully
                        {
                            receivedPackets.Add(missedPacket);   // add the received packet to the list
                            Console.WriteLine($"Received missed packet with sequence - {missedPacket.PacketSequence}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to receive packet for sequence - {seq}");
                        }
                    }

                    // Process received packets (save to JSON)
                    string json = JsonConvert.SerializeObject(receivedPackets, Formatting.Indented);   // converting the list of packets into a json string
                    File.WriteAllText("received_packets.json", json);   // this method will write the json string to a file named received_packets.json
                    Console.WriteLine("Saved received packets to 'received_packets.json'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
            }
            Console.WriteLine("Client Over");
        }

        private static async Task ConnectAsync(Socket socket, string serverAddress, int serverPort)
        {
            await Task.Factory.FromAsync(
                socket.BeginConnect(serverAddress, serverPort, null, null),
                socket.EndConnect
            );
        }

        private static async Task SendAsync(Socket socket, byte[] data)
        {
            await Task.Factory.FromAsync(
                socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null),
                socket.EndSend
            );
        }

        private static async Task<List<Packet>> ReceiveAllPacketsAsync(Socket socket)
        {
            List<Packet> packets = new List<Packet>();  // initialize ist to store received packets
            byte[] buffer = new byte[PacketSize]; //it will create a buffer to hold the data received from the server
                                                  //the size of the buffer matches the expected packet size
            try
            {
                while (true)
                {
                    //int bytesReceived = await Task.Factory.FromAsync<int>(
                    //    socket.BeginReceive(buffer, 0, PacketSize, SocketFlags.None, null, null),
                    //    socket.EndReceive
                    //);
                    int bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None);   // will contain the number of bytes read
                    if (bytesRead == 0)
                    {
                        break; // Connection closed
                    }
                    if (bytesRead == PacketSize) //checks if the number of bytes read matches the expected packet size
                    {
                        Packet packet = Packet.FromBytes(buffer); //converting buffer to packet object
                        packets.Add(packet);
                    }
                    else
                    {
                        throw new Exception("Received packet size doesn't match the expected size");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while receiving packets - {ex.Message}");
            }
            return packets;    // returns the list of packets received from the server
        }

        private static async Task<Packet> ReceiveSinglePacketAsync(Socket socket)
        {
            byte[] buffer = new byte[PacketSize];
            try
            {
                //int bytesReceived = await Task.Factory.FromAsync<int>(
                //socket.BeginReceive(buffer, 0, PacketSize, SocketFlags.None, null, null),
                //socket.EndReceive
                //);
                int bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
                if (bytesRead == PacketSize)
                {
                    return Packet.FromBytes(buffer);
                }
                else
                {
                    Console.WriteLine("Received packet size doesn't match the expected size");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while receiving a packet - {ex.Message}");
                return null;
            }
        }

        private static List<int> GetMissingSequences(List<Packet> packets)
        {
            List<int> missingSequences = new List<int>();    // initializes a list to store the missing sequence numbers
            try
            {
                if (packets == null || packets.Count == 0)    // checks if the list of packets is empty
                {
                    return missingSequences;
                }
                packets.Sort((x, y) => x.PacketSequence.CompareTo(y.PacketSequence));    //sorts the list of packets by their sequence numbers in ascending order
                int expectedSequence = packets[0].PacketSequence;    // sets the expectedSequence to the sequence number of the first packet in the sorted list
                foreach (Packet packet in packets)
                {
                    while (expectedSequence < packet.PacketSequence)   // If the expectedSequence is less than the current packet sequence number it means that some packets are missing
                    {
                        missingSequences.Add(expectedSequence);
                        expectedSequence++;
                    }

                    expectedSequence++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while determining missing sequences - {ex.Message}");
            }
            return missingSequences;

            //for (int i = 0; i < packets.Count - 1; i++)
            //{
            //    int currentSeq = packets[i].PacketSequence;
            //    int nextSeq = packets[i + 1].PacketSequence;

            //    for (int missingSeq = currentSeq + 1; missingSeq < nextSeq; missingSeq++)
            //    {
            //        missingSequences.Add(missingSeq);
            //    }
            //}
        }
    }
}
