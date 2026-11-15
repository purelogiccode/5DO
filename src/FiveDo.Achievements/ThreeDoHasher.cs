// Ported from rcheevos (MIT) - src/rhash/hash_disc.c, rc_hash_3do.
// The hash is computed from 2048-byte user-data sectors, matching the Opera
// core's RETRO_MEMORY_SYSTEM_RAM plus rc_hash behavior used by RetroArch.
using System;
using System.IO;
using System.Security.Cryptography;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Achievements;

/// <summary>Computes the RetroAchievements 3DO disc hash from a disc source.</summary>
public static class ThreeDoHasher
{
    private static readonly byte[] OperaFsIdentifier = [0x01, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x01];

    /// <summary>Computes the 32-character lowercase MD5 hash RetroAchievements uses for 3DO discs.</summary>
    /// <param name="disc">A disc source exposing 2048-byte user-data sectors.</param>
    /// <param name="hash">The lowercase hash on success; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the disc is a recognizable 3DO disc.</returns>
    public static bool TryHash(IDiscSource disc, out string hash)
    {
        ArgumentNullException.ThrowIfNull(disc);
        hash = string.Empty;

        try
        {
            byte[] buffer = new byte[disc.SectorSize];
            disc.ReadSector(0, buffer);

            if (!buffer.AsSpan(0, OperaFsIdentifier.Length).SequenceEqual(OperaFsIdentifier))
            {
                return false;
            }

            using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            md5.AppendData(buffer, 0, 132);

            int blockSize = (buffer[0x4D] << 16) | (buffer[0x4E] << 8) | buffer[0x4F];
            int blockLocation = (buffer[0x65] << 16) | (buffer[0x66] << 8) | buffer[0x67];
            blockLocation *= blockSize;

            int sector = blockLocation / disc.SectorSize;
            int size = 0;

            do
            {
                disc.ReadSector(sector, buffer);

                int offset = (buffer[0x12] << 8) | buffer[0x13];
                int stop = (buffer[0x0D] << 16) | (buffer[0x0E] << 8) | buffer[0x0F];

                while (offset < stop)
                {
                    if (offset + 0x48 > buffer.Length)
                    {
                        return false;
                    }

                    if (buffer[offset + 0x03] == 0x02
                        && IsLaunchMe(buffer.AsSpan(offset + 0x20, Math.Min(32, buffer.Length - offset - 0x20))))
                    {
                        blockSize = (buffer[offset + 0x0D] << 16)
                            | (buffer[offset + 0x0E] << 8)
                            | buffer[offset + 0x0F];
                        blockLocation = (buffer[offset + 0x45] << 16)
                            | (buffer[offset + 0x46] << 8)
                            | buffer[offset + 0x47];
                        blockLocation *= blockSize;

                        size = (buffer[offset + 0x11] << 16)
                            | (buffer[offset + 0x12] << 8)
                            | buffer[offset + 0x13];
                        break;
                    }

                    offset += 0x48 + (buffer[offset + 0x43] * 4);
                }

                if (size != 0)
                {
                    break;
                }

                offset = (buffer[0x02] << 8) | buffer[0x03];
                if (offset == 0xFFFF)
                {
                    break;
                }

                offset *= blockSize;
                sector = (blockLocation + offset) / disc.SectorSize;
            }
            while (true);

            if (size == 0)
            {
                return false;
            }

            sector = blockLocation / disc.SectorSize;
            while (size > buffer.Length)
            {
                disc.ReadSector(sector, buffer);
                md5.AppendData(buffer);
                ++sector;
                size -= buffer.Length;
            }

            disc.ReadSector(sector, buffer);
            md5.AppendData(buffer, 0, size);

            hash = Convert.ToHexStringLower(md5.GetHashAndReset());
            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or DiscImageException)
        {
            hash = string.Empty;
            return false;
        }
    }

    private static bool IsLaunchMe(ReadOnlySpan<byte> name)
    {
        int length = name.IndexOf((byte)0);
        if (length < 0)
        {
            length = name.Length;
        }

        ReadOnlySpan<byte> launchMe = "LaunchMe"u8;
        if (length != launchMe.Length)
        {
            return false;
        }

        for (int index = 0; index < length; index++)
        {
            byte value = name[index];
            if (value is >= (byte)'A' and <= (byte)'Z')
            {
                value = (byte)(value + 32);
            }

            byte expected = launchMe[index];
            if (expected is >= (byte)'A' and <= (byte)'Z')
            {
                expected = (byte)(expected + 32);
            }

            if (value != expected)
            {
                return false;
            }
        }

        return true;
    }
}


