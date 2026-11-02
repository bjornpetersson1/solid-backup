namespace SolidBackup.Recording;

/// <summary>The recording could not be written; the message is for the user, in Swedish.</summary>
public sealed class RecordingFailedException(Exception inner) : IOException("Inspelningen kunde inte skrivas.", inner);
