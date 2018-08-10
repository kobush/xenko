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
    public class WindowsMixedRealityTouchController : TouchController
    {
        private readonly SpatialInteractionSourceHandedness hand;

        private Vector3 currentPosition;
        private Quaternion currentRotation = Quaternion.Identity;
        private Vector3 currentLinearVelocity;
        private Vector3 currentAngularVelocity;

        private SpatialInteractionSourceState currentState;
        private DeviceState internalState;
        private SpatialInteractionSourceState previousState;

        public WindowsMixedRealityTouchController(TouchControllerHand hand)
        {
            this.hand = hand == TouchControllerHand.Left ? SpatialInteractionSourceHandedness.Left : SpatialInteractionSourceHandedness.Right;

        }

        public override Vector3 Position => currentPosition;
        public override Quaternion Rotation => currentRotation;
        public override Vector3 LinearVelocity => currentLinearVelocity;
        public override Vector3 AngularVelocity => currentAngularVelocity;

        public override DeviceState State => internalState;

        public override float Trigger => (float)currentState.SelectPressedValue;

        public override float Grip => currentState.IsGrasped ? 1.0f : 0.0f;

        public override bool IndexPointing => false;

        public override bool IndexResting => true;

        public override bool ThumbUp => !currentState.ControllerProperties.IsTouchpadTouched;

        public override bool ThumbResting => currentState.ControllerProperties.IsTouchpadTouched;

        public override Vector2 ThumbAxis => new Vector2((float)currentState.ControllerProperties.TouchpadX, (float)currentState.ControllerProperties.TouchpadY);

        public override Vector2 ThumbstickAxis => new Vector2((float)currentState.ControllerProperties.ThumbstickX, (float)currentState.ControllerProperties.ThumbstickY);

        public override bool IsPressed(TouchControllerButton button) => IsButtonPressed(button, currentState);

        public override bool IsPressedDown(TouchControllerButton button) => !IsButtonPressed(button, previousState) && IsButtonPressed(button, currentState);

        public override bool IsPressReleased(TouchControllerButton button) => IsButtonPressed(button, previousState) && !IsButtonPressed(button, currentState);

        public override bool IsTouched(TouchControllerButton button) => !IsButtonTouched(button, currentState);

        public override bool IsTouchedDown(TouchControllerButton button) => !IsButtonTouched(button, previousState) && IsButtonTouched(button, currentState);

        public override bool IsTouchReleased(TouchControllerButton button) => IsButtonTouched(button, previousState) && !IsButtonTouched(button, currentState);

        public void Update(GameTime gameTime, SpatialInteractionSourceState sourceState, SpatialCoordinateSystem coordinateSystem)
        {
            SpatialInteractionSourceLocation location = sourceState.Properties.TryGetLocation(coordinateSystem);

            if (location != null)
            {
                SetSpatialInteractionSourceLocation(location);
            }

            previousState = currentState;
            currentState = sourceState;

            internalState = previousState != null ? DeviceState.Valid : DeviceState.Invalid;
        }

        private bool IsButtonPressed(TouchControllerButton button, SpatialInteractionSourceState state)
        {
            switch (button)
            {
                case TouchControllerButton.Thumbstick:
                    return state.ControllerProperties.IsThumbstickPressed;
                case TouchControllerButton.Touchpad:
                    return state.ControllerProperties.IsTouchpadPressed;
                case TouchControllerButton.A when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Right:
                    return ThumbAxis.X >= 0.0f;
                case TouchControllerButton.B when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Right:
                    return ThumbAxis.X < 0.0f;
                case TouchControllerButton.X when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Left:
                    return ThumbAxis.X < 0.0f;
                case TouchControllerButton.Y when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Left:
                    return ThumbAxis.X >= 0.0f;
                case TouchControllerButton.Trigger:
                    return state.IsSelectPressed;
                case TouchControllerButton.Grip:
                    return state.IsGrasped;
                case TouchControllerButton.Menu:
                    return state.IsMenuPressed;
                default:
                    return false;
            }
        }

        private bool IsButtonTouched(TouchControllerButton button, SpatialInteractionSourceState state)
        {
            switch (button)
            {
                case TouchControllerButton.Touchpad:
                    return state.ControllerProperties.IsTouchpadTouched;
                case TouchControllerButton.A when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Right:
                    return ThumbAxis.X >= 0.0f;
                case TouchControllerButton.B when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Right:
                    return ThumbAxis.X < 0.0f;
                case TouchControllerButton.X when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Left:
                    return ThumbAxis.X < 0.0f;
                case TouchControllerButton.Y when state.ControllerProperties.IsTouchpadPressed && hand == SpatialInteractionSourceHandedness.Left:
                    return ThumbAxis.X >= 0.0f;
                default:
                    return false;
            }
        }

        private void SetSpatialInteractionSourceLocation(SpatialInteractionSourceLocation location)
        {
            currentPosition = location.Position?.ToXenkoVector3() ?? currentPosition;
            currentRotation = location.Orientation?.ToXenkoQuaternion() ?? currentRotation;
            currentLinearVelocity = location.Velocity?.ToXenkoVector3() ?? currentLinearVelocity;
            currentAngularVelocity = location.AngularVelocity?.ToXenkoVector3() ?? currentAngularVelocity;
        }
    }
}

#endif
