using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Avalonia.Views;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.MameData;

namespace SimpleLauncher.Avalonia;

public partial class GameDetailWindow : Window
{
    private readonly GameCardViewModel _game;
    private readonly LocalizationService _localization;
    private readonly MainViewModel _mainViewModel;
    private readonly MameDataService? _mameData;

    public GameDetailWindow(GameCardViewModel game, MainViewModel mainViewModel)
    {
        InitializeComponent();

        _game = game;
        _mainViewModel = mainViewModel;
        DataContext = game;

        try
        {
            _mameData = App.ServiceProvider.GetRequiredService<MameDataService>();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MameDataService unavailable in GameDetailWindow");
            _mameData = null;
        }

        try
        {
            _localization = App.ServiceProvider.GetRequiredService<LocalizationService>();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "LocalizationService unavailable in GameDetailWindow");
            _localization = null!;
        }

        UpdateFavoriteButton();
        LoadDescription();
    }

    private void LoadDescription()
    {
        var descText = DescriptionBlock;

        if (_mameData is not null &&
            (_game.SystemName.Contains("Arcade", StringComparison.OrdinalIgnoreCase) ||
             _game.SystemName.Contains("MAME", StringComparison.OrdinalIgnoreCase)))
        {
            var machineName = Path.GetFileNameWithoutExtension(_game.FilePath);
            if (_mameData.Lookup.TryGetValue(machineName, out var mameDesc))
            {
                descText.Text = mameDesc;
                return;
            }
        }

        try
        {
            if (File.Exists(_game.FilePath))
            {
                var fi = new FileInfo(_game.FilePath);
                descText.Text = _localization is not null
                    ? _localization.GetString("GameDetail.FileInfo",
                        fi.Name, fi.Length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                        fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm",
                            System.Globalization.CultureInfo.InvariantCulture))
                    : $"File: {fi.Name}\nSize: {fi.Length:N0} bytes\nModified: {fi.LastWriteTime:yyyy-MM-dd HH:mm}";
            }
            else
            {
                descText.Text = _localization is not null
                    ? _localization.GetString("GameDetail.FileNotFound")
                    : "File not found on disk.";
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read file details for {Path}", _game.FilePath);
            descText.Text = _localization is not null
                ? _localization.GetString("GameDetail.NoInfo")
                : "No additional information available.";
        }
    }

    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        _mainViewModel.PlayGameCommand.Execute(_game);
        Close();
    }

    /// <summary>
    ///     Opens the full-size cover in the ImageViewerWindow (parity with the WPF
    ///     "Open Cover" context-menu action).
    /// </summary>
    private async void CoverImage_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_game.CoverPath) || !File.Exists(_game.CoverPath)) return;

            var viewer = App.ServiceProvider.GetRequiredService<ImageViewerWindow>();
            viewer.LoadImagePath(_game.CoverPath);
            await viewer.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open image viewer for {Path}", _game.CoverPath);
        }
    }

    private async void FavoriteButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _mainViewModel.ToggleFavoriteCommand.ExecuteAsync(_game);
            UpdateFavoriteButton();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to favorite game");
        }
    }

    private async void RemoveButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var confirmMessage = _localization is not null
                ? string.Format(
                    _localization.GetString("GameDetail.RemoveConfirm",
                        "Remove \"{0}\" from your library?\nThis will not delete the file."),
                    _game.DisplayTitle)
                : $"Remove \"{_game.DisplayTitle}\" from your library?\nThis will not delete the file.";
            var confirmTitle = _localization is not null
                ? _localization.GetString("GameDetail.RemoveGame", "Remove Game")
                : "Remove Game";

            var result = await MessageDialogWindow.ShowAsync(
                this,
                confirmMessage,
                confirmTitle,
                MessageButtons.YesNo,
                MessageIcon.Question);

            if (result == MessageBoxResult.Yes)
            {
                _mainViewModel.RemoveGameFromCurrentList(_game);
                Close();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method RemoveButton_Click");
        }
    }

    private void UpdateFavoriteButton()
    {
        FavoriteButton.Content = _game.IsFavorite ? "♥ Favorited" : "♡ Favorite";
        FavoriteButton.Classes.Set("primary", _game.IsFavorite);
        FavoriteButton.Classes.Set("secondary", !_game.IsFavorite);
    }
}