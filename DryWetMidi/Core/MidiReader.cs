﻿using Melanchall.DryWetMidi.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Melanchall.DryWetMidi.Core
{
    /// <summary>
    /// Reader of the MIDI data types.
    /// </summary>
    public sealed class MidiReader : IDisposable
    {
        #region Fields

        private readonly ReaderSettings _settings;

        private readonly BinaryReader _binaryReader;
        private readonly bool _isStreamWrapped;
        private readonly MemoryStream _allDataBuffer;

        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MidiReader"/> with the specified stream.
        /// </summary>
        /// <param name="stream">Stream to read MIDI file from.</param>
        /// <param name="settings">Settings according to which MIDI data should be read.</param>
        /// <exception cref="ArgumentNullException">
        /// <para>One of the following errors occured:</para>
        /// <list type="bullet">
        /// <item>
        /// <description><paramref name="stream"/> is <c>null</c>.</description>
        /// </item>
        /// <item>
        /// <description><paramref name="settings"/> is <c>null</c>.</description>
        /// </item>
        /// </list>
        /// </exception>
        public MidiReader(Stream stream, ReaderSettings settings)
        {
            ThrowIfArgument.IsNull(nameof(stream), stream);
            ThrowIfArgument.IsNull(nameof(settings), settings);

            _settings = settings;

            if (!stream.CanSeek)
            {
                stream = new StreamWrapper(stream, settings.NonSeekableStreamBufferSize);
                _isStreamWrapped = true;
            }

            Length = stream.Length;

            if (settings.ReadFromMemory && !(stream is MemoryStream))
            {
                _allDataBuffer = new MemoryStream();
                stream.CopyTo(_allDataBuffer);
                _allDataBuffer.Position = 0;
                stream = _allDataBuffer;
            }

            _binaryReader = new BinaryReader(stream, SmfConstants.DefaultTextEncoding, leaveOpen: true);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the position within the underlying stream.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="ObjectDisposedException">Property was called after the reader was disposed.</exception>
        public long Position
        {
            get { return _binaryReader.BaseStream.Position; }
            set { _binaryReader.BaseStream.Position = value; }
        }

        /// <summary>
        /// Gets length of the underlying stream.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets a value indicating whether end of the underlying stream is reached.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="ObjectDisposedException">Property was called after the reader was disposed.</exception>
        public bool EndReached => Position >= Length || (_isStreamWrapped && ((StreamWrapper)_binaryReader.BaseStream).IsEndReached());

        #endregion

        #region Methods

        /// <summary>
        /// Reads all remaining bytes from the underlying stream and moves the current position
        /// to the stream's end.
        /// </summary>
        /// <returns>All bytes read from the underlying stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the underlying stream is reached.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public byte[] ReadAllBytes()
        {
            const int bufferSize = 512;

            using (var memoryStream = new MemoryStream())
            {
                var buffer = new byte[bufferSize];

                int count;
                while ((count = _binaryReader.Read(buffer, 0, buffer.Length)) != 0)
                {
                    memoryStream.Write(buffer, 0, count);
                }

                return memoryStream.ToArray();
            }

        }

        /// <summary>
        /// Reads a byte from the underlying stream and advances the current position by one byte.
        /// </summary>
        /// <returns>The next byte read from the underlying stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the underlying stream is reached.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public byte ReadByte()
        {
            return _binaryReader.ReadByte();
        }

        /// <summary>
        /// Reads a signed byte from the underlying stream and advances the current position by one byte.
        /// </summary>
        /// <returns>A signed byte read from the underlying stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the underlying stream is reached.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public sbyte ReadSByte()
        {
            return _binaryReader.ReadSByte();
        }

        /// <summary>
        /// Reads the specified number of bytes from the underlying stream into a byte array
        /// and advances the current position by that number of bytes.
        /// </summary>
        /// <param name="count">The number of bytes to read. This value must be 0 or a
        /// non-negative number or an exception will occur.</param>
        /// <returns>A byte array containing data read from the underlying stream. This might be less
        /// than the number of bytes requested if the end of the stream is reached.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public byte[] ReadBytes(int count)
        {
            if (_isStreamWrapped && count > _settings.NonSeekableStreamIncrementalBytesReadingThreshold)
            {
                var bytesList = new List<byte[]>();

                while (count > 0)
                {
                    var bytes = _binaryReader.ReadBytes(Math.Min(count, _settings.NonSeekableStreamIncrementalBytesReadingStep));
                    if (bytes.Length == 0)
                        break;

                    count -= bytes.Length;
                    bytesList.Add(bytes);
                }

                return bytesList.SelectMany(bytes => bytes).ToArray();
            }

            return _binaryReader.ReadBytes(count);
        }

        /// <summary>
        /// Reads a WORD value (16-bit unsigned integer) from the underlying stream and
        /// advances the current position by two bytes.
        /// </summary>
        /// <returns>A 16-bit unsigned integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a WORD.</exception>
        public ushort ReadWord()
        {
            const int wordSize = sizeof(ushort);

            var bytes = ReadBytes(wordSize);
            if (bytes.Length < wordSize)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a WORD.", wordSize, bytes.Length);

            return (ushort)((bytes[0] << 8) + bytes[1]);
        }

        /// <summary>
        /// Reads a DWORD value (32-bit unsigned integer) from the underlying stream and
        /// advances the current position by four bytes.
        /// </summary>
        /// <returns>A 32-bit unsigned integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a DWORD.</exception>
        public uint ReadDword()
        {
            const int dwordSize = sizeof(uint);

            var bytes = ReadBytes(dwordSize);
            if (bytes.Length < dwordSize)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a DWORD.", dwordSize, bytes.Length);

            return (uint)((bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3]);
        }

        /// <summary>
        /// Reads an INT16 value (16-bit signed integer) from the underlying stream and
        /// advances the current position by two bytes.
        /// </summary>
        /// <returns>A 16-bit signed integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a INT16.</exception>
        public short ReadInt16()
        {
            const int int16Size = sizeof(short);

            var bytes = ReadBytes(int16Size);
            if (bytes.Length < int16Size)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a INT16.", int16Size, bytes.Length);

            return (short)((bytes[0] << 8) + bytes[1]);
        }

        /// <summary>
        /// Reads the specified number of characters from the underlying stream, returns the
        /// data as string, and advances the current position by that number of characters.
        /// </summary>
        /// <param name="count">The length of string to read.</param>
        /// <returns>The string being read.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public string ReadString(int count)
        {
            var chars = _binaryReader.ReadChars(count);
            return new string(chars);
        }

        /// <summary>
        /// Reads a 32-bit signed integer presented in compressed format called variable-length quantity (VLQ)
        /// to the underlying stream.
        /// </summary>
        /// <remarks>
        /// Numbers in VLQ format are represented 7 bits per byte, most significant bits first.
        /// All bytes except the last have bit 7 set, and the last byte has bit 7 clear. If the
        /// number is between 0 and 127, it is thus represented exactly as one byte.
        /// </remarks>
        /// <returns>A 32-bit signed integer read from the underlying stream.</returns>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a variable-length quantity
        /// number.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public int ReadVlqNumber()
        {
            return (int)ReadVlqLongNumber();
        }

        /// <summary>
        /// Reads a 64-bit signed integer presented in compressed format called variable-length quantity (VLQ)
        /// to the underlying stream.
        /// </summary>
        /// <remarks>
        /// Numbers in VLQ format are represented 7 bits per byte, most significant bits first.
        /// All bytes except the last have bit 7 set, and the last byte has bit 7 clear. If the
        /// number is between 0 and 127, it is thus represented exactly as one byte.
        /// </remarks>
        /// <returns>A 64-bit signed integer read from the underlying stream.</returns>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a variable-length quantity
        /// number.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public long ReadVlqLongNumber()
        {
            long result = 0;
            byte b;

            try
            {
                do
                {
                    b = ReadByte();
                    result = (result << 7) + (b & 127);
                }
                while (b >> 7 != 0);
            }
            catch (EndOfStreamException ex)
            {
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a variable-length quantity number.", ex);
            }

            return result;
        }

        /// <summary>
        /// Reads a DWORD value (32-bit unsigned integer) presented by 3 bytes from the underlying
        /// stream and advances the current position by three bytes.
        /// </summary>
        /// <returns>A 32-bit unsigned integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a 3-byte DWORD.</exception>
        public uint Read3ByteDword()
        {
            const int dwordSize = 3;

            var bytes = ReadBytes(dwordSize);
            if (bytes.Length < dwordSize)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a 3-byte DWORD.", dwordSize, bytes.Length);

            return (uint)((bytes[0] << 16) + (bytes[1] << 8) + bytes[2]);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="MidiReader"/> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _allDataBuffer?.Dispose();
                _binaryReader.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }
}
