namespace CefSharp.MinimalExample.Wpf
{
    using System.Windows;
    using System.Windows.Controls;

    public partial class MainWindow : Window
    {
        #region Constructors

        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region Private Methods

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Browser.CefSharpBrowser.IsBrowserInitialized)
                Browser.CefSharpBrowser.Load("www.fishgl.com");
            else
                Browser.CefSharpBrowser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
        }

        private void Browser_IsBrowserInitializedChanged(object sender, IsBrowserInitializedChangedEventArgs e)
        {
            Browser.CefSharpBrowser.Load("www.fishgl.com");
        }
        #endregion

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var bitmap = ControlSnapshot.Snapshot(Browser.CefSharpBrowser);
            var imagesource = ControlSnapshot.CreateImageFromBitmap(bitmap);
            Window x = new Window()
            {
                Content = new Image() { Source = imagesource },
                Owner = this,
                Width = 400,
                Height = 200,
                Title = "ControlSnapshot"
            };

            x.Show();
        }

    }
}