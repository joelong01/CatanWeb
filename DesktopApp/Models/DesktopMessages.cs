using System;

namespace Catan3.Models
{
    /// <summary>
    /// Desktop-specific MVVM messages that handle file system operations.
    /// These messages contain file paths and are only used by the Desktop app.
    /// </summary>

    /// <summary>
    /// Desktop-specific message for loading games from local file paths.
    /// Contains file path for .catan or .catan_test files.
    /// </summary>
    public class LoadLocalCatanGameMessage(string localFile)
    {
        public string LocalFile { get; } = localFile;
        public override string ToString() => $"LoadLocalCatanGameMessage: {LocalFile}";
    }
}