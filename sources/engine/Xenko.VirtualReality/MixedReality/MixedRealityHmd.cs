#if  XENKO_PLATFORM_UWP

using System;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;

namespace Xenko.VirtualReality.WMR
{
    public class MixedRealityHmd : VRDevice
    {
        private MixedRealityGraphicsPresenter presenter;

        private bool canInitialize = true;
        private DeviceState deviceState = DeviceState.Valid;

        private Vector3 headPosition = Vector3.Zero;
        private Quaternion headRotation = Quaternion.Identity;
        private Vector3 headLinearVelocity = Vector3.Zero;
        private Vector3 headAngularVelocity = Vector3.Zero;

        private Size2 optimalRenderFrameSize = new Size2(2200, 2200);
        private Size2 actualRenderFrameSize;

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

        public override TouchController LeftHand { get; }
        public override TouchController RightHand { get; }

        public MixedRealityHmd()
        {
            VRApi = VRApi.WindowsMixedReality;
            SupportsOverlays = false;
        }

        public override void Enable(GraphicsDevice device, GraphicsDeviceManager graphicsDeviceManager, bool requireMirror, int mirrorWidth, int mirrorHeight)
        {
            presenter = device.Presenter as MixedRealityGraphicsPresenter;
            if (presenter == null)
                throw new InvalidOperationException($"{nameof(MixedRealityGraphicsPresenter)} is required for Windows Mixed Reality");

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

            //TODO: create touch controlers
        }

        public override void Commit(CommandList commandList, Texture renderFrame)
        {
        }

        public override void Update(GameTime gameTime)
        {
            //nothing needed
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


            headPosition = presenter.HeadPosition;
            headRotation = Quaternion.RotationMatrix(Matrix.LookAtRH(presenter.HeadPosition, presenter.HeadPosition + presenter.HeadDirection, presenter.HeadUpDirection));
        }

        public override void ReadEyeParameters(Eyes eye, float near, float far, 
            ref Vector3 cameraPosition, ref Matrix cameraRotation, 
            bool ignoreHeadRotation, bool ignoreHeadPosition, 
            out Matrix view, out Matrix projection)
        {
            presenter.GetCameraPose(eye == Eyes.Left ? 0 : 1, out view, out projection);

            return;

            view.Decompose(out var eyeScale, out Quaternion eyeRotation, out var eyePosition);

            //TODO: eyeScale

            var camRot = Quaternion.RotationMatrix(cameraRotation);

            var position = cameraPosition + Vector3.Transform(eyePosition * ViewScaling, camRot);
            var rotation = Matrix.RotationQuaternion(eyeRotation) * Matrix.Scaling(ViewScaling) * Matrix.RotationQuaternion(camRot);
            var finalUp = Vector3.TransformCoordinate(Vector3.UnitY, rotation);
            var finalForward = Vector3.TransformCoordinate(-Vector3.UnitZ, rotation);
            view = Matrix.LookAtRH(position, position + finalForward, finalUp);
        }
    }
}

#endif
