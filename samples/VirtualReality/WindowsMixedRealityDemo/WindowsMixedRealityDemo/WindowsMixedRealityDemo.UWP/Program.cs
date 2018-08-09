using System;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Holographic;
using Windows.UI.Core;
using Windows.UI.Popups;
using Xenko.Engine;
using Xenko.Games;

namespace WindowsMixedRealityDemo
{
    /// <summary>
    /// Windows Holographic application using SharpDX.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        [MTAThread]
        public static void Main()
        {
            if (HolographicSpace.IsSupported && HolographicSpace.IsAvailable)
            {
                var viewFactory = new AppViewSource();
                CoreApplication.Run(viewFactory);
            }
            else
            {
                //TODO:
            }
        }

        private class AppViewSource : IFrameworkViewSource
        {
            public IFrameworkView CreateView()
            {
                var frameworkView = new AppView();
                return frameworkView;
            }
        }

        private class AppView : IFrameworkView, IDisposable
        {
            private Game _game;
            private CoreWindow _coreWindow;

            private bool _windowVisible;
            private bool _windowClosed;
            private HolographicSpace holographicSpace;

            public void Dispose()
            {
                if (_game != null)
                {
                    _game.Dispose();
                    _game = null;
                }
            }

            #region IFrameworkView Members

            /// <summary>
            /// The first method called when the IFrameworkView is being created.
            /// Use this method to subscribe for Windows shell events and to initialize your app.
            /// </summary>
            void IFrameworkView.Initialize(CoreApplicationView applicationView)
            {
                //  ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
                applicationView.Activated += this.OnViewActivated;
            }

            /// <summary>
            /// Called when the app view is activated. Activates the app's CoreWindow.
            /// </summary>
            private void OnViewActivated(CoreApplicationView sender, IActivatedEventArgs args)
            {
                // Run() won't start until the CoreWindow is activated.
                sender.CoreWindow.Activate();
            }

            /// <summary>
            /// Called when the CoreWindow object is created (or re-created).
            /// </summary>
            void IFrameworkView.SetWindow(CoreWindow window)
            {
                _coreWindow = window;

                // Register for notification that the app window is being closed.
                window.Closed += this.OnWindowClosed;

                // Register for notifications that the app window is losing focus.
                window.VisibilityChanged += this.OnVisibilityChanged;


                // Create a holographic space for the core window for the current view.
                // Presenting holographic frames that are created by this holographic space will put
                // the app into exclusive mode.
                holographicSpace = HolographicSpace.CreateForCoreWindow(window);
            }

            /// <summary>
            /// The Load method can be used to initialize scene resources or to load a
            /// previously saved app state.
            /// </summary>
            void IFrameworkView.Load(string entryPoint)
            {
            }

            /// <summary>
            /// This method is called after the window becomes active. It oversees the
            /// update, draw, and present loop, and also oversees window message processing.
            /// </summary>
            void IFrameworkView.Run()
            {
                _game = new Game();
                _game.IsFixedTimeStep = true;
                _game.IsDrawDesynchronized = true;
                _game.GraphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
                _game.TargetElapsedTime = TimeSpan.FromSeconds(1 / 60.0f);

                //    _game.GraphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;
                _game.GraphicsDeviceManager.IsFullScreen = true;
                _game.UnhandledException += Game_UnhandledException;

                _game.Run(new GameContextUWPMixedReality(_coreWindow, holographicSpace));
            }

            private async void Game_UnhandledException(object sender, GameUnhandledExceptionEventArgs e)
            {
                // If something went wrong (i.e. GPU feature level is not enough), display an error message instead of crashing to properly pass certifications
                var exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    // Display error message
                    var messageDialog = new MessageDialog(exception.Message);
                    await messageDialog.ShowAsync();

                    // Exit application
                    CoreApplication.Exit();
                }
            }

            /// <summary>
            /// Terminate events do not cause Uninitialize to be called. It will be called if your IFrameworkView
            /// class is torn down while the app is in the foreground.
            // This method is not often used, but IFrameworkView requires it and it will be called for
            // holographic apps.
            /// </summary>
            void IFrameworkView.Uninitialize()
            {
            }

            #endregion

            #region Window event handlers

            private void OnVisibilityChanged(CoreWindow sender, VisibilityChangedEventArgs args)
            {
                _windowVisible = args.Visible;
            }

            private void OnWindowClosed(CoreWindow sender, CoreWindowEventArgs arg)
            {
                _windowClosed = true;
            }

            #endregion

        }
    }

}
