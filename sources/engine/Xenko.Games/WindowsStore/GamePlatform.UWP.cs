// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
//
// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
#if XENKO_PLATFORM_UWP
using System;
using System.Collections.Generic;

using Xenko.Graphics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;

namespace Xenko.Games
{
    internal class GamePlatformUWP : GamePlatform
    {
        public GamePlatformUWP(GameBase game) : base(game)
        {
            // Application lifecycle reference:
            // https://docs.microsoft.com/en-us/windows/uwp/launch-resume/app-lifecycle
            // https://docs.microsoft.com/en-us/uwp/api/windows.applicationmodel.core.coreapplication

            CoreApplication.Suspending += CurrentOnSuspending;
            CoreApplication.Resuming += CurrentOnResuming;
        }

        private void CurrentOnResuming(object sender, object o)
        {
            OnResume(sender, null);
        }

        private void CurrentOnSuspending(object sender, SuspendingEventArgs suspendingEventArgs)
        {
            var deferral = suspendingEventArgs.SuspendingOperation.GetDeferral();

            using (var device3 = game.GraphicsDevice.NativeDevice.QueryInterface<SharpDX.DXGI.Device3>())
            {
                game.GraphicsContext.CommandList.ClearState();
                device3.Trim();
            }

            OnSuspend(sender, null);

            deferral.Complete();
        }

        public override string DefaultAppDirectory
        {
            get
            {
                return Package.Current.InstalledLocation.Path;
            }
        }

        internal override GameWindow GetSupportedGameWindow(AppContextType type)
        {
            if (type == AppContextType.UWPCoreWindow || type == AppContextType.UWPXaml)
            {
                return new GameWindowUWP();
            }
            else if (type == AppContextType.UWPMixedReality)
            {
                return new GameWindowUWPMixedReality();
            }
            else
            {
                return null;
            }
        }

        public override GraphicsDevice CreateDevice(GraphicsDeviceInformation deviceInformation)
        {
            if (gameWindow is GameWindowUWPMixedReality mixedRealityGameWindow)
            {
                //TODO: move this to FindBestDevices
                // When a primary adapter ID is given to the app, the app should find
                // the corresponding DXGI adapter and use it to create Direct3D devices
                // and device contexts. Otherwise, there is no restriction on the DXGI
                // adapter the app can use.
                var id = mixedRealityGameWindow.GetPreferredAdapterId();
                if (id != 0)
                {
                    var adapterUid = id.ToString();
                    foreach (var adapter in GraphicsAdapterFactory.Adapters)
                    {
                        if (adapter.AdapterUid == adapterUid)
                        {
                            deviceInformation.Adapter = adapter;
                            break;
                        }
                    }
                }

                // required
                deviceInformation.DeviceCreationFlags |= DeviceCreationFlags.BgraSupport;

                var graphicsDevice = GraphicsDevice.New(deviceInformation.Adapter, deviceInformation.DeviceCreationFlags,
                    gameWindow.NativeWindow, deviceInformation.GraphicsProfile);

                graphicsDevice.ColorSpace = deviceInformation.PresentationParameters.ColorSpace;
                graphicsDevice.Presenter = new MixedRealityGraphicsPresenter(graphicsDevice, mixedRealityGameWindow.HolographicSpace,
                    deviceInformation.PresentationParameters);

                return graphicsDevice;
            }
            else
            {
                return base.CreateDevice(deviceInformation);
            }
        }

        public override List<GraphicsDeviceInformation> FindBestDevices(GameGraphicsParameters preferredParameters)
        {
            var graphicsDeviceInfos = base.FindBestDevices(preferredParameters);

            // Special case where the default FindBestDevices is not working
            if (graphicsDeviceInfos.Count == 0)
            {
                var graphicsAdapter = GraphicsAdapterFactory.Adapters[0];

                // Iterate on each preferred graphics profile
                foreach (var featureLevel in preferredParameters.PreferredGraphicsProfile)
                {
                    // Check if this profile is supported.
                    if (graphicsAdapter.IsProfileSupported(featureLevel))
                    {
                        var deviceInfo = new GraphicsDeviceInformation
                                             {
                                                 Adapter = graphicsAdapter,
                                                 GraphicsProfile = featureLevel,
                                                 PresentationParameters =
                                                     {
                                                         MultisampleCount = preferredParameters.PreferredMultisampleCount,
                                                         IsFullScreen = preferredParameters.IsFullScreen,
                                                         PresentationInterval = preferredParameters.SynchronizeWithVerticalRetrace ? PresentInterval.One : PresentInterval.Immediate,
                                                         DeviceWindowHandle =  MainWindow.NativeWindow,
                                                     }
                                             };

                        // Hardcoded format and refresh rate...
                        // This is a workaround to allow this code to work inside the emulator
                        // but this is not really robust
                        // TODO: Check how to handle this case properly
                        var displayMode = new DisplayMode(PixelFormat.B8G8R8A8_UNorm, gameWindow.ClientBounds.Width, gameWindow.ClientBounds.Height, new Rational(60, 1));
                        AddDevice(displayMode, deviceInfo, preferredParameters, graphicsDeviceInfos);

                        // If the profile is supported, we are just using the first best one
                        break;
                    }
                }
            }

            return graphicsDeviceInfos;
        }
    }
}
#endif
