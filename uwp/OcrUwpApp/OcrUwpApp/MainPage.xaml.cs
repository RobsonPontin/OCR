using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.UI.Xaml.Shapes;
using System.Threading.Tasks;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OcrUwpApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Bitmap holder of currently loaded image.
        private SoftwareBitmap mSoftwareBitmap;
        private List<OcrWord> mOcrWords;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;

            Img.PointerReleased += Img_PointerReleased;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Resources/OcrSampleImg.jpg"));

            await OpenImageFromFileAsync(file);
            
        }

        private void Img_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var curretPoint = e.GetCurrentPoint(Img);
            var currentPos = curretPoint.Position;

            if (mOcrWords == null)
                return;

            foreach (var word in mOcrWords)
            {
                var WordRect = word.BoundingRect;

                double x_min = WordRect.X;
                double x_max = WordRect.X + WordRect.Width;

                double y_min = WordRect.Y;
                double y_max = WordRect.Y + WordRect.Height;

                if ((currentPos.X > x_min && currentPos.X < x_max)
                    && (currentPos.Y > y_min && currentPos.Y < y_max))
                {
                    ShowBboxWord(word.BoundingRect);
                    break;
                }
            }

        }

        void ShowBboxWord(Rect bbox)
        {
            var rect = new Rectangle();
            rect.Margin = new Thickness(bbox.Left, bbox.Top, bbox.Right, bbox.Bottom);
            
            rect.Width = bbox.Width;
            rect.Height = bbox.Height;
            
            rect.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
            rect.Opacity = 0.2;

            ImageGrid.Children.Add(rect);
        }


        private async void btnOpenImg_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".jpg");

            var file = await filePicker.PickSingleFileAsync();
            if (file == null)
                return;

            await OpenImageFromFileAsync(file);
        }

        private async Task OpenImageFromFileAsync(StorageFile file)
        {
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                mSoftwareBitmap = await decoder.GetSoftwareBitmapAsync();

                var imgSource = new WriteableBitmap(mSoftwareBitmap.PixelWidth, mSoftwareBitmap.PixelHeight);
                mSoftwareBitmap.CopyToBuffer(imgSource.PixelBuffer);

                Img.Source = imgSource;
            }
        }

        private async void btnOcr_Click(object sender, RoutedEventArgs e)
        {
            if (mSoftwareBitmap == null)
                return;

            // Check if OcrEngine supports image resolution.
            if (mSoftwareBitmap.PixelWidth > OcrEngine.MaxImageDimension 
                || mSoftwareBitmap.PixelHeight > OcrEngine.MaxImageDimension)
            {
                var contentDialog = new ContentDialog();

                contentDialog.Content = 
                    String.Format("Bitmap dimensions ({0}x{1}) are too big for OCR.", mSoftwareBitmap.PixelWidth, mSoftwareBitmap.PixelHeight) +
                    "Max image dimension is " + OcrEngine.MaxImageDimension;
                contentDialog.PrimaryButtonText = "OK";

                await contentDialog.ShowAsync();

                return;
            }

            await RunOcrAsync();
        }

        async Task RunOcrAsync()
        {
            OcrEngine ocrEngine = null;
            mOcrWords = new List<OcrWord>();

            // Try to create OcrEngine for first supported language from UserProfile.GlobalizationPreferences.Languages list.
            // If none of the languages are available on device, method returns null.
            ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

            if (ocrEngine == null)
            {
                return;
            }

            // Recognize text from image.
            var ocrResult = await ocrEngine.RecognizeAsync(mSoftwareBitmap);

            // Display recognized text.
            var strResult = ocrResult.Text;

            // Create overlay boxes over recognized words.
            foreach (var line in ocrResult.Lines)
            {
                // Determine if line is horizontal or vertical.
                // Vertical lines are supported only in Chinese Traditional and Japanese languages.
                Rect lineRect = Rect.Empty;
                foreach (var word in line.Words)
                {
                    mOcrWords.Add(word);
                }
            }



            var dialogResult = new ContentDialog();
            dialogResult.Content = strResult;
            dialogResult.PrimaryButtonText = "OK";

            await dialogResult.ShowAsync();
        }

    }
}
