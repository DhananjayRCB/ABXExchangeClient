// Packet.cs
using System;
using System.Text;

namespace ABXExchangeClient
{
    class Packet
    {
        public string Symbol { get; set; }
        public char BuySellIndicator { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int PacketSequence { get; set; }

        //static method to create a packet instance from a byte array.
        public static Packet FromBytes(byte[] data)
        {
            //checks the data is the correct size
            if (data.Length != 17) 
                throw new ArgumentException("Invalid packet size");

            Packet packet = new Packet();

            packet.Symbol = Encoding.ASCII.GetString(data, 0, 4); // Converts the first 4 bytes to a string symbol

            packet.BuySellIndicator = (char)data[4]; // Converts the 5th byte to a character - buy/sell indicator


            // Parsing the Quantity , Price and Sequence

            packet.Quantity = BitConverter.ToInt32(data, 5); // Converts bytes 6-9 to an integer -  quantity.

            // Checks if the system uses little - endian byte order.
            if (BitConverter.IsLittleEndian)
            {
                packet.Quantity = ReverseBytes(packet.Quantity); // Reverses byte order if needed -  big-endian expected.
            }

            // Parse price (next 4 bytes, big endian)
            packet.Price = BitConverter.ToInt32(data, 9); // converts bytes 10-13 to an integer - price.
            if (BitConverter.IsLittleEndian)
            {
                packet.Price = ReverseBytes(packet.Price);
            }

            // Parse packet sequence (last 4 bytes, big endian)
            packet.PacketSequence = BitConverter.ToInt32(data, 13); //converts bytes 14-17 to an integer  -  packet sequence.
            if (BitConverter.IsLittleEndian)
            {
                packet.PacketSequence = ReverseBytes(packet.PacketSequence);
            }

            return packet;  // returns the populated packet object.
        }

        // Reverse the byte order
        private static int ReverseBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}
