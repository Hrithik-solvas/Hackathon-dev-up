namespace CodeCompass.Core.Models;

/// <summary>
/// Determines whether the pipeline processes all files or only changed files.
/// </summary>
public enum IndexingMode
{
    Full,
    Incremental
}

/// <summary>
/// Classifies the kind of code symbol extracted during parsing.
/// </summary>
public enum CodeSymbolKind
{
    Class,
    Method,
    Component,
    Hook,
    StoredProcedure,
    Parameter
}

/// <summary>
/// Indicates the severity of a pipeline processing error.
/// </summary>
public enum PipelineErrorSeverity
{
    Warning,
    Error,
    Fatal
}
