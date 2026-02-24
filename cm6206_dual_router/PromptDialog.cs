using System.Windows.Forms;

namespace Cm6206DualRouter;

public static class PromptDialog
{
    public static string? Show(string title, string message, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Width = 480,
            Height = 160
        };

        var label = new Label
        {
            Text = message,
            AutoSize = false,
            Left = 12,
            Top = 12,
            Width = 440,
            Height = 36
        };

        var textBox = new TextBox
        {
            Left = 12,
            Top = 56,
            Width = 440,
            Text = defaultValue
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 292,
            Top = 88,
            Width = 76
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 376,
            Top = 88,
            Width = 76
        };

        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);

        form.AcceptButton = ok;
        form.CancelButton = cancel;

        var result = form.ShowDialog();
        if (result != DialogResult.OK) return null;

        var value = textBox.Text.Trim();
        return value.Length == 0 ? null : value;
    }
}
