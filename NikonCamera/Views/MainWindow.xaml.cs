using CommunityToolkit.Mvvm.Messaging;
using NikonCamera.Dialogs;
using NikonCamera.Messages;
using NikonCamera.Services;
using NikonCamera.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NikonCamera.Views;

public partial class MainWindow
{
    private const int WmGetIcon = 0x007F;
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int GclHIcon = -14;
    private const int GclHIconSm = -34;
    private const uint IconVersion = 0x00030000;

    private readonly ViewModel _vm;
    private readonly AppSettingsStore _settingsStore = new();
    private bool _isRestarting;
    private readonly StringBuilder _scanBuffer = new();
    private DateTime _lastScanKeyUtc;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new ViewModel(new DialogProvider());

        DataContext = _vm;

        WeakReferenceMessenger.Default.Register<RestartMessage>(this, (_, m) =>
        {
            Restart(m);
        });

        LoadSettings();
    }

    private void Restart(RestartMessage restartMessage)
    {
        if (_isRestarting)
            return;

        _isRestarting = true;

        _vm.DisableEvents();
        _vm.OnClosing();

        var executablePath = Environment.ProcessPath;
        if (executablePath != null)
            Process.Start(executablePath);

        Application.Current.Shutdown();
    }

    private void MainWindow_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (SearchTextBox.IsKeyboardFocused || string.IsNullOrEmpty(e.Text))
            return;

        var now = DateTime.UtcNow;
        var gapMs = (now - _lastScanKeyUtc).TotalMilliseconds;
        _lastScanKeyUtc = now;

        if (gapMs > 80)
            _scanBuffer.Clear();

        var isScannerBurst = _scanBuffer.Length > 0 && gapMs <= 80;
        _scanBuffer.Append(e.Text);

        if (!isScannerBurst)
            return;

        e.Handled = true;
        RemoveLeakedScanCharacter(_scanBuffer[0]);
        SearchTextBox.Focus();
        SearchTextBox.Text = _scanBuffer.ToString();
        SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
        _scanBuffer.Clear();
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (SearchTextBox.IsKeyboardFocused || e.Key is not (Key.Return or Key.Enter))
            return;

        var gapMs = (DateTime.UtcNow - _lastScanKeyUtc).TotalMilliseconds;
        if (_scanBuffer.Length < 2 || gapMs > 80)
        {
            _scanBuffer.Clear();
            return;
        }

        e.Handled = true;
        SearchTextBox.Focus();
        SearchTextBox.Text = _scanBuffer.ToString();
        SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
        _scanBuffer.Clear();
        _vm.LoadSamplesCommand.Execute(null);
    }

    private void RemoveLeakedScanCharacter(char character)
    {
        if (Keyboard.FocusedElement is not TextBox textBox || textBox == SearchTextBox)
            return;

        if (textBox.Text.Length == 0 || textBox.Text[^1] != character)
            return;

        textBox.Text = textBox.Text[..^1];
        textBox.CaretIndex = textBox.Text.Length;
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        Left = settings.WindowLeft;
        Top = settings.WindowTop;
        WindowState = settings.WindowState;
    }

    private void SaveSettings()
    {
        var settings = _settingsStore.Load();
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        settings.WindowState = WindowState;
        _settingsStore.Save(settings);
    }

    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        _vm.DisableEvents();
        _vm.OnClosing();

        SaveSettings();
    }
}