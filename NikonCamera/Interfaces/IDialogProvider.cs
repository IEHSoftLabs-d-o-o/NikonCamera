namespace NikonCamera.Interfaces;

public interface IDialogProvider
{
    void ShowExceptionMessage(string message, string caption = "Exception");
    void ShowInformationMessage(string message, string caption = "Information");
    bool ShowQuestionMessage(string message, string caption = "Frage");
    void ShowExclamationMessage(string message, string caption = "Achtung");
    List<string>? ShowOpenMultipleFileDialog(string extension, string description);
}