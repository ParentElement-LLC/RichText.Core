using ParentElement.RichText.Core.Abstractions.Controllers;

namespace ParentElement.RichText.Core.Abstractions.IO
{
    /// <summary>Contract for importing content from an external format (e.g. RTF, HTML) into a document.</summary>
    public interface IImportContent
    {
        /// <summary>Reads content from <paramref name="inputStream"/> and loads it into the document held by <paramref name="controller"/>.</summary>
        public Task ImportAsync(IDocumentController controller, Stream inputStream);

        /// <summary>Reads content from the file at <paramref name="filePath"/> and loads it into the document held by <paramref name="controller"/>.</summary>
        public Task ImportAsync(IDocumentController controller, string filePath);
    }
}
