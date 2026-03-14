using ParentElement.RichText.Core.Abstractions.Controllers;
using ParentElement.RichText.Core.Abstractions.IO;

namespace ParentElement.RichText.Core.IO
{
    /// <summary>Base class for document exporters. Derive from this class to implement a specific export format.</summary>
    public abstract class ExporterBase: IExportContent
    {
        /// <inheritdoc/>
        public abstract Task ExportAsync(IDocumentController controller, Stream outputStream);

        /// <inheritdoc/>
        public abstract Task ExportAsync(IDocumentController controller, string filePath);
    }
}