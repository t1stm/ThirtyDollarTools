using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ThirtyDollarGUI.Services;

namespace ThirtyDollarGUI.Helper;

public static class DialogHelper
{
    public static async Task<IEnumerable<Uri>?> OpenFileDialogAsync(this object? context, string? title = null)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var top_level = DialogService.GetTopLevelForContext(context);

        if (top_level == null) return null;
        
        var storage_files = await top_level.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = title ?? "Select a file"
            });

        
        return storage_files.Select(s => s.Path);
    }
    
    public static async Task<Uri?> SaveFileDialogAsync(this object? context, string? title = null)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var top_level = DialogService.GetTopLevelForContext(context);

        if (top_level == null) return null;

        var storage_file = await top_level.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title ?? "Select any file"
            });

        return storage_file?.Path;
    }
}