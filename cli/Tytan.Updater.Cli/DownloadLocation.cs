namespace Tytan.Updater.Cli;

internal static class DownloadLocation
{
    internal static async Task<string?> ChooseAsync(string filename, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // HTTP awaits resume on pool threads. Windows dialogs need their own STA thread.
        var thread = new Thread(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                using var dialog = new SaveFileDialog
                {
                    Title = "Save update ZIP",
                    FileName = filename,
                    Filter = "ZIP packages (*.zip)|*.zip",
                    DefaultExt = "zip",
                    AddExtension = true,
                    CheckPathExists = true,
                    OverwritePrompt = false,
                    RestoreDirectory = true
                };
                result.TrySetResult(dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null);
            }
            catch (Exception error)
            {
                result.TrySetException(error);
            }
        }) { IsBackground = true, Name = "ZIP save dialog" };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Ctrl+C can end the console process even while its background dialog is open.
        string? path = await result.Task.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        return path;
    }
}
