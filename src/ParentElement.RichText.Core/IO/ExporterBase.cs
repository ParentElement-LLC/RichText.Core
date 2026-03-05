using ParentElement.RichText.Core.Abstractions.Controllers;
using ParentElement.RichText.Core.Abstractions.IO;

namespace ParentElement.RichText.Core.IO
{
    public abstract class ExporterBase: IExportContent
    {
        public abstract Task ExportAsync(IDocumentController controller, Stream outputStream);

        public abstract Task ExportAsync(IDocumentController controller, string filePath);
    }
}