using AcManager.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AcManager.App.Views;

public partial class ContentView : UserControl
{
    public ContentView()
    {
        InitializeComponent();
    }

    private async void Browse(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Car or track zip",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Zip archive") { Patterns = ["*.zip"] }],
        });
        if (files.Count == 0) {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (path != null && DataContext is ContentViewModel viewModel) {
            viewModel.ArchivePath = path;
        }
    }
}
