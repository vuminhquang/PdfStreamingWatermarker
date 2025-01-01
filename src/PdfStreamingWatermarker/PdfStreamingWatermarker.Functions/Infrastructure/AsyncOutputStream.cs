namespace PdfStreamingWatermarker.Functions.Infrastructure;

/// <summary>
/// Provides an adapter to bridge synchronous PDF writing with asynchronous stream operations.
/// Used by iText7's PdfWriter to write to Azure Function's response stream asynchronously.
/// </summary>
public class AsyncOutputStream : Stream
{
    private readonly Stream _outputStream;

    public AsyncOutputStream(Stream outputStream)
    {
        _outputStream = outputStream;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _outputStream.FlushAsync().GetAwaiter().GetResult();
    
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _outputStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => 
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => 
        throw new NotSupportedException();

    public override void SetLength(long value) => 
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        _outputStream.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _outputStream.WriteAsync(buffer, offset, count, cancellationToken);
    }
} 