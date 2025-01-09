using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using PDFtoImage;
using System.Text;
using System.Diagnostics.Metrics;
using SkiaSharp.Views.Maui;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Android.OS;
using Microsoft.Maui.Controls.Internals;
namespace Painter
{
    public class PathData
    {
        public SKPath Path { get; set; }
        public SKColor Color { get; set; }
        public float StrokeWidth { get; set; }
        public float Opacity { get; set; }
    }
    public partial class PdfColoringPage : ContentPage
    {
        //private string pdfFilePath;
        private SKBitmap pdfPageBitmap;
        private List<SKBitmap> pdfThumbnails;
        private List<SKBitmap> pdfPages;
        private SKBitmap currentPageBitmap;
        private float PoziomOpacity = 0;
        private float PoziomGrubosci = 0;

        private bool isErasing = false;
        private bool isPainting = false;
        private SKPoint Touchpoint;
        private bool ready = false;
        private Color PickedColor;
        private Dictionary<int, List<PathData>> pagePaths;

        private SKPath path = new SKPath();
        private List<SKPath> paths = new List<SKPath>();
        private int currentPageIndex = 0;

        private float scaleFactor = 1f;
        private SKPoint lastTouch = SKPoint.Empty;
        private SKPoint lastTouch2 = SKPoint.Empty;
        private SKPoint touchCenter = SKPoint.Empty;
        public PdfColoringPage()
        {
            InitializeComponent();
            pdfPageBitmap = new SKBitmap();
            pdfThumbnails = new List<SKBitmap>();
            pdfPages = new List<SKBitmap>();
            currentPageBitmap = new SKBitmap();
            Touchpoint = SKPoint.Empty;
            LoadingScreen.IsVisible = false;
            PickedColor = Color.FromRgba(0, 0, 0, 255);
            pagePaths = new Dictionary<int, List<PathData>>();
        }
        private async void OnChoosePdfFileClicked(object sender, EventArgs e)
        {
            this.ready = false;
            ClearResources();
            ClearCanvasViews();
            try
            {

                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Choose a PDF File",
                    FileTypes = FilePickerFileType.Pdf
                });

                if (fileResult == null)
                {
                    await DisplayAlert("Error", "No file selected.", "OK");
                    return;
                }
                string pdfFilePath = fileResult.FullPath;
                FileNameLabel.Text = Path.GetFileName(pdfFilePath);

                DisplayThumbnails(pdfFilePath);
                LoadingScreen.IsVisible = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load PDF file: {ex.Message}", "OK");
            }
        }
        private void ClearCanvasViews()
        {
            currentPageBitmap = new SKBitmap();
            CanvasView.InvalidateSurface();
            DrawingView.InvalidateSurface();
            PdfThumbnails.Children.Clear();
            Touchpoint = SKPoint.Empty;
            isErasing = false;
            ProgressbarLoading.Progress = 0;
        }
        private void ClearResources()
        {
            foreach (var bitmap in pdfPages)
            {
                bitmap?.Dispose();
            }
            pdfPages.Clear();

            foreach (var thumbnail in pdfThumbnails)
            {
                thumbnail?.Dispose();
            }
            pdfThumbnails.Clear();

            pdfPageBitmap?.Dispose();
            pdfPageBitmap = null;

            currentPageBitmap?.Dispose();
            currentPageBitmap = null;
            pagePaths.Clear();
            GC.Collect();
        }
        private async Task DisplayFullPage(int pageIndex)
        {
            try
            {
                currentPageBitmap = null;
                ClearDrawingView();
                CanvasView.InvalidateSurface();

                LoadingScreen.IsVisible = true;

                if (pageIndex < 0 || pageIndex >= pdfPages.Count)
                {
                    await DisplayAlert("Error", "Invalid page index.", "OK");
                    return;
                }

                currentPageIndex = pageIndex;
                currentPageBitmap = pdfPages[pageIndex];
                LoadingScreen.IsVisible = false;

                CanvasView.InvalidateSurface();
                DrawingView.InvalidateSurface();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error displaying page: {ex.Message}", "OK");
            }
        }
        private async Task<SKBitmap> GenerateFullPdfPage(string pdfFilePath, int pageIndex)
        {
            try
            {
                byte[] pdfBytes = await File.ReadAllBytesAsync(pdfFilePath);
                var render = await Task.Run(() => PDFtoImage.Conversion.ToImage(pdfBytes));

                if (render != null)
                {

                    return render;
                }
                else
                {
                    throw new Exception("Nie uda³o siê za³adowaæ strony PDF.");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"B³¹d podczas generowania strony: {ex.Message}", "OK");
                return null;
            }
        }
        private async void DisplayThumbnails(string pdfFpath)
        {
            try
            {
                this.ready = false;
                var thumbnails = await Task.Run(() => GeneratePdfThumbnails(pdfFpath));
                await DisplayFullPage(0);
                this.ready = true;
                PdfThumbnails.Children.Clear();
                LoadingScreen.IsVisible = false;
                int len = thumbnails.Count;
                for (int i = 0; i < len; i++)
                {
                    var thumbnail = thumbnails[i];
                    var thumbnailCanvas = new SKCanvasView
                    {
                        BackgroundColor = Color.FromRgba(128, 128, 128, 255),
                        HeightRequest = 100,
                        WidthRequest = 100,
                    };

                    thumbnailCanvas.PaintSurface += (sender, e) =>
                    {
                        var canvas = e.Surface.Canvas;
                        var surfaceWidth = e.Info.Width;
                        var surfaceHeight = e.Info.Height;

                        float scaleX = surfaceWidth / (float)thumbnail.Width;
                        float scaleY = surfaceHeight / (float)thumbnail.Height;
                        float scale = Math.Min(scaleX, scaleY);

                        scale = Math.Min(1, scale);

                        int scaledWidth = (int)(thumbnail.Width * scale);
                        int scaledHeight = (int)(thumbnail.Height * scale);
                        float offsetX = (surfaceWidth - scaledWidth) / 2f;
                        float offsetY = (surfaceHeight - scaledHeight) / 2f;
                        canvas.Clear(SKColors.White);
                        canvas.DrawBitmap(thumbnail, new SKRect(0, 0, scaledWidth, scaledHeight));
                    };

                    int pageIndex = i;
                    var tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += async (s, e) =>
                    {
                        currentPageIndex = pageIndex;
                        await DisplayFullPage(pageIndex);
                    };

                    thumbnailCanvas.GestureRecognizers.Add(tapGesture);

                    PdfThumbnails.Children.Add(thumbnailCanvas);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to generate thumbnails: {ex.Message}", "OK");
            }
        }
        private SKBitmap ResizeToThumbnail(SKBitmap originalImage)
        {
            float maxThumbnailSize = 3500;
            float scale = Math.Min(maxThumbnailSize / originalImage.Width, maxThumbnailSize / originalImage.Height);
            int newWidth = (int)(originalImage.Width * scale);
            int newHeight = (int)(originalImage.Height * scale);
            var resizedBitmap = new SKBitmap(newWidth, newHeight);
            using (var canvas = new SKCanvas(resizedBitmap))
            {
                canvas.DrawBitmap(originalImage, 0, 0);
            }

            return resizedBitmap;
        }
        private async Task<List<SKBitmap>> GeneratePdfThumbnails(string PdfPage)
        {
            List<SKBitmap> thumbnails = new List<SKBitmap>();
            try
            {
                byte[] pdfBytes = await File.ReadAllBytesAsync(PdfPage);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new Exception("PDF file is empty or cannot be read.");
                }

                Device.BeginInvokeOnMainThread(() => LoadingScreen.IsVisible = true);

                var render = await Task.Run(() => PDFtoImage.Conversion.ToImages(pdfBytes));

                if (render == null || !render.Any())
                {
                    throw new Exception("Failed to render PDF pages.");
                }

                int totalPages = render.Count();
                int currpage = 0;

                foreach (var pageImage in render)
                {
                    var thumbnail = ResizeToThumbnail(pageImage);
                    thumbnails.Add(thumbnail);
                    pdfPages.Add(pageImage);

                    Device.BeginInvokeOnMainThread(() =>
                    {
                        currpage++;
                        float progress = (float)(currpage + 1) / totalPages;
                        ProgressbarLoading.Progress = progress;
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error generating thumbnails: {ex.Message}", "OK");
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() => LoadingScreen.IsVisible = false);
            }
            return thumbnails;
        }


        private void CanvasView_PaintSurface(object sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
        {
            if (currentPageBitmap == null || currentPageBitmap.Width == 0 || currentPageBitmap.Height == 0)
            {
                return;
            }

            var canvas = e.Surface.Canvas;
            var surfaceWidth = e.Info.Width;
            var surfaceHeight = e.Info.Height;

            float scaleX = surfaceWidth / (float)currentPageBitmap.Width;
            float scaleY = surfaceHeight / (float)currentPageBitmap.Height;
            float scale = Math.Min(scaleX, scaleY);


            float scaledWidth = currentPageBitmap.Width * scale;
            float scaledHeight = currentPageBitmap.Height * scale;


            float offsetX = (surfaceWidth - scaledWidth) / 2f;
            float offsetY = (surfaceHeight - scaledHeight) / 2f;


            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(currentPageBitmap, new SKRect(offsetX, offsetY, offsetX + scaledWidth, offsetY + scaledHeight));
        }

        private void Marker(object sender, EventArgs e)
        {
            isErasing = false;
            isPainting = !isPainting;
        }
        private void Gumka(object sender, EventArgs e)
        {
            isErasing = !isErasing;
            isPainting = false;
        }
        private void ColorPicker(object sender, EventArgs e)
        {
            this.FrameColorPicker.IsVisible = !this.FrameColorPicker.IsVisible;
        }
        private void Opcje(object sender, EventArgs e)
        {
            this.FrameOpcje.IsVisible = !this.FrameOpcje.IsVisible;
        }

        private void Opacity_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            this.PoziomOpacity = (float)e.NewValue;
            OpacityLevel.Text = "Przezroczystosc: " + Math.Round(e.NewValue, 0).ToString();
        }
        private void Size_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            this.PoziomGrubosci = (float)e.NewValue;
            SizeLevel.Text = "Rozmiar: " + Math.Round(e.NewValue, 0).ToString();
        }

        private void DrawingView_PaintSurface(object sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);


            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                BlendMode = SKBlendMode.Src,
            })
            {
                if (pagePaths.TryGetValue(currentPageIndex, out var paths))
                {
                    foreach (var pathData in paths)
                    {
                        paint.Color = pathData.Color;
                        paint.StrokeWidth = pathData.StrokeWidth;
                        canvas.DrawPath(pathData.Path, paint);
                    }
                }

                if (isErasing && path != null)
                {
                    paint.Color = SKColors.Transparent;
                    paint.StrokeWidth = PoziomGrubosci;
                    paint.BlendMode = SKBlendMode.Src;

                    canvas.DrawPath(path, paint);
                }
            }

            if (!pagePaths.ContainsKey(currentPageIndex))
            {
                pagePaths[currentPageIndex] = new List<PathData>();
            }
        }

        private void DrawingView_Touch(object sender, SKTouchEventArgs e)
        {
            if (!ready || (!isPainting && !isErasing))
            {
                return;
            }

            Touchpoint = e.Location;

            if (e.ActionType == SKTouchAction.Pressed)
            {
                if (e.InContact)
                {
                    if (lastTouch == SKPoint.Empty)
                    {
                        lastTouch = e.Location;
                    }
                    else if (lastTouch2 == SKPoint.Empty)
                    {
                        lastTouch2 = e.Location;
                    }
                }
            }
            else if (e.ActionType == SKTouchAction.Moved)
            {
                if (e.InContact && lastTouch != SKPoint.Empty && lastTouch2 != SKPoint.Empty)
                {
                    float currentDistance = (e.Location - lastTouch2).Length;
                    float previousDistance = (lastTouch - lastTouch2).Length;

                    scaleFactor *= (currentDistance > previousDistance) ? 1.1f : 0.9f;

                    lastTouch = e.Location;
                    lastTouch2 = e.Location;
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                lastTouch = SKPoint.Empty;
                lastTouch2 = SKPoint.Empty;
            }

            if (e.ActionType == SKTouchAction.Pressed)
            {
                if (isPainting)
                {

                    path = new SKPath();
                    path.MoveTo(Touchpoint);

                    var newPathData = new PathData
                    {
                        Path = path,
                        Color = new SKColor(
                            (byte)(PickedColor.Red * 255),
                            (byte)(PickedColor.Green * 255),
                            (byte)(PickedColor.Blue * 255),
                            (byte)(PoziomOpacity * 2.5)),
                        StrokeWidth = PoziomGrubosci,
                        Opacity = PoziomOpacity
                    };

                    if (!pagePaths.ContainsKey(currentPageIndex))
                    {
                        pagePaths[currentPageIndex] = new List<PathData>();
                    }
                    pagePaths[currentPageIndex].Add(newPathData);
                }
                else if (isErasing)
                {

                    path = new SKPath();
                    path.MoveTo(Touchpoint);
                }
            }
            else if (e.ActionType == SKTouchAction.Moved)
            {
                if (isPainting && path != null)
                {
                    path.LineTo(Touchpoint);
                }

                if (isErasing && path != null)
                {
                    path.LineTo(Touchpoint);
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                if (isPainting && path != null && path.PointCount > 1)
                {
                    var newPathData = new PathData
                    {
                        Path = path,
                        Color = new SKColor(
                            (byte)(PickedColor.Red * 255),
                            (byte)(PickedColor.Green * 255),
                            (byte)(PickedColor.Blue * 255),
                            (byte)(PoziomOpacity * 2.5)
                        ),
                        StrokeWidth = PoziomGrubosci,
                        Opacity = PoziomOpacity
                    };

                    if (!pagePaths.ContainsKey(currentPageIndex))
                    {
                        pagePaths[currentPageIndex] = new List<PathData>();
                    }

                    pagePaths[currentPageIndex].Add(newPathData);
                    path = null;
                }

                if (isErasing && path != null)
                {
                    var newErasePathData = new PathData
                    {
                        Path = path,
                        Color = SKColors.Transparent,
                        StrokeWidth = PoziomGrubosci,
                        Opacity = 0
                    };

                    if (!pagePaths.ContainsKey(currentPageIndex))
                    {
                        pagePaths[currentPageIndex] = new List<PathData>();
                    }

                    pagePaths[currentPageIndex].Add(newErasePathData);
                    path = null;
                }
            }

            e.Handled = true;
            DrawingView.InvalidateSurface();
        }

        private void MyColorPicker_PickedColorChanged(object sender, Maui.ColorPicker.PickedColorChangedEventArgs e)
        {
            PickedColor = e.NewPickedColorValue;
        }
        private void ClearDrawingView()
        {
            path?.Reset();
            DrawingView.InvalidateSurface();
        }

        private async void SavePdf_Clicked(object sender, EventArgs e)
        {
            if (!this.ready)
            {
                DisplayAlert("Info", "There's nothing to save", "OK");
                return;
            }
            SaveFileInfo.IsVisible = !SaveFileInfo.IsVisible;
        }

        private void SaveButton_Clicked(object sender, EventArgs e)
        {
            if (!this.ready)
            {
                DisplayAlert("Info", "There's nothing to save", "OK");
                return;
            }
            string filename = SaveFileText.Text;
            if (string.IsNullOrEmpty(filename))
            {
                DisplayAlert("Error", "Please provide a filename.", "OK");
                return;
            }
            string drawingFolderPath = "/storage/emulated/0/Android/files/com.companyname.painter/";
            if (!Directory.Exists(drawingFolderPath))
            {
                Directory.CreateDirectory(drawingFolderPath);
            }

            string SavePath = Path.Combine(drawingFolderPath, $"{filename}.pdf");
            if (File.Exists(SavePath))
            {
                DisplayAlert("Error", "A file with this name already exists. Please choose a different name.", "OK");
                return;
            }

            try
            {
                int len = pdfPages.Count;
                using (var stream = File.Create(SavePath))
                using (var document = SKDocument.CreatePdf(stream))
                {
                    for (int i = 0; i < len; i++)
                    {
                        using (var pdfCanvas = document.BeginPage(pdfPages[i].Width, pdfPages[i].Height))
                        {
                            pdfCanvas.DrawBitmap(pdfPages[i], new SKRect(0, 0, pdfPages[i].Width, pdfPages[i].Height));
                            if (pagePaths.TryGetValue(i, out var paths))
                            {
                                using (var paint = new SKPaint { IsAntialias = true })
                                {
                                    foreach (var pathData in paths)
                                    {
                                        paint.Color = pathData.Color;
                                        paint.StrokeWidth = pathData.StrokeWidth;
                                        paint.Style = SKPaintStyle.Stroke;
                                        pdfCanvas.DrawPath(pathData.Path, paint);
                                    }
                                }
                            }
                            document.EndPage();
                        }
                    }
                }
                DisplayAlert("Success", $"PDF saved to: {SavePath}", "OK");
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to save PDF: {ex.Message}", "OK");
            }
        }
    }
}
