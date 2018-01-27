using System;
using System.IO;
using AlleyCat.Common;
using EnsureThat;
using JetBrains.Annotations;
using File = Godot.File;

namespace AlleyCat.IO
{
    public class FileStream : Stream
    {
        public override bool CanSeek => _file.IsOpen();

        public override bool CanRead =>
            _file.IsOpen() && (_access & FileAccess.Read) != 0 && !_file.EofReached();

        public override bool CanWrite => _file.IsOpen() && (_access & FileAccess.Write) != 0;

        public override long Length => _file.GetLen();

        public override long Position
        {
            get => _file.GetPosition();
            set => _file.Seek((int) value);
        }

        private readonly File _file;

        private readonly FileAccess _access;

        private bool _closed;

        public FileStream([NotNull] File file, FileAccess access)
        {
            Ensure.Any.IsNotNull(file, nameof(file));

            Ensure.Bool.IsTrue(
                file.IsOpen(),
                nameof(file),
                opt => opt.WithMessage("File is closed."));

            file.GetError().ThrowIfNecessary(msg => new IOException(msg));

            _file = file;
            _access = access;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckClosed();

            switch (origin)
            {
                case SeekOrigin.Begin:
                    _file.Seek((int) offset);
                    break;
                case SeekOrigin.Current:
                    _file.Seek((int) (Position + offset));
                    break;
                case SeekOrigin.End:
                    _file.SeekEnd((int) offset);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }

            CheckErrors();

            return Position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckClosed();

            var remaining = (int) (Length - Position);

            var size = Math.Min(Math.Min(buffer.Length - offset, count), remaining);
            var data = _file.GetBuffer(size);

            CheckErrors();

            Array.Copy(data, 0, buffer, offset, data.Length);

            return data.Length;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckClosed();

            var size = Math.Min(buffer.Length - offset, count);

            if (offset == 0 && buffer.Length <= count)
            {
                _file.StoreBuffer(buffer);
            }
            else
            {
                var data = new byte[size];

                Array.Copy(buffer, offset, data, 0, size);

                _file.StoreBuffer(data);
            }

            CheckErrors();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (_file.IsOpen())
            {
                _file.Close();
            }

            _file.Dispose();

            _closed = true;

            base.Dispose(disposing);
        }

        private void CheckClosed()
        {
            if (_closed)
            {
                throw new ObjectDisposedException("The file was already closed.");
            }
        }

        private void CheckErrors() => _file.GetError().ThrowIfNecessary(msg => new IOException(msg));

        [NotNull]
        public static FileStream Open([NotNull] string path, FileAccess access = FileAccess.Read)
        {
            Ensure.String.IsNotNullOrWhiteSpace(path, nameof(path));

            var file = new File();

            file.Open(path, (int) ToModeFlags(access));
            file.GetError().ThrowIfNecessary(msg => new IOException(msg));

            return new FileStream(file, access);
        }

        [NotNull]
        public static FileStream OpenCompressed(
            [NotNull] string path,
            FileAccess access = FileAccess.Read,
            File.CompressionMode mode = File.CompressionMode.Fastlz)
        {
            Ensure.String.IsNotNullOrWhiteSpace(path, nameof(path));

            var file = new File();

            file.OpenCompressed(path, (int) ToModeFlags(access), (int) mode);
            file.GetError().ThrowIfNecessary(msg => new IOException(msg));

            return new FileStream(file, access);
        }

        [NotNull]
        public static FileStream OpenEncrypted([NotNull] string path, FileAccess access, byte[] key)
        {
            Ensure.String.IsNotNullOrWhiteSpace(path, nameof(path));
            Ensure.Enumerable.HasItems(key, nameof(key));

            var file = new File();

            file.OpenEncrypted(path, (int) ToModeFlags(access), key);
            file.GetError().ThrowIfNecessary(msg => new IOException(msg));

            return new FileStream(file, access);
        }

        [NotNull]
        public static FileStream OpenEncrypted([NotNull] string path, FileAccess access, string password)
        {
            Ensure.String.IsNotNullOrWhiteSpace(path, nameof(path));
            Ensure.String.IsNotNullOrWhiteSpace(password, nameof(password));

            var file = new File();

            file.OpenEncryptedWithPass(path, (int) ToModeFlags(access), password);
            file.GetError().ThrowIfNecessary(msg => new IOException(msg));

            return new FileStream(file, access);
        }

        private static File.ModeFlags ToModeFlags(FileAccess access)
        {
            switch (access)
            {
                case FileAccess.Read:
                    return File.ModeFlags.Read;
                case FileAccess.Write:
                    return File.ModeFlags.Write;
                case FileAccess.ReadWrite:
                    return File.ModeFlags.ReadWrite;
                default:
                    throw new ArgumentOutOfRangeException(nameof(access), access, null);
            }
        }
    }
}
