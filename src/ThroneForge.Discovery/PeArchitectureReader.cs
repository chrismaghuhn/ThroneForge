using System.Buffers.Binary;

namespace ThroneForge.Discovery;

public static class PeArchitectureReader
{
    private const int DosHeaderMinimumSize = 0x40;
    private const int CoffHeaderSize = 20;
    private const int MaximumHeaderBytes = 1024 * 1024;

    public static bool TryRead(string path, out ExecutableArchitecture architecture)
    {
        architecture = ExecutableArchitecture.Unknown;

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);

            if (stream.Length < DosHeaderMinimumSize)
            {
                return false;
            }

            Span<byte> dosHeader = stackalloc byte[DosHeaderMinimumSize];
            if (!ReadExactly(stream, dosHeader) || dosHeader[0] != 'M' || dosHeader[1] != 'Z')
            {
                return false;
            }

            var peOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[0x3C..]);
            if (peOffset < DosHeaderMinimumSize || peOffset > MaximumHeaderBytes || peOffset > stream.Length - 4 - CoffHeaderSize)
            {
                return false;
            }

            stream.Position = peOffset;
            Span<byte> peAndCoffHeader = stackalloc byte[4 + CoffHeaderSize];
            if (!ReadExactly(stream, peAndCoffHeader)
                || peAndCoffHeader[0] != 'P'
                || peAndCoffHeader[1] != 'E'
                || peAndCoffHeader[2] != 0
                || peAndCoffHeader[3] != 0)
            {
                return false;
            }

            var machine = BinaryPrimitives.ReadUInt16LittleEndian(peAndCoffHeader[4..]);
            architecture = machine switch
            {
                0x014C => ExecutableArchitecture.X86,
                0x8664 => ExecutableArchitecture.X64,
                0xAA64 => ExecutableArchitecture.Arm64,
                _ => ExecutableArchitecture.Unknown
            };

            return architecture != ExecutableArchitecture.Unknown;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool ReadExactly(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
