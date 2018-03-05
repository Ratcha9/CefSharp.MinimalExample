using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace CefSharp.MinimalExample.Wpf
{
    public class HwndCaptureHelper
    {
        #region Fields

        private readonly Stopwatch _watch = new Stopwatch();
        private readonly IntPtr _hwnd;
        private readonly IntPtr _rootHwnd;
        private readonly TranslateTransform _transform;

        private bool _isCapturing;
        private CancellationTokenSource _cancellationTokenSource;
        private uint _hwndStyleCache;
        private int _imgCountRendered;

        private readonly object _lock = new object();
        private MemoryStream _memoryStream;
        private MemoryStream _initialStream;
        private Bitmap _image;
        private bool _needRender;
        private int _bitmapWidth;
        private int _bitmapHeight;
        private Action _bitmapCapturedAction;

        #endregion

        #region Constructor

        public HwndCaptureHelper(IntPtr nestedHwnd, IntPtr rootHwnd, TranslateTransform transform)
        {
            _hwnd = nestedHwnd;
            _rootHwnd = rootHwnd;
            _transform = transform;
        }

        #endregion

        #region Public Methods

        public void BeginCapture(int bitmapWidth, int bitmapHeight, Action bitmapCapturedCallback)
        {
            if (_isCapturing)
                throw new InvalidOperationException("Capturing offscreen hwnd is already running");

            _bitmapCapturedAction = bitmapCapturedCallback;
            _bitmapWidth = bitmapWidth;
            _bitmapHeight = bitmapHeight;

            _watch.Restart();
            // capture an initial bitmap in order to prevent black image.
            CaptureInitial();
            _needRender = true;

            // detach hwnd and move out  of bounds
            MoveHwndOutOfBounds();
            DetachHwnd(_hwnd);

            // save current windowstyle of captured hwnd
            _hwndStyleCache = HideWindowInTaskbar(_hwnd);

            _imgCountRendered = 0;
            _cancellationTokenSource = RunCapturingTask();
            _isCapturing = true;
        }

        public void StopCapture()
        {
            _watch.Stop();
            _cancellationTokenSource.Cancel(throwOnFirstException: true);

            MoveHwndBack();
            RestoreWindowStyle(_hwnd, _hwndStyleCache);
            AttachHwnd(_hwnd, _rootHwnd);

            _memoryStream?.Dispose();
            _memoryStream = null;
            _initialStream?.Dispose();
            _initialStream = null;
            _image = null;
            _bitmapCapturedAction = null;
            _isCapturing = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            double fps = (int)(_imgCountRendered / _watch.Elapsed.TotalSeconds);
            MessageBox.Show($"{fps} fps in total {_watch.Elapsed.TotalSeconds} seconds");
        }

        public async void AdjustBitmapSize(int width, int height)
        {
            if (_isCapturing)
            {
                _cancellationTokenSource.Cancel(throwOnFirstException: true);
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;

                await Task.Delay(50);
            }

            _bitmapWidth = width;
            _bitmapHeight = height;

            if (_isCapturing)
            {
                _cancellationTokenSource = RunCapturingTask();
            }
        }

        public void RenderCapturedBitmap(DrawingContext drawingContext, double renderWidth, double renderHeight)
        {
            if (_memoryStream == null && _initialStream == null)
                return;

            if (!_needRender)
                return;

            var bitmapimage = new BitmapImage();

            try
            {
                lock (_lock)
                {
                    bitmapimage.BeginInit();
                    bitmapimage.StreamSource = _memoryStream != null && _memoryStream.Length > 0 ? _memoryStream : _initialStream;
                    bitmapimage.CacheOption = BitmapCacheOption.None;
                    bitmapimage.EndInit();
                    bitmapimage.Freeze();
                }

                drawingContext.DrawImage(bitmapimage, new System.Windows.Rect(0, 0, renderWidth, renderHeight));
                _imgCountRendered++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                _needRender = false;
            }
        }

        #endregion

        #region Private Methods

        private void MoveHwndBack()
        {
            _transform.X = 0;
        }

        private void MoveHwndOutOfBounds()
        {
            _transform.X = 5000;
        }

        private void CaptureInitial()
        {
            _image = new Bitmap(_bitmapWidth, _bitmapHeight, PixelFormat.Format32bppRgb);

            CaptureNestedHwnd(_hwnd, _image);

            _initialStream?.Dispose();
            _initialStream = new MemoryStream();

            _image.Save(_initialStream, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        /// <summary>
        ///     returns previous window ex style
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        private uint HideWindowInTaskbar(IntPtr hwnd)
        {
            var result = NativeMethods.GetWindowLong(hwnd, NativeMethods.WindowLongInputParam.GWL_EXSTYLE);
            var style = NativeMethods.WindowStyleValues.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.WindowLongInputParam.GWL_EXSTYLE, style);
            return result;
        }

        private void RestoreWindowStyle(IntPtr hwnd, uint previousStyle)
        {
            NativeMethods.SetWindowLong(hwnd, NativeMethods.WindowLongInputParam.GWL_EXSTYLE, previousStyle);
        }

        private void AttachHwnd(IntPtr hwnd, IntPtr parentHwnd)
        {
            NativeMethods.SetParent(hwnd, parentHwnd);
        }

        private void DetachHwnd(IntPtr hwnd)
        {
            NativeMethods.SetParent(hwnd, IntPtr.Zero);
        }

        #endregion

        #region Async Capturing

        private CancellationTokenSource RunCapturingTask()
        {
            var source = new CancellationTokenSource();
            var token = source.Token;
            _image = new Bitmap(_bitmapWidth, _bitmapHeight, PixelFormat.Format32bppRgb);

            _memoryStream?.Dispose();
            _memoryStream = new MemoryStream();

            Task.Run(() => CapturingLoop(token, _hwnd), token);

            return source;
        }

        private async void CapturingLoop(CancellationToken token, IntPtr handle)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                CapturePrintWindow(handle, _image);

                if (_needRender)
                {
                    var loops = 0;
                    for (var i = 0; i < 10; i++)
                    {
                        await Task.Delay(2, token).ConfigureAwait(false);
                        loops++;
                        if (!_needRender)
                            break;
                    }

                    if (_needRender)
                        continue;
                }

                lock (_lock)
                {
                    if (token.IsCancellationRequested)
                        break;

                    if (_memoryStream == null)
                        _memoryStream = new MemoryStream();

                    _image.Save(_memoryStream, ImageFormat.Bmp);
                }

                if (token.IsCancellationRequested)
                    break;

                if (_memoryStream != null && _memoryStream.Length > 0)
                {
                    _needRender = true;
                    _bitmapCapturedAction();
                }

                if (token.IsCancellationRequested)
                    break;

                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        #endregion

        #region Capture Implementations

        /// <summary>
        ///     Uses BitBlt
        /// </summary>
        /// <param name="srcHwnd"></param>
        /// <param name="targetBitmap"></param>
        private bool CaptureNestedHwnd(IntPtr srcHwnd, Bitmap targetBitmap)
        {
            var srcDc = NativeMethods.GetDC(srcHwnd);

            var success = false;
            try
            {
                using (var g = Graphics.FromImage(targetBitmap))
                {
                    var destinationDeviceContext = g.GetHdc();

                    try
                    {
                        success = NativeMethods.BitBlt(destinationDeviceContext,
                            0,
                            0,
                            targetBitmap.Width,
                            targetBitmap.Height,
                            srcDc,
                            0,
                            0,
                            NativeMethods.TernaryRasterOperations.SRCCOPY);
                    }
                    finally
                    {
                        g.ReleaseHdc(destinationDeviceContext);
                    }
                }
            }
            finally
            {
                NativeMethods.ReleaseDC(srcHwnd, srcDc);
            }

            return success;
        }

        private bool CapturePrintWindow(IntPtr srcHwnd, Bitmap targetBitmap)
        {
            var succeeded = false;
            try
            {
                using (var g = Graphics.FromImage(targetBitmap))
                {
                    var destinationDeviceContext = g.GetHdc();
                    try
                    {
                        succeeded = NativeMethods.PrintWindow(srcHwnd, destinationDeviceContext, 0);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    finally
                    {
                        g.ReleaseHdc(destinationDeviceContext);
                    }
                }
            }
            catch (Exception e)
            {
                succeeded = false;
                Console.WriteLine(e);
            }

            return succeeded;
        }

        #endregion
    }
}