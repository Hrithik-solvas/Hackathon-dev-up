namespace CodeCompass.Core.Exceptions;

/// <summary>
/// Exception thrown when file validation fails during document parsing.
/// Carries details about the validation failure (unsupported extension, oversized file, or missing file).
/// </summary>
public class FileValidationException : Exception
{
    /// <summary>
    /// The file path that failed validation.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The specific reason the validation failed.
    /// </summary>
    public FileValidationErrorKind ErrorKind { get; }

    public FileValidationException(string message, string filePath, FileValidationErrorKind errorKind)
        : base(message)
    {
        FilePath = filePath;
        ErrorKind = errorKind;
    }

    public FileValidationException(string message, string filePath, FileValidationErrorKind errorKind, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
        ErrorKind = errorKind;
    }
}

/// <summary>
/// Categorizes the type of file validation failure.
/// </summary>
public enum FileValidationErrorKind
{
    FileNotFound,
    UnsupportedExtension,
    FileTooLarge
}
