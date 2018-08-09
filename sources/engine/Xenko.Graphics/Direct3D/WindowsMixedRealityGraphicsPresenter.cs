// Copyright (c) Xenko contributors (https://xenko.com) 
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if XENKO_PLATFORM_UWP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Holographic;
using Windows.Perception.Spatial;

using SharpDX.Direct3D11;
using SharpDX.DXGI;

using Xenko.Core.Annotations;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;

namespace Xenko.Graphics
{
    public class WindowsMixedRealityGraphicsPresenter : GraphicsPresenter
    {
        internal class Native
        {
            public static Guid IInspectable = new Guid("AF86E2E0-B12D-4c6a-9C5A-D7AA65101E90");
            public static Guid ID3D11Resource = new Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d");
            public static Guid IDXGIAdapter3 = new Guid("645967A4-1392-4310-A798-8053CE3E93FD");

            [ComImport]
            [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [ComVisible(true)]
            public interface IDirect3DDxgiInterfaceAccess : IDisposable
            {
                IntPtr GetInterface([In] ref Guid iid);
            };

            [DllImport(
                "d3d11.dll",
                EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
                SetLastError = true,
                CharSet = CharSet.Unicode,
                ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall
            )]
            public static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
        }

        private IDirect3DDevice d3dInteropDevice;
        private bool d3dDeviceSupportsVprt;

        private Texture backBuffer;
        private object swapChain;

        // Cache whether or not the HolographicCamera.Display property can be accessed.
        static readonly bool canGetHolographicDisplayForCamera = false;

        // Cache whether or not the HolographicDisplay.GetDefault() method can be called.
        static readonly bool canGetDefaultHolographicDisplay = false;

        // Cache whether or not the HolographicCameraRenderingParameters.CommitDirect3D11DepthBuffer() method can be called.
        static readonly bool canCommitDirect3D11DepthBuffer = false;


        private HolographicSpace holographicSpace;
        private HolographicFrame holographicFrame;
        private SpatialLocator spatialLocator;
        private SpatialStationaryFrameOfReference stationaryReferenceFrame;

        // Back buffer resources, etc. for attached holographic cameras.
        private readonly Dictionary<uint, CameraResources> cameraResourcesDictionary = new Dictionary<uint, CameraResources>();
        private readonly object cameraResourcesLock = new object();
        private uint? currentCamera;

        static WindowsMixedRealityGraphicsPresenter()
        {
            canGetHolographicDisplayForCamera = Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Holographic.HolographicCamera", "Display");
            canGetDefaultHolographicDisplay = Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Graphics.Holographic.HolographicDisplay", "GetDefault");
            canCommitDirect3D11DepthBuffer =
                Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Graphics.Holographic.HolographicCameraRenderingParameters", "CommitDirect3D11DepthBuffer");
        }

        public WindowsMixedRealityGraphicsPresenter(GraphicsDevice device, HolographicSpace holographicSpace, PresentationParameters presentationParameters)
            : base(device, presentationParameters)
        {
            this.holographicSpace = holographicSpace;

            // Acquire the DXGI interface for the Direct3D device.
            using (var dxgiDevice = device.NativeDevice.QueryInterface<SharpDX.DXGI.Device3>())
            {
                // Wrap the native device using a WinRT interop object.
                uint hr = Native.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);
                if (hr == 0)
                {
                    d3dInteropDevice = (IDirect3DDevice)Marshal.GetObjectForIUnknown(pUnknown);
                    Marshal.Release(pUnknown);
                }

                // Store a pointer to the DXGI adapter.
                // This is for the case of no preferred DXGI adapter, or fallback to WARP.
                //dxgiAdapter = dxgiDevice.Adapter.QueryInterface<SharpDX.DXGI.Adapter3>();

            }

            // Check for device support for the optional feature that allows setting the render target array index from the vertex shader stage.
            var options = device.NativeDevice.CheckD3D113Features3();
            if (options.VPAndRTArrayIndexFromAnyShaderFeedingRasterizer)
            {
                d3dDeviceSupportsVprt = true;
            }

            holographicSpace.SetDirect3D11Device(d3dInteropDevice);

            SetHolographicSpace(holographicSpace);

        }



        protected override void ProcessPresentationParameters()
        {

        }

        public override Texture BackBuffer => this.backBuffer;

        public override object NativePresenter => null;

        public override bool IsFullScreen
        {
            get => true;
            set { }
        }

        protected override void ResizeBackBuffer(int width, int height, PixelFormat format)
        {
            if (backBuffer == null)
            {
                var backBufferDesc = new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    Format = (SharpDX.DXGI.Format)format,
                    BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                    ArraySize = 1,
                    MipLevels = 1,
                    SampleDescription = new SampleDescription(1, 0)
                };

                var backBufferNative = new Texture2D(GraphicsDevice.NativeDevice, backBufferDesc);
                backBufferNative.DebugName = "WMR_BackBuffer";
                backBuffer = SharpDXInterop.CreateTextureFromNative(GraphicsDevice, backBufferNative, true);
            }
            else
            {

                var newTextureDescrition = BackBuffer.Description;
                newTextureDescrition.Width = width;
                newTextureDescrition.Height = height;
                newTextureDescrition.Format = format;

                // Manually update back buffer texture
                BackBuffer.OnDestroyed();

                // Manually update all children textures
                var fastList = DestroyChildrenTextures(BackBuffer);

                backBuffer.InitializeFrom(newTextureDescrition);

                foreach (var texture in fastList)
                {
                    texture.InitializeFrom(BackBuffer, texture.ViewDescription);
                }
            }
        }

        protected override void ResizeDepthStencilBuffer(int width, int height, PixelFormat format)
        {
            var newTextureDescrition = DepthStencilBuffer.Description;
            newTextureDescrition.Width = width;
            newTextureDescrition.Height = height;

            // Manually update the texture
            DepthStencilBuffer.OnDestroyed();

            // Manually update all children textures
            var fastList = DestroyChildrenTextures(DepthStencilBuffer);

            // Put it in our back buffer texture
            DepthStencilBuffer.InitializeFrom(newTextureDescrition);

            foreach (var texture in fastList)
            {
                texture.InitializeFrom(DepthStencilBuffer, texture.ViewDescription);
            }
        }

        /// <summary>
        /// Calls <see cref="Texture.OnDestroyed"/> for all children of the specified texture
        /// </summary>
        /// <param name="parentTexture">Specified parent texture</param>
        /// <returns>A list of the children textures which were destroyed</returns>
        private FastList<Texture> DestroyChildrenTextures(Texture parentTexture)
        {
            var fastList = new FastList<Texture>();
            foreach (var resource in GraphicsDevice.Resources)
            {
                var texture = resource as Texture;
                if (texture != null && texture.ParentTexture == parentTexture)
                {
                    texture.OnDestroyed();
                    fastList.Add(texture);
                }
            }

            return fastList;
        }

        public override void BeginDraw(CommandList commandList)
        {
            // Before doing the timer update, there is some work to do per-frame
            // to maintain holographic rendering. First, we will get information
            // about the current frame.

            // The HolographicFrame has information that the app needs in order
            // to update and render the current frame. The app begins each new
            // frame by calling CreateNextFrame.
            holographicFrame = holographicSpace.CreateNextFrame();

            // Get a prediction of where holographic cameras will be when this frame
            // is presented.
            var prediction = holographicFrame.CurrentPrediction;

            // Back buffers can change from frame to frame. Validate each buffer, and recreate
            // resource views and depth buffers as needed.
            EnsureCameraResources(holographicFrame, prediction);
        }

        public bool TryUpdateSpatialLocation(out SpatialLocation location)
        {
            if (stationaryReferenceFrame == null || spatialLocator == null)
            {
                Debug.WriteLine("spatialLocator not ready");

                location = null;
                return false;
            }

            // Up-to-date frame predictions enhance the effectiveness of image stablization and
            // allow more accurate positioning of holograms.
            holographicFrame.UpdateCurrentPrediction();

            // Get a prediction of where holographic cameras will be when this frame
            // is presented.
            var prediction = holographicFrame.CurrentPrediction;

            // Check for new input state since the last frame.
            //var pointerPose = SpatialPointerPose.TryGetAtTimestamp(stationaryReferenceFrame.CoordinateSystem, prediction.Timestamp);

            //SpatialInteractionSourceState pointerState = spatialInputHandler.CheckForInput();
            // When a Pressed gesture is detected, the sample hologram will be repositioned
            // two meters in front of the user.
            // var pointerPose = pointerState.TryGetPointerPose(currentCoordinateSystem);

            location = spatialLocator.TryLocateAtTimestamp(prediction.Timestamp, stationaryReferenceFrame.CoordinateSystem);
            if (location == null)
            {
                Debug.WriteLine("Can't update spatial location.");
            }
            return location != null;
        }

        internal bool TryGetCameraPose(int eye, float near, float far, out Viewport viewport, out Matrix cameraView, out Matrix cameraProjection)
        {
            viewport = Viewport.Empty;
            cameraView = Matrix.Zero;
            cameraProjection = Matrix.Zero;

            if (stationaryReferenceFrame == null)
                return false;

            var prediction = holographicFrame.CurrentPrediction;

            foreach (var cameraPose in prediction.CameraPoses)
            {
                if (cameraPose.HolographicCamera.Id != currentCamera)
                    continue;

                /* TODO:
                if (cameraPose.NearPlaneDistance != near)
                    cameraPose.HolographicCamera.SetNearPlaneDistance(near); 
                 
                if (cameraPose.FarPlaneDistance != far)
                    cameraPose.HolographicCamera.SetFarPlaneDistance(far);
                 */

                // The system changes the viewport on a per-frame basis for system optimizations.
                viewport = new Viewport(
                    (float)cameraPose.Viewport.Left,
                    (float)cameraPose.Viewport.Top,
                    (float)cameraPose.Viewport.Width,
                    (float)cameraPose.Viewport.Height);

                // The projection transform for each frame is provided by the HolographicCameraPose.
                HolographicStereoTransform cameraProjectionTransform = cameraPose.ProjectionTransform;
                cameraProjection = eye == 0 ? cameraProjectionTransform.Left.ToXenkoMatrix() : cameraProjectionTransform.Right.ToXenkoMatrix();

                // Get a container object with the view and projection matrices for the given
                // pose in the given coordinate system.
                HolographicStereoTransform? viewTransformContainer = cameraPose.TryGetViewTransform(stationaryReferenceFrame.CoordinateSystem);

                // If TryGetViewTransform returns null, that means the pose and coordinate system
                // cannot be understood relative to one another; content cannot be rendered in this 
                // coordinate system for the duration of the current frame.
                // This usually means that positional tracking is not active for the current frame, in
                // which case it is possible to use a SpatialLocatorAttachedFrameOfReference to render
                // content that is not world-locked instead.

                var viewTransformAcquired = viewTransformContainer.HasValue;
                if (!viewTransformAcquired)
                {
                    Debug.WriteLine("TryGetViewTransform returns null for eye "+ eye);
                    return false;
                }
                
                // Otherwise, the set of view transforms can be retrieved.
                HolographicStereoTransform viewCoordinateSystemTransform = viewTransformContainer.Value;

                // Update the view matrices. Holographic cameras (such as Microsoft HoloLens) are
                // constantly moving relative to the world. The view matrices need to be updated
                // every frame.
                cameraView = eye == 0 ? viewCoordinateSystemTransform.Left.ToXenkoMatrix() : viewCoordinateSystemTransform.Right.ToXenkoMatrix();

                /*coordinateSystem = AttachedReferenceFrame.GetStationaryCoordinateSystemAtTimestamp(prediction.Timestamp);
                    viewTransformContainer = cameraPose.TryGetViewTransform(coordinateSystem);
                    if (viewTransformContainer.HasValue)
                    {
                        SetTransform(viewTransformContainer.Value, AttachedView);
                    }*/

                /*CullingFrustum = cameraPose.TryGetCullingFrustum(coordinateSystem);

                    VisibleFrustum = cameraPose.TryGetVisibleFrustum(coordinateSystem);*/

                return true;
            }

            return false;
        }


        public override void EndDraw(CommandList commandList, bool present)
        {
            if (currentCamera == null || backBuffer == null)
                return;

            UseHolographicCameraResources(cameraResourcesDictionary =>
            {
                var cameraResources = cameraResourcesDictionary[currentCamera.Value];

                var width = (int)cameraResources.RenderTargetSize.Width;
                var height = (int)cameraResources.RenderTargetSize.Height;

                var context = GraphicsDevice.NativeDeviceContext;

                context.CopySubresourceRegion(BackBuffer.NativeResource, 0,
                    new SharpDX.Direct3D11.ResourceRegion(0, 0, 0, width, height, 1),
                    cameraResources.BackBufferTexture2D, 0);

                context.CopySubresourceRegion(BackBuffer.NativeResource, 0,
                    new SharpDX.Direct3D11.ResourceRegion(width, 0, 0, width * 2, height, 1),
                    cameraResources.BackBufferTexture2D, 1);

                //todo: CommitDirect3D11DepthBuffer
            });

        }

        public override void Present()
        {
            // By default, this API waits for the frame to finish before it returns.
            // Holographic apps should wait for the previous frame to finish before 
            // starting work on a new frame. This allows for better results from
            // holographic frame predictions.
            var presentResult = this.holographicFrame.PresentUsingCurrentPrediction(
                HolographicFramePresentWaitBehavior.WaitForFrameToFinish
            );

            // The PresentUsingCurrentPrediction API will detect when the graphics device
            // changes or becomes invalid. When this happens, it is considered a Direct3D
            // device lost scenario.
            if (presentResult == HolographicFramePresentResult.DeviceRemoved)
            {
                // The Direct3D device, context, and resources should be recreated.
                HandleDeviceLost();
            }
        }

        private void HandleDeviceLost()
        {
            UseHolographicCameraResources(cameraResourcesDictionary =>
            {
                foreach (var cameraResources in cameraResourcesDictionary.Values)
                {
                    cameraResources.ReleaseAllDeviceResources(GraphicsDevice);
                }
            });

            //TODO InitializeUsingHolographicSpace();

            //DeviceRestored.Invoke(this, null);

        }

        private void SetHolographicSpace(HolographicSpace holographicSpace)
        {
            if (canGetDefaultHolographicDisplay)
            {
                // Subscribe for notifications about changes to the state of the default HolographicDisplay 
                // and its SpatialLocator.
                HolographicSpace.IsAvailableChanged += this.OnHolographicDisplayIsAvailableChanged;
            }

            // Acquire the current state of the default HolographicDisplay and its SpatialLocator.
            OnHolographicDisplayIsAvailableChanged(null, null);

            // Respond to camera added events by creating any resources that are specific
            // to that camera, such as the back buffer render target view.
            // When we add an event handler for CameraAdded, the API layer will avoid putting
            // the new camera in new HolographicFrames until we complete the deferral we created
            // for that handler, or return from the handler without creating a deferral. This
            // allows the app to take more than one frame to finish creating resources and
            // loading assets for the new holographic camera.
            // This function should be registered before the app creates any HolographicFrames.
            holographicSpace.CameraAdded += this.OnCameraAdded;

            // Respond to camera removed events by releasing resources that were created for that
            // camera.
            // When the app receives a CameraRemoved event, it releases all references to the back
            // buffer right away. This includes render target views, Direct2D target bitmaps, and so on.
            // The app must also ensure that the back buffer is not attached as a render target, as
            // shown in DeviceResources.ReleaseResourcesForBackBuffer.
            holographicSpace.CameraRemoved += this.OnCameraRemoved;

            // Notes on spatial tracking APIs:
            // * Stationary reference frames are designed to provide a best-fit position relative to the
            //   overall space. Individual positions within that reference frame are allowed to drift slightly
            //   as the device learns more about the environment.
            // * When precise placement of individual holograms is required, a SpatialAnchor should be used to
            //   anchor the individual hologram to a position in the real world - for example, a point the user
            //   indicates to be of special interest. Anchor positions do not drift, but can be corrected; the
            //   anchor will use the corrected position starting in the next frame after the correction has
            //   occurred.
        }

        void OnHolographicDisplayIsAvailableChanged(object o, object args)
        {
            // Get the spatial locator for the default HolographicDisplay, if one is available.
            SpatialLocator spatialLocator = null;
            if (canGetDefaultHolographicDisplay)
            {
                HolographicDisplay defaultHolographicDisplay = HolographicDisplay.GetDefault();
                if (defaultHolographicDisplay != null)
                {
                    Debug.WriteLine($"[HolographicDisplay] {defaultHolographicDisplay.DisplayName}\n"+
                                    $"isStereo: {defaultHolographicDisplay.IsStereo}\n" +
                                    $"isOpaque: {defaultHolographicDisplay.IsOpaque}\n" +
                                    $"refreshRate: {defaultHolographicDisplay.RefreshRate}\n" +
                                    $"maxViewportSize: {defaultHolographicDisplay.MaxViewportSize}");
 
                    spatialLocator = defaultHolographicDisplay.SpatialLocator;
                }
            }
            else
            {
                spatialLocator = SpatialLocator.GetDefault();
            }

            if (this.spatialLocator != spatialLocator)
            {
                // If the spatial locator is disconnected or replaced, we should discard any state that was
                // based on it.
                if (this.spatialLocator != null)
                {
                    this.spatialLocator.LocatabilityChanged -= this.OnLocatabilityChanged;
                    this.spatialLocator = null;
                }

                this.stationaryReferenceFrame = null;

                if (spatialLocator != null)
                {
                    // Use the SpatialLocator from the default HolographicDisplay to track the motion of the device.
                    this.spatialLocator = spatialLocator;

                    // Respond to changes in the positional tracking state.
                    this.spatialLocator.LocatabilityChanged += this.OnLocatabilityChanged;

                    // The simplest way to render world-locked holograms is to create a stationary reference frame
                    // based on a SpatialLocator. This is roughly analogous to creating a "world" coordinate system
                    // with the origin placed at the device's position as the app is launched.
                    this.stationaryReferenceFrame = this.spatialLocator.CreateStationaryFrameOfReferenceAtCurrentLocation();
                }
            }
        }

        void OnLocatabilityChanged(SpatialLocator sender, Object args)
        {
            switch (sender.Locatability)
            {
                case SpatialLocatability.Unavailable:
                    // Holograms cannot be rendered.
                    String message = "Warning! Positional tracking is " + sender.Locatability + ".";
                    Debug.WriteLine(message);
                    break;

                // In the following three cases, it is still possible to place holograms using a
                // SpatialLocatorAttachedFrameOfReference.
                case SpatialLocatability.PositionalTrackingActivating:
                    // The system is preparing to use positional tracking.
                    break;

                case SpatialLocatability.OrientationOnly:
                    // Positional tracking has not been activated.
                    break;

                case SpatialLocatability.PositionalTrackingInhibited:
                    // Positional tracking is temporarily inhibited. User action may be required
                    // in order to restore positional tracking.
                    break;

                case SpatialLocatability.PositionalTrackingActive:
                    // Positional tracking is active. World-locked content can be rendered.
                    break;
            }
        }

        public void OnCameraAdded(
            HolographicSpace sender,
            HolographicSpaceCameraAddedEventArgs args
        )
        {
            Deferral deferral = args.GetDeferral();
            HolographicCamera holographicCamera = args.Camera;

            Debug.WriteLine($"[HolographicCameraAdded] {holographicCamera.Id}\n"+
                            $"viewportScaleFactor: {holographicCamera.ViewportScaleFactor}\n" +
                            $"renderTargetSize: {holographicCamera.RenderTargetSize}\n" +
                            $"maxQuadLayers: {holographicCamera.MaxQuadLayerCount}\n" +
                            $"isStereo: {holographicCamera.IsStereo}");

            Task task1 = new Task(() =>
            {
                //
                // TODO: Allocate resources for the new camera and load any content specific to
                //       that camera. Note that the render target size (in pixels) is a property
                //       of the HolographicCamera object, and can be used to create off-screen
                //       render targets that match the resolution of the HolographicCamera.
                //

                // Create device-based resources for the holographic camera and add it to the list of
                // cameras used for updates and rendering. Notes:
                //   * Since this function may be called at any time, the AddHolographicCamera function
                //     waits until it can get a lock on the set of holographic camera resources before
                //     adding the new camera. At 60 frames per second this wait should not take long.
                //   * A subsequent Update will take the back buffer from the RenderingParameters of this
                //     camera's CameraPose and use it to create the ID3D11RenderTargetView for this camera.
                //     Content can then be rendered for the HolographicCamera.
                AddHolographicCamera(holographicCamera);

                // Holographic frame predictions will not include any information about this camera until
                // the deferral is completed.
                deferral.Complete();
            });
            task1.Start();
        }

        public void OnCameraRemoved(
            HolographicSpace sender,
            HolographicSpaceCameraRemovedEventArgs args
        )
        {
            HolographicCamera holographicCamera = args.Camera;

            Task task2 = new Task(() =>
            {
                //
                // TODO: Asynchronously unload or deactivate content resources (not back buffer 
                //       resources) that are specific only to the camera that was removed.
                //
            });
            task2.Start();

            Debug.WriteLine("Holographic camera removed " + holographicCamera.Id);

            // Before letting this callback return, ensure that all references to the back buffer 
            // are released.
            // Since this function may be called at any time, the RemoveHolographicCamera function
            // waits until it can get a lock on the set of holographic camera resources before
            // deallocating resources for this camera. At 60 frames per second this wait should
            // not take long.
            RemoveHolographicCamera(holographicCamera);
        }

        /// <summary>
        /// Validates the back buffer for each HolographicCamera and recreates
        /// resources for back buffers that have changed.
        /// Locks the set of holographic camera resources until the function exits.
        /// </summary>
        public void EnsureCameraResources(HolographicFrame frame, HolographicFramePrediction prediction)
        {
            UseHolographicCameraResources(cameraResourcesDictionary =>
            {
                foreach (var pose in prediction.CameraPoses)
                {
                    var renderingParameters = frame.GetRenderingParameters(pose);
                    var cameraResources = cameraResourcesDictionary[pose.HolographicCamera.Id];

                    cameraResources.CreateResourcesForBackBuffer(GraphicsDevice, renderingParameters);

                    if (pose.HolographicCamera.Id == currentCamera)
                    {
                        int width = (int)(cameraResources.RenderTargetSize.Width * 2);
                        int height = (int)cameraResources.RenderTargetSize.Height;
                        if (BackBuffer == null ||
                            BackBuffer.Width != width || BackBuffer.Height != height)
                        {
                            Resize(width, height, (PixelFormat)cameraResources.BackBufferDxgiFormat);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Prepares to allocate resources and adds resource views for a camera.
        /// Locks the set of holographic camera resources until the function exits.
        /// </summary>
        public void AddHolographicCamera(HolographicCamera camera)
        {
            UseHolographicCameraResources(cameraResourcesDictionary =>
            {
                cameraResourcesDictionary.Add(camera.Id, new CameraResources(camera));

                // TODO: for now always uses first camera
                if (currentCamera == null)
                    currentCamera = camera.Id;
            });
        }


        // Deallocates resources for a camera and removes the camera from the set.
        // Locks the set of holographic camera resources until the function exits.
        private void RemoveHolographicCamera(HolographicCamera camera)
        {
            UseHolographicCameraResources(cameraResourcesDictionary =>
            {
                CameraResources cameraResources = cameraResourcesDictionary[camera.Id];

                if (null != cameraResources)
                {
                    cameraResources.ReleaseResourcesForBackBuffer(this.GraphicsDevice);
                    cameraResourcesDictionary.Remove(camera.Id);
                }
            });
        }


        private void UseHolographicCameraResources([NotNull] Action<Dictionary<uint, CameraResources>> callback)
        {
            lock (cameraResourcesLock)
            {
                callback(cameraResourcesDictionary);
            }
        }

        internal class CameraResources 
        {
            private readonly HolographicCamera holographicCamera;

            #region Fields

            // Direct3D rendering objects. Required for 3D.
            private SharpDX.Direct3D11.RenderTargetView renderTargetView;
            private SharpDX.Direct3D11.DepthStencilView depthStencilView;
            private SharpDX.Direct3D11.Texture2D depthBuffer;
            private SharpDX.Direct3D11.Texture2D backBuffer;

            // Direct3D rendering properties.
            private SharpDX.DXGI.Format dxgiFormat;
            private Size renderTargetSize;

            // Indicates whether the camera supports stereoscopic rendering.
            private readonly bool isStereo;

            // Indicates whether this camera has a pending frame.
            bool framePending = false;

            #endregion


            #region Properties

            public RenderTargetView BackBufferRenderTargetView => renderTargetView;

            public DepthStencilView DepthStencilView => depthStencilView;

            public SharpDX.Direct3D11.Texture2D BackBufferTexture2D => backBuffer;

            // Render target properties.

            public SharpDX.DXGI.Format BackBufferDxgiFormat => dxgiFormat;

            public Size RenderTargetSize => renderTargetSize;

            public bool IsRenderingStereoscopic => isStereo;


            #endregion

            public CameraResources(HolographicCamera holographicCamera)
            {
                this.holographicCamera = holographicCamera;

                isStereo = holographicCamera.IsStereo;
                renderTargetSize = holographicCamera.RenderTargetSize;
            }

            /// <summary>
            /// Updates resources associated with a holographic camera's swap chain.
            /// The app does not access the swap chain directly, but it does create
            /// resource views for the back buffer.
            /// </summary>
            public void CreateResourcesForBackBuffer(
                GraphicsDevice graphicsDevice,
                HolographicCameraRenderingParameters cameraParameters)
            {
                var device = graphicsDevice.NativeDevice;

                // Get the WinRT object representing the holographic camera's back buffer.
                IDirect3DSurface surface = cameraParameters.Direct3D11BackBuffer;

                // Get a DXGI interface for the holographic camera's back buffer.
                // Holographic cameras do not provide the DXGI swap chain, which is owned
                // by the system. The Direct3D back buffer resource is provided using WinRT
                // interop APIs.
                WindowsMixedRealityGraphicsPresenter.Native.IDirect3DDxgiInterfaceAccess surfaceDxgiInterfaceAccess =
                    surface as WindowsMixedRealityGraphicsPresenter.Native.IDirect3DDxgiInterfaceAccess;
                IntPtr pResource = surfaceDxgiInterfaceAccess.GetInterface(WindowsMixedRealityGraphicsPresenter.Native.ID3D11Resource);

                // Determine if the back buffer has changed. If so, ensure that the render target view
                // is for the current back buffer.
                if (backBuffer == null || backBuffer.NativePointer != pResource)
                {
                    Debug.WriteLine("Recreating back buffer for holo camera " + holographicCamera.Id);

                    // Clean up references to previous resources.
                    backBuffer?.Dispose();
                    renderTargetView?.Dispose();

                    // This can change every frame as the system moves to the next buffer in the
                    // swap chain. This mode of operation will occur when certain rendering modes
                    // are activated.
                    backBuffer = new SharpDX.Direct3D11.Texture2D(pResource);

                    // Create a render target view of the back buffer.
                    // Creating this resource is inexpensive, and is better than keeping track of
                    // the back buffers in order to pre-allocate render target views for each one.
                    renderTargetView = new RenderTargetView(device, backBuffer);

                    // Get the DXGI format for the back buffer.
                    // This information can be accessed by the app using CameraResources::GetBackBufferDXGIFormat().
                    Texture2DDescription backBufferDesc = backBuffer.Description;
                    dxgiFormat = backBufferDesc.Format;

                    // Check for render target size changes.
                    Size currentSize = holographicCamera.RenderTargetSize;
                    if (renderTargetSize != currentSize)
                    {
                        Debug.WriteLine("Render target size changed to " + currentSize);

                        // Set render target size.
                        renderTargetSize = holographicCamera.RenderTargetSize;

                        // A new depth stencil view is also needed.
                        depthStencilView.Dispose();
                        depthStencilView = null;
                    }


                    /*RenderTargetView l_target_view = new RenderTargetView(device, backBuffer, new RenderTargetViewDescription()
                    {
                        Format = (SharpDX.DXGI.Format)backBuffer.Description.Format,
                        Dimension = RenderTargetViewDimension.Texture2D,
                        Texture2DArray = new RenderTargetViewDescription.Texture2DArrayResource()
                        {
                            ArraySize = 1,
                            FirstArraySlice = 0
                        }
                    });
    
                    RenderTargetView r_target_view = new RenderTargetView(device, backBuffer, new RenderTargetViewDescription()
                    {
                        Format = (SharpDX.DXGI.Format)backBuffer.Description.Format,
                        Dimension = RenderTargetViewDimension.Texture2D,
                        Texture2DArray = new RenderTargetViewDescription.Texture2DArrayResource()
                        {
                            ArraySize = 1,
                            FirstArraySlice = 1
                        }
                    });*/
                }

                Marshal.Release(pResource);

                // Refresh depth stencil resources, if needed.
                if (depthStencilView == null)
                {
                    Debug.WriteLine("Recreating depth buffer for holo camera " + holographicCamera.Id);

                    // Create a depth stencil view for use with 3D rendering if needed.
                    var depthStencilDesc = new Texture2DDescription
                    {
                        Format = SharpDX.DXGI.Format.D16_UNorm,
                        Width = (int)RenderTargetSize.Width,
                        Height = (int)RenderTargetSize.Height,
                        ArraySize = IsRenderingStereoscopic ? 2 : 1, // Create two textures when rendering in stereo.
                        MipLevels = 1, // Use a single mipmap level.
                        BindFlags = BindFlags.DepthStencil,
                        SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0)
                    };

                    depthBuffer = new Texture2D(device, depthStencilDesc);
                    if (depthBuffer != null)
                    {
                        var depthStencilViewDesc = new DepthStencilViewDescription();
                        depthStencilViewDesc.Dimension = IsRenderingStereoscopic ? DepthStencilViewDimension.Texture2DArray : DepthStencilViewDimension.Texture2D;
                        depthStencilViewDesc.Texture2DArray.ArraySize = IsRenderingStereoscopic ? 2 : 0;
                        depthStencilViewDesc.Format = SharpDX.DXGI.Format.D16_UNorm;
                        depthStencilView = new DepthStencilView(device, depthBuffer, depthStencilViewDesc);
                    }
                }
            }

            /// <summary>
            /// Releases resources associated with a holographic display back buffer.
            /// </summary>
            public void ReleaseResourcesForBackBuffer(GraphicsDevice graphicsDevice)
            {
                var context = graphicsDevice.NativeDeviceContext;

                backBuffer.Dispose();
                backBuffer = null;

                depthBuffer.Dispose();
                depthBuffer = null;

                renderTargetView.Dispose();
                renderTargetView = null;

                depthStencilView.Dispose();
                depthStencilView = null;

                const int D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT = 8;
                RenderTargetView[] nullViews = new RenderTargetView[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT];

                // Ensure system references to the back buffer are released by clearing the render
                // target from the graphics pipeline state, and then flushing the Direct3D context.
                context.OutputMerger.SetRenderTargets(null, nullViews);
                context.Flush();
            }

            public void ReleaseAllDeviceResources(GraphicsDevice graphicsDevice)
            {
                ReleaseResourcesForBackBuffer(graphicsDevice);
            }
        }
    }
}

#endif
