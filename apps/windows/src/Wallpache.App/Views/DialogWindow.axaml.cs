using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Wallpache.App.Views;

/// <summary>
/// The app's alert, confirmation, and rename dialogs.
///
/// Avalonia has no built-in message box, and three near-identical windows would
/// be worse than one that is configured at the call site.
/// </summary>
public partial class DialogWindow : Window
{
    private string? _result;

    public DialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>A message with a single dismiss button.</summary>
    public static Task ShowMessageAsync(Window owner, string title, string message)
    {
        var dialog = Configure(title, message);
        return dialog.ShowDialog(owner);
    }

    /// <summary>
    /// A destructive or irreversible action. Returns <see langword="true"/> only
    /// when the user chose the confirming button.
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        Window owner,
        string title,
        string message,
        string confirmText)
    {
        var dialog = Configure(title, message);
        dialog.ConfirmButton.Content = confirmText;
        dialog.CancelButton.IsVisible = true;

        return await dialog.ShowDialog<string?>(owner) is not null;
    }

    /// <summary>
    /// A single-line text prompt. Returns <see langword="null"/> when cancelled,
    /// so an empty answer and a cancelled dialog stay distinguishable.
    /// </summary>
    public static async Task<string?> PromptAsync(
        Window owner,
        string title,
        string message,
        string initialValue,
        string confirmText)
    {
        var dialog = Configure(title, message);
        dialog.ConfirmButton.Content = confirmText;
        dialog.CancelButton.IsVisible = true;
        dialog.InputBox.IsVisible = true;
        dialog.InputBox.Text = initialValue;

        dialog.Opened += (_, _) =>
        {
            dialog.InputBox.SelectAll();
            dialog.InputBox.Focus();
        };

        return await dialog.ShowDialog<string?>(owner);
    }

    private static DialogWindow Configure(string title, string message)
    {
        var dialog = new DialogWindow();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.MessageText.IsVisible = !string.IsNullOrEmpty(message);
        return dialog;
    }

    private void OnConfirm(object? sender, RoutedEventArgs args)
    {
        _result = InputBox.IsVisible ? InputBox.Text ?? string.Empty : string.Empty;
        Close(_result);
    }

    private void OnCancel(object? sender, RoutedEventArgs args) => Close(null);
}
