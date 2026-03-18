using System.Windows.Forms;

namespace SystemAnalysis.Interop;

public static class ClipboardHelper
{
    public static string GetText()
    {
        string? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    result = Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result ?? string.Empty;
    }
}
