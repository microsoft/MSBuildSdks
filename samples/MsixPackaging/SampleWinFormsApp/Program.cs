using System.Windows.Forms;

namespace SampleWinFormsApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var form = new Form
        {
            Text = "Sample WinForms App",
            Width = 480,
            Height = 240,
        };
        form.Controls.Add(new Label
        {
            Text = "Hello from SampleWinFormsApp inside an MSIX package!",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
        });

        Application.Run(form);
    }
}
