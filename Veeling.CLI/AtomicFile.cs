namespace Veeling.CLI;

public static class AtomicFile
{
    /// <summary>
    /// Writes content atomically to the destination file.
    ///
    /// Contract:
    /// - Success: destination contains the new full payload.
    /// - Failure: destination is never reported as successfully saved and a best-effort temp-file cleanup is attempted.
    /// - Requires: destination directory already exists.
    /// </summary>
    public static void WriteAllText(FileInfo file, string content)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);

        DirectoryInfo? directory = file.Directory;
        if (directory is null || !directory.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The target directory '{directory?.FullName ?? "<unknown>"}' does not exist."
            );
        }

        string tempFilePath = Path.Combine(
            directory.FullName,
            $".{file.Name}.{Guid.NewGuid():N}.tmp"
        );

        try
        {
            File.WriteAllText(tempFilePath, content);

            if (File.Exists(file.FullName))
            {
                File.Replace(tempFilePath, file.FullName, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFilePath, file.FullName);
            }
        }
        catch
        {
            TryDelete(tempFilePath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }
}
