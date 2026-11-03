// What an install writes under one name: bytes made here (an ini) or a payload file on disk. A payload
// stays on disk until it is copied into the game: an OptiScaler install is some 1.8 GB of it, and held
// in memory all at once it was 1.8 GB of heap the app kept after the install had finished.

using System.Buffers;
using System.Security.Cryptography;

namespace AmdNr.Core;

public sealed class Blob
{
    private readonly byte[]? _bytes;
    private readonly string? _path;
    private string? _sha;

    private Blob(byte[] bytes)
    {
        _bytes = bytes;
        Length = bytes.LongLength;
    }

    private Blob(string path, long length, string sha)
    {
        _path = path;
        Length = length;
        _sha = sha;
    }

    public static implicit operator Blob(byte[] bytes) => new(bytes);

    /// <summary>A file read once here, to hash it. What is written later is checked against that hash
    /// on the way, so a file changed in between fails the write instead of going in.</summary>
    public static Blob OfFile(string path)
    {
        try { return new Blob(path, new FileInfo(path).Length, Engine.HashFile(path)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InstallException($"Cannot read {path}: {e.Message}");
        }
    }

    /// <summary>A file whose hash is already known, because it was just checked against it.</summary>
    public static Blob OfFile(string path, string sha) => new(path, new FileInfo(path).Length, sha);

    public long Length { get; }

    public string Sha => _sha ??= Engine.Sha(_bytes);

    /// <summary>The bytes, read from disk when they are a file's. For what an install changes before
    /// it writes (an ini), never for a payload as large as the weights.</summary>
    public byte[] ToArray() => _bytes ?? Engine.Read(_path!);

    public void WriteTo(string path)
    {
        if (_bytes is not null) Engine.Write(path, _bytes);
        else Engine.CopyVerified(_path!, path, Sha, Path.GetFileName(path));
    }
}

public static partial class Engine
{
    /// <summary>Whether <paramref name="found"/> says yes to any piece of the file, read 1 MB at a time. Each piece
    /// starts with the last <paramref name="overlap"/> bytes of the one before, so a name across the cut is still seen.</summary>
    public static bool ScanFile(string path, Func<ReadOnlySpan<byte>, bool> found, int overlap)
    {
        using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
        var buffer = ArrayPool<byte>.Shared.Rent((1 << 20) + overlap);
        try
        {
            var kept = 0;
            int read;
            while ((read = f.Read(buffer, kept, buffer.Length - kept)) > 0)
            {
                var length = kept + read;
                if (found(buffer.AsSpan(0, length))) return true;
                kept = Math.Min(overlap, length);
                buffer.AsSpan(length - kept, kept).CopyTo(buffer);
            }
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Copies a file in pieces, hashing on the way, and fails when the copy does not hash to
    /// <paramref name="expected"/>: the streamed form of Write(to, Read(from)) followed by a hash check.</summary>
    public static void CopyVerified(string from, string to, string expected, string what)
    {
        FileStream source;
        try { source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan); }
        catch (Exception e) { throw new InstallException($"Cannot read {from}: {e.Message}"); }

        string got;
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 20);
        try
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (source)
            {
                try
                {
                    Writable(to);
                    using var sink = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None, 1);
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha.AppendData(buffer, 0, read);
                        sink.Write(buffer, 0, read);
                    }
                }
                catch (IOException) when (IsLocked(to))
                {
                    throw new InstallException(OpenElsewhere(Path.GetDirectoryName(to)!, [Path.GetFileName(to)]));
                }
                catch (Exception e) when (e is not InstallException)
                {
                    throw new InstallException($"Cannot write {to}: {e.Message}");
                }
            }
            got = Convert.ToHexStringLower(sha.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        Require(got == expected, $"{what} SHA256 mismatch");
    }
}
