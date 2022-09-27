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
using Windows.ApplicationModel.DataTransfer;

namespace OcrUwpApp
{
    public class WordHolder
    {
        OcrWord mOcrWord;
        Rect mWordBbox;

        public WordHolder(OcrWord ocrWord)
        {
            mOcrWord = ocrWord;
            mWordBbox = ocrWord.BoundingRect;
        }

        public double Width => mWordBbox.Width;

        public double Height => mWordBbox.Height;

        public Thickness Position => new Thickness(mWordBbox.Left, mWordBbox.Top, 0, 0);

        public Rect WordRect => mWordBbox;

        public String Text => mOcrWord.Text;

        public void Transform(ScaleTransform scale)
        {
            mWordBbox = scale.TransformBounds(mOcrWord.BoundingRect);
        }
    }

    public sealed partial class MainPage : Page
    {
        // Bitmap holder of currently loaded image.
        private SoftwareBitmap mSoftwareBitmap;
        private List<WordHolder> mWordHolders;
        private WordHolder mWordHolder;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;

            Img.PointerReleased += Img_PointerReleased;
            Img.SizeChanged += Img_SizeChanged;
        }

        private void Img_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWordBboxTransform();
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Resources/OcrSampleImg2.jpg"));
            await OpenImageFromFileAsync(file);

            await RunOcrAsync();
        }

        private void Img_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var curretPoint = e.GetCurrentPoint(Img);
            var currentPos = curretPoint.Position;

            if (mWordHolders == null)
                return;

            foreach (var word in mWordHolders)
            {
                var WordRect = word.WordRect;

                double x_min = WordRect.X;
                double x_max = WordRect.X + WordRect.Width;

                double y_min = WordRect.Y;
                double y_max = WordRect.Y + WordRect.Height;

                if ((currentPos.X > x_min && currentPos.X < x_max)
                    && (currentPos.Y > y_min && currentPos.Y < y_max))
                {
                    mWordHolder = word;

                    ShowBboxWord(word);
                    break;
                }
            }
        }

        void ShowBboxWord(WordHolder word)
        {
            var rect = new Rectangle();
            rect.Margin = word.Position;

            rect.HorizontalAlignment = HorizontalAlignment.Left;
            rect.VerticalAlignment = VerticalAlignment.Top;
            
            rect.Width = word.Width;
            rect.Height = word.Height;
            
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
            mWordHolders = new List<WordHolder>();

            // Try to create OcrEngine for first supported language from UserProfile.GlobalizationPreferences.Languages list.
            // If none of the languages are available on device, method returns null.
            ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

            if (ocrEngine == null)
            {
                return;
            }

            // Recognize text from image.
            var ocrResult = await ocrEngine.RecognizeAsync(mSoftwareBitmap);

            var sTransf = GetScaleTransform();

            // Create overlay boxes over recognized words.
            foreach (var line in ocrResult.Lines)
            {
                // Determine if line is horizontal or vertical.
                // Vertical lines are supported only in Chinese Traditional and Japanese languages.
                Rect lineRect = Rect.Empty;
                foreach (var word in line.Words)
                {
                    var wHolder = new WordHolder(word);
                    wHolder.Transform(sTransf);
                    mWordHolders.Add(wHolder);
                }
            }

            // NOTE: display all results in a dialog
            
            // Display recognized text.
            //var strResult = ocrResult.Text;
            //var dialogResult = new ContentDialog();
            //dialogResult.Content = strResult;
            //dialogResult.PrimaryButtonText = "OK";

            //await dialogResult.ShowAsync();
        }

        private void UpdateWordBboxTransform()
        {
            if (mWordHolders == null)
                return;

            var scaleT = GetScaleTransform();
            foreach (var word in mWordHolders)
            {
                word.Transform(scaleT);
            }
        }

        private ScaleTransform GetScaleTransform()
        {
            // Need for text overlay
            // Prepare scale transform for words since image is not diplayed in original size
            var scaleTransf = new ScaleTransform
            {
                CenterX = 0,
                CenterY = 0,
                ScaleX = Img.ActualWidth / mSoftwareBitmap.PixelWidth,
                ScaleY = Img.ActualHeight / mSoftwareBitmap.PixelHeight
            };

            return scaleTransf;
        }

        private void copyText_Click(object sender, RoutedEventArgs e)
        {
            DataPackage dp = new DataPackage();
            dp.SetText(mWordHolder.Text);
            Clipboard.SetContent(dp);
        }
    }
}
