using CodeCompass.Core.Configuration;
using CodeCompass.Core.Exceptions;
using Microsoft.Extensions.Options;

namespace CodeCompass.Parsing;

/// <summary>
/// Shared file validation logic used by all document parsers.
/// Validates file existence, extension support, and file size constraints.
/// </summary>
public class FileValidator
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".pdf",
        ".docx"
    };

    private readonly long _maxFileSizeBytes;

    public FileValidator(IOptions<IngestionSettings> settings)
    {
        _maxFileSizeBytes = (long)settings.Value.MaxFileSizeMB * 1024 * 1024;
    }

    /// <summary>
    /// Validates that the file exists, has a supported extension, and is within the size limit.
    /// Throws <see cref="FileValidationException"/> if any check fails.
    /// </summary>
    /// <param name="filePath">The path to the file to validate.</param>
    /// <exception cref="FileValidationException">Thrown when validation fails.</exception>
    public void Validate(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileValidationException(
                $"File not found: '{filePath}'.",
                filePath,
                FileValidationErrorKind.FileNotFound);
        }

        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new FileValidationException(
                $"Unsupported file extension '{extension}'. Supported formats are: {string.Join(", ", SupportedExtensions.Order())}.",
                filePath,
                FileValidationErrorKind.UnsupportedExtension);
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _maxFileSizeBytes)
        {
            var maxMb = _maxFileSizeBytes / (1024 * 1024);
            throw new FileValidationException(
                $"File exceeds the maximum supported size of {maxMb} MB.",
                filePath,
                FileValidationErrorKind.FileTooLarge);
        }
    }
}
