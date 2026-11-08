using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Collects the optional email used to recognize a donation and returns it to the caller.
/// The caller performs the network check after the dialog closes.
/// </summary>
public partial class DonationEmailWindow : Window
{
    /// <summary>Creates the dialog.</summary>
    public DonationEmailWindow()
    {
        InitializeComponent();
        EmailBox.TextChanged += OnEmailChanged;
        Opened += (_, _) =>
        {
            EmailBox.Focus();
            EmailBox.SelectAll();
        };
    }

    /// <summary>Creates the dialog with the saved email prefilled.</summary>
    /// <param name="email">Saved donation email, or <see langword="null"/>.</param>
    public DonationEmailWindow(string? email)
        : this()
    {
        EmailBox.Text = email ?? string.Empty;
    }

    /// <summary>Gets the confirmed email, or <see langword="null"/> when the dialog was cancelled.</summary>
    public string? Email { get; private set; }

    /// <summary>Shows the dialog and returns the confirmed email.</summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="currentEmail">Saved donation email, or <see langword="null"/>.</param>
    /// <returns>The confirmed email, or <see langword="null"/> when cancelled.</returns>
    public static async Task<string?> ShowAsync(Window owner, string? currentEmail)
    {
        ArgumentNullException.ThrowIfNull(owner);
        try
        {
            var dialog = new DonationEmailWindow(currentEmail);
            await dialog.ShowDialog(owner).ConfigureAwait(true);
            return dialog.Email;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Serilog.Log.Error(exception, "The donation email dialog failed.");
            throw;
        }
    }

    private void OnCheck(object? sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text?.Trim() ?? string.Empty;
        if (!IsValidEmail(email))
        {
            ValidationText.IsVisible = true;
            EmailBox.Focus();
            return;
        }

        Email = email;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnEmailChanged(object? sender, TextChangedEventArgs e)
    {
        ValidationText.IsVisible = false;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length == 0 || email.Length > 254)
        {
            return false;
        }

        return MailAddress.TryCreate(email, out MailAddress? address)
            && string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
    }
}
