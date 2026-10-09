using Microsoft.Win32;
using NikonCamera.Interfaces;
using System.Windows;

namespace NikonCamera.Dialogs;

public class DialogProvider : IDialogProvider
{
    public void ShowExceptionMessage(string message, string caption = "Exception") => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    
    public void ShowInformationMessage(string message, string caption = "Information") => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    
    public bool ShowQuestionMessage(string message, string caption = "Frage") => MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    
    public void ShowExclamationMessage(string message, string caption = "Achtung") => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Exclamation);
    
    public List<string>? ShowOpenMultipleFileDialog(string extension, string description)
    {
        var openFileDialog = new OpenFileDialog()
        {
            Filter = $"{description} (*.{extension})|*.{extension}",
            FileName = $"*.{extension}",
            Multiselect = true
        };

        var dialogResult = openFileDialog.ShowDialog();

        return dialogResult == false ? null : [.. openFileDialog.FileNames];
    }
}