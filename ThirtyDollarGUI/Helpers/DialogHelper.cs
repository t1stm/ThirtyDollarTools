using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ThirtyDollarGUI.Services;

namespace ThirtyDollarGUI.Helper;

public static class DialogHelper
{
    public static async Task<IEnumerable<string>?> OpenFileDialogAsync(this object? context, string? title = null,
        IReadOnlyList<FilePickerFileType>? file_types = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var top_level = DialogService.GetTopLevelForContext(context);

        if (top_level == null) return null;

        var storage_files = await top_level.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = title ?? "Select a file",
                FileTypeFilter = file_types
            });


        return storage_files.Select(s => s.TryGetLocalPath()!);
    }

    public static async Task<string> SaveFileDialogAsync(this object? context, string? title = null,
        IReadOnlyList<FilePickerFileType>? file_types = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var top_level = DialogService.GetTopLevelForContext(context);

        if (top_level == null) return string.Empty;

        var storage_file = await top_level.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title ?? "Select any file",
                FileTypeChoices = file_types
            });

        return storage_file?.TryGetLocalPath()!;
    }
}