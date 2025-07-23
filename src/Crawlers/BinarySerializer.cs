using System.Text;

namespace Crawlers;

    // A custom binary serializer used and tested by AES encryption
    public class BinarySerializer : IDisposable
    {
        private readonly MemoryStream _memoryStream;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public BinarySerializer(int initialSize)
        {
            _memoryStream = new MemoryStream(initialSize);
            _writer = new BinaryWriter(_memoryStream, Encoding.UTF8, true); // Leave open
            _reader = new BinaryReader(_memoryStream, Encoding.UTF8, true); // Leave open
        }

        // Mimics the 'align8Bytes' for encryption input
        // Writes int length + data + padding, returns the final byte array for encryption
        public byte[] WriteLengthPrefixedAndAligned(byte[] data)
        {
            if (_writer == null) throw new InvalidOperationException("BinarySerializer not initialized for writing.");

            // Write the length of the buffer and the data (4 bytes, integer)
            _writer.Write(data.Length);
            _writer.Write(data.Length);
            // Write the actual data bytes
            _writer.Write(data);

            // Calculate padding to align to 8 bytes (total length including the 4-byte length prefix + data)
            var currentLength = _memoryStream.Position; // Position is current total bytes written
            var paddingNeeded = (8 - (currentLength % 8)) % 8; // Calculate bytes needed to reach next multiple of 8

            if (paddingNeeded > 0)
            {
                // Write padding bytes (typically zeros, as per block cipher padding but GCM doesn't use it directly)
                _writer.Write(new byte[paddingNeeded]);
            }

            _writer.Flush(); // Ensure all buffered writes are to MemoryStream
            return _memoryStream.ToArray(); // Get the entire buffer content as a byte array
        }
        
        public int ReadInt()
        {
            var data = _reader.ReadInt32();
            return data;
        }

        public byte[] ReadByteArray()
        {
            var size = _reader.ReadInt32();
            var bytes = _reader.ReadBytes(size);
            return bytes;
        }

        // For decryption, we often create a new serializer after decrypting
        // and wrap the decrypted bytes in it to read the string length etc.
        public BinarySerializer(byte[] decryptedBytes)
        {
            _memoryStream = new MemoryStream(decryptedBytes);
            _reader = new BinaryReader(_memoryStream, Encoding.UTF8, true);
            _writer = new BinaryWriter(_memoryStream, Encoding.UTF8, true); // Writer not strictly needed for reading
        }

        // Kotlin's size property seems to refer to the current length of the buffer
        public int Size => (int)_memoryStream.Length;


        public void Dispose()
        {
            _reader.Dispose();
            _writer.Dispose();
            _memoryStream.Dispose();
        }
    }
    