// Copyright (c) Xenko contributors (https://xenko.com) 
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if XENKO_PLATFORM_UWP

using System;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Holographic;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Xenko.Core.Mathematics;
using Xenko.Graphics;

namespace Xenko.Games
{
    internal class GameWindowUWPMixedReality : GameWindow
    {
        #region Fields

        private CoreWindow coreWindow;
        private WindowHandle windowHandle;
        private ApplicationView applicationView;

        public HolographicSpace HolographicSpace { get; private set; }

        private double requiredRatio;
        private bool canResize;
        private int currentWidth;
        private int currentHeight;

        #endregion

        #region Public Properties

        public override bool AllowUserResizing
        {
            get => false;
            set { }
        }


        /// <summary>
        /// Converts a length in device-independent pixels (DIPs) to a length in physical pixels.
        /// </summary>
        private static double ConvertDipsToPixels(double dips, double dpi)
        {
            const float DipsPerInch = 96.0f;
            return Math.Floor(dips * dpi / DipsPerInch + 0.5f); // Round to nearest integer.
        }

        public override Rectangle ClientBounds
        {
            get
            {
                if (coreWindow == null)
                    throw new ArgumentException($"{nameof(GameWindow)} should have a {nameof(CoreWindow)}");

                var dpi = DisplayInformation.GetForCurrentView().LogicalDpi;

                return new Rectangle(
                    (int)ConvertDipsToPixels(coreWindow.Bounds.X, dpi), 
                    (int)ConvertDipsToPixels(coreWindow.Bounds.Y, dpi), 
                    (int)ConvertDipsToPixels(coreWindow.Bounds.Width, dpi), 
                    (int)ConvertDipsToPixels(coreWindow.Bounds.Height, dpi));
            }
        }

        public override DisplayOrientation CurrentOrientation => DisplayOrientation.Default;

        public override bool IsMinimized => false;

        public override bool IsMouseVisible { get; set; }

        public override WindowHandle NativeWindow => windowHandle;

        public override bool Visible { get; set; }
        public override bool IsBorderLess { get; set; }

        #endregion

        #region Public Methods and Operators

        public override void BeginScreenDeviceChange(bool willBeFullScreen)
        {
        }

        public override void EndScreenDeviceChange(int clientWidth, int clientHeight)
        {
        }

        #endregion

        protected internal override void Initialize(GameContext windowContext)
        {
            coreWindow = ((GameContextUWPMixedReality)windowContext)?.Control;
            HolographicSpace = ((GameContextUWPMixedReality)windowContext)?.HolographicSpace;

            if (coreWindow == null)
                throw new ArgumentException($"{nameof(GameWindow)} should have a {nameof(CoreWindow)}");

            coreWindow.SizeChanged += ResizeOnWindowChange;
            windowHandle = new WindowHandle(AppContextType.UWPMixedReality, coreWindow, IntPtr.Zero);

            applicationView = ApplicationView.GetForCurrentView();
            if (applicationView != null && windowContext.RequestedWidth != 0 && windowContext.RequestedHeight != 0)
            {
                applicationView.SetPreferredMinSize(new Size(windowContext.RequestedWidth, windowContext.RequestedHeight));
                canResize = applicationView.TryResizeView(new Size(windowContext.RequestedWidth, windowContext.RequestedHeight));
            }

            requiredRatio = windowContext.RequestedWidth / (double)windowContext.RequestedHeight;
        }

        public ulong GetPreferredAdapterId()
        {
            if (HolographicSpace == null)
                return 0;

            // The holographic space might need to determine which adapter supports
            // holograms, in which case it will specify a non-zero PrimaryAdapterId.
            int shiftPos = sizeof(uint);
            ulong id = (ulong)HolographicSpace.PrimaryAdapterId.LowPart |
                       ((ulong)HolographicSpace.PrimaryAdapterId.HighPart << shiftPos);

            return id;
        }

        private void ResizeOnWindowChange(CoreWindow sender, WindowSizeChangedEventArgs e)
        {
            OnClientSizeChanged(sender, EventArgs.Empty);
        }

        internal override void Resize(int width, int height)
        {
            currentWidth = width;
            currentHeight = height;
        }

        internal override void Run()
        {
            // Call InitCallback only first time
            if (InitCallback != null)
            {
                InitCallback();
                InitCallback = null;
            }

            try
            {
                while (true)
                {
                    coreWindow.Dispatcher.ProcessEvents(CoreProcessEventsOption.ProcessAllIfPresent);
                    if (Exiting)
                    {
                        Destroy();
                        break;
                    }

                    RunCallback();
                }
            }
            finally
            {
                ExitCallback?.Invoke();
            }
        }

        protected internal override void SetSupportedOrientations(DisplayOrientation orientations)
        {
            // Desktop doesn't have orientation (unless on Windows 8?)
        }

        protected override void SetTitle(string title)
        {
        }
    }
}

#endif
