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
            Browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
        }

        #endregion

        #region Private Methods

        private void Browser_IsBrowserInitializedChanged(object sender, IsBrowserInitializedChangedEventArgs e)
        {
            Browser.Load("www.google.com");
        }
        #endregion

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var bitmap = ControlSnapshot.Snapshot(Browser);
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