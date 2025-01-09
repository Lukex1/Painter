using System.Collections.ObjectModel;
using System.IO;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using AndroidX.DocumentFile.Provider;

namespace Painter;

public class PdfFileInfo
{
    public string? Name { get; set; }
    public string CreationDate { get; set; }
    public string FullPath { get; set; }
}

public partial class DisplayProj : ContentPage
{
    public ObservableCollection<PdfFileInfo> PdfFiles { get; set; }
    private PdfColoringPage page;

    public DisplayProj()
    {
        InitializeComponent();
        PdfFiles = new ObservableCollection<PdfFileInfo>();
        BindingContext = this;
        page = new PdfColoringPage();
        CheckPermissions(); // Check storage permission
        LoadPdfFiles();
    }

    private async void CheckPermissions()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.StorageRead>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("B³¹d", "Brak dostêpu do plików. Prosze przyznaæ uprawnienia do pamiêci", "OK");
            }
        }
    }

    private async void LoadPdfFiles()
    {
        try
        {
            var projection = new string[] { MediaStore.MediaColumns.DisplayName, MediaStore.MediaColumns.Data, MediaStore.MediaColumns.DateAdded };
            var uri = MediaStore.Files.GetContentUri("external");
            var selection = $"{MediaStore.MediaColumns.MimeType} = ?";
            var selectionArgs = new string[] { "application/pdf" };

            var context = Android.App.Application.Context;
            var cursor = context.ContentResolver.Query(uri, projection, selection, selectionArgs, null);

            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    var fileName = cursor.GetString(cursor.GetColumnIndex(MediaStore.MediaColumns.DisplayName));
                    var fileUri = cursor.GetString(cursor.GetColumnIndex(MediaStore.MediaColumns.Data));
                    var creationDate = cursor.GetLong(cursor.GetColumnIndex(MediaStore.MediaColumns.DateAdded));

                    // U¿ycie SAF lub DocumentFile do uzyskania dostêpu do pliku
                    var file = DocumentFile.FromSingleUri(context, Android.Net.Uri.Parse(fileUri));
                    if (file != null)
                    {
                        PdfFiles.Add(new PdfFileInfo
                        {
                            Name = fileName,
                            CreationDate = DateTimeOffset.FromUnixTimeSeconds(creationDate).ToString("yyyy-MM-dd HH:mm:ss"),
                            FullPath = file.Uri.Path // Zwrócenie œcie¿ki absolutnej URI
                        });
                    }
                }
                cursor.Close();
            }

            if (PdfFiles.Count == 0)
            {
                await DisplayAlert("Brak plików", "Nie znaleziono plików PDF w folderze Downloads.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("B³¹d", $"Wyst¹pi³ problem: {ex.Message}", "OK");
        }
    }

    private Android.Net.Uri GetUriFromFilePath(string filePath)
    {
        Java.IO.File file = new Java.IO.File(filePath);
        Android.Net.Uri fileUri = Microsoft.Maui.Storage.FileProvider.GetUriForFile(Android.App.Application.Context, "com.companyname.painter.provider", file);
        return fileUri;
    }
    private async void FilesCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0)
        {
            var selectedPdf = e.CurrentSelection[0] as PdfFileInfo;
            if (selectedPdf != null)
            {
                await Navigation.PushAsync(page);
                
                page.StartProject(GetUriFromFilePath(selectedPdf.FullPath).Path);
            }
        }
    }
}
