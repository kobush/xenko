// Copyright (c) Xenko contributors (https://xenko.com) 
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if  XENKO_PLATFORM_UWP

using System;
using Windows.Perception.Spatial;
using Windows.UI.Input.Spatial;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;

namespace Xenko.VirtualReality
{

    internal class WindowsMixedRealityHmd : VRDevice
    {
        private WindowsMixedRealityGraphicsPresenter presenter;
        private SpatialInteractionManager spatialInteractionManager;

        private bool canInitialize = true;
        private DeviceState deviceState = DeviceState.Valid;

        private Vector3 headPosition = Vector3.Zero;
        private Quaternion headRotation = Quaternion.Identity;
        private Vector3 headLinearVelocity = Vector3.Zero;
        private Vector3 headAngularVelocity = Vector3.Zero;

        private Size2 optimalRenderFrameSize = new Size2(2200, 2200);
        private Size2 actualRenderFrameSize;

        private WindowsMixedRealityTouchController leftHandController;
        private WindowsMixedRealityTouchController rightHandController;

        public override Size2 OptimalRenderFrameSize => optimalRenderFrameSize;

        public override Size2 ActualRenderFrameSize
        {
            get => actualRenderFrameSize;
            protected set => actualRenderFrameSize = value;
        }

        public override Texture MirrorTexture { get; protected set; }

        public override float RenderFrameScaling { get; set; } = 1f;

        public override bool CanInitialize => canInitialize;
        public override DeviceState State => deviceState;

        public override Vector3 HeadPosition => headPosition;
        public override Quaternion HeadRotation => headRotation;
        public override Vector3 HeadLinearVelocity => headLinearVelocity;
        public override Vector3 HeadAngularVelocity => headAngularVelocity;

        public override TouchController LeftHand => leftHandController;

        public override TouchController RightHand => rightHandController;

        public WindowsMixedRealityHmd()
        {
            VRApi = VRApi.WindowsMixedReality;
            SupportsOverlays = false;
        }

        public override void Enable(GraphicsDevice device, GraphicsDeviceManager graphicsDeviceManager, bool requireMirror, int mirrorWidth, int mirrorHeight)
        {
            presenter = device.Presenter as WindowsMixedRealityGraphicsPresenter;
            if (presenter == null)
                throw new InvalidOperationException($"{nameof(WindowsMixedRealityGraphicsPresenter)} is required for Windows Mixed Reality");

            if (presenter.BackBuffer != null)
            {
                optimalRenderFrameSize = new Size2(presenter.BackBuffer.Width, presenter.BackBuffer.Height);
            }

            ActualRenderFrameSize = optimalRenderFrameSize;

            if (requireMirror)
            {
                MirrorTexture = Texture.New2D(device, ActualRenderFrameSize.Width, ActualRenderFrameSize.Height, PixelFormat.R8G8B8A8_UNorm,
                    TextureFlags.RenderTarget | TextureFlags.ShaderResource);
            }

            this.spatialInteractionManager = SpatialInteractionManager.GetForCurrentView();

            leftHandController = new WindowsMixedRealityTouchController(TouchControllerHand.Left);
            rightHandController = new WindowsMixedRealityTouchController(TouchControllerHand.Right);
        }

        public override void Update(GameTime gameTime)
        {
            // update camera pose
            //TODO var sources = spatialInteractionManager.GetDetectedSourcesAtTimestamp(prediction.Timestamp);

            if (presenter.TryUpdateSpatialLocation(out var spatialLocation))
            {
                deviceState = DeviceState.Valid;

                headPosition = spatialLocation.Position.ToXenkoVector3();
                headRotation = spatialLocation.Orientation.ToXenkoQuaternion();
                headLinearVelocity = spatialLocation.AbsoluteLinearVelocity.ToXenkoVector3();
                headAngularVelocity = spatialLocation.AbsoluteAngularVelocity.ToXenkoQuaternion().YawPitchRoll;
            }
            else
            {
                deviceState = DeviceState.OutOfRange;
            }
        }

        public override void ReadEyeParameters(Eyes eye, float near, float far, 
            ref Vector3 cameraPosition, ref Matrix cameraRotation, 
            bool ignoreHeadRotation, bool ignoreHeadPosition, 
            out Matrix view, out Matrix projection)
        {
            Viewport viewport;
            if (!presenter.TryGetCameraPose(eye == Eyes.Left ? 0 : 1, near, far, out viewport, out view, out projection))
            {
                return;
            }

            /*if (ignoreHeadPosition)
            {
                view.TranslationVector = Vector3.Zero;
            }

            if (ignoreHeadRotation)
            {
                // keep the scale just in case
                view.Row1 = new Vector4(view.Row1.Length(), 0, 0, 0);
                view.Row2 = new Vector4(0, view.Row2.Length(), 0, 0);
                view.Row3 = new Vector4(0, 0, view.Row3.Length(), 0);
            }

            view = Matrix.Translation(-cameraPosition) * cameraRotation * view;*/

        }

        public override void Draw(GameTime gameTime)
        {
            /*// Clear the back buffer and depth stencil view.
            if (canGetHolographicDisplayForCamera &&
                cameraPose.HolographicCamera.Display.IsOpaque)
            {
                SharpDX.Mathematics.Interop.RawColor4 cornflowerBlue = new SharpDX.Mathematics.Interop.RawColor4(0.392156899f, 0.58431375f, 0.929411829f, 1.0f);
                context.ClearRenderTargetView(renderTargetView, cornflowerBlue);
            }
            else
            {
                SharpDX.Mathematics.Interop.RawColor4 transparent = new SharpDX.Mathematics.Interop.RawColor4(0.0f, 0.0f, 0.0f, 0.0f);
                context.ClearRenderTargetView(renderTargetView, transparent);
            }
            context.ClearDepthStencilView(
                depthStencilView,
                SharpDX.Direct3D11.DepthStencilClearFlags.Depth | SharpDX.Direct3D11.DepthStencilClearFlags.Stencil,
                1.0f,
                0);*/


        }

        public override void Commit(CommandList commandList, Texture renderFrame)
        {
        }
    }
}

#endif
