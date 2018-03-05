using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Forms.VisualStyles;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CefSharp.WinForms;

namespace CefSharp.MinimalExample.Wpf
{
  
    public class CustomBrowser : Control
    {
        static CustomBrowser()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomBrowser), new FrameworkPropertyMetadata(typeof(CustomBrowser)));
        }

        private HwndCaptureHelper _capturer;
        private ChromiumWebBrowser _browser;
        private WindowsFormsHost _host;
        private IntPtr _applicationWindowHandle;

        public static readonly DependencyProperty IsOffscreenProperty = DependencyProperty.Register(
            "IsOffscreen",
            typeof(bool), 
            typeof(CustomBrowser), 
            new FrameworkPropertyMetadata(
                default(bool), 
                FrameworkPropertyMetadataOptions.AffectsRender, 
                IsOffscreenChanged));

        private static void IsOffscreenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var d = (CustomBrowser) dependencyObject;
            d?.IsOffScreenChanged((bool)e.NewValue);
        }

        public bool IsOffscreen
        {
            get { return (bool) GetValue(IsOffscreenProperty); }
            set { SetValue(IsOffscreenProperty, value); }
        }

        private void IsOffScreenChanged(bool newValue)
        {
            if (newValue)
            {
                if (_capturer == null)
                    _capturer = new HwndCaptureHelper(_host.Handle, _applicationWindowHandle, _host.RenderTransform as TranslateTransform);

                _capturer.BeginCapture(_browser.Width, _browser.Height, () =>
                    {
                        Dispatcher.Invoke(InvalidateVisual,
                            DispatcherPriority.DataBind);
                    });
            }
            else
            {
                _capturer.StopCapture();
            }
        }

        public ChromiumWebBrowser CefSharpBrowser => _browser;
        public WindowsFormsHost Host => _host;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _host = GetTemplateChild("PART_FormsHost") as WindowsFormsHost;
            _browser = _host.Child as ChromiumWebBrowser;
            _applicationWindowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (IsOffscreen)
                _capturer?.RenderCapturedBitmap(drawingContext, RenderSize.Width, RenderSize.Height);
        }
    }
}
