using ParentElement.RichText.Core.Abstractions.Controllers;

namespace ParentElement.RichText.Core.Abstractions.IO
{
    public interface IExportContent
    {
        /// <summary>Exports the document held by <paramref name="controller"/> and writes the result to <paramref name="outputStream"/>.</summary>
        public Task ExportAsync(IDocumentController controller, Stream outputStream);

        /// <summary>Exports the document held by <paramref name="controller"/> and writes the result to the file at <paramref name="filePath"/>.</summary>
        public Task ExportAsync(IDocumentController controller, string filePath);
    }
}
