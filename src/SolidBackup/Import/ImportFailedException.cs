namespace SolidBackup.Import;

/// <summary>A clip could not be imported; the message is Swedish, for the console.</summary>
public sealed class ImportFailedException(string message, Exception? inner = null) : Exception(message, inner);
