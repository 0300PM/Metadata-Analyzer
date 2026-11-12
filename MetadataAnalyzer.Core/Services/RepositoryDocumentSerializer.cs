using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MetadataAnalyzer.Core.Services;

public sealed class RepositoryDocumentException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class RepositoryDocumentSerializer
{
    private static readonly XmlSerializer Serializer = new(typeof(RepositoryDocument));

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Indent = true,
        IndentChars = "  ",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        NewLineHandling = NewLineHandling.Entitize,
        CloseOutput = false,
        Async = true,
    };

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        CloseInput = false,
        Async = true,
    };

    public static async Task WriteAsync(Stream stream, RepositoryDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            await using var writer = XmlWriter.Create(stream, WriterSettings);
            Serializer.Serialize(writer, document);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            throw new RepositoryDocumentException("The metadata analysis could not be serialized.", exception);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public static async Task<RepositoryDocument> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = XmlReader.Create(stream, ReaderSettings);
            var document = await Task.Run(() => Serializer.Deserialize(reader) as RepositoryDocument, cancellationToken)
                .ConfigureAwait(false);

            return document ?? throw new RepositoryDocumentException("The document is empty.");
        }
        catch (InvalidOperationException exception)
        {
            throw new RepositoryDocumentException("The document is not a valid metadata analysis file.", exception);
        }
        catch (XmlException exception)
        {
            throw new RepositoryDocumentException("The document is not well-formed XML.", exception);
        }
    }
}
