// Copyright (c) Xenko contributors (https://xenko.com) 
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if  XENKO_PLATFORM_UWP

using System;
using Xenko.Core.Mathematics;

namespace Xenko.VirtualReality
{
    public class WindowsMixedRealityTouchController : TouchController
    {
        private TouchControllerHand hand;

        public WindowsMixedRealityTouchController(TouchControllerHand hand)
        {
            this.hand = this.hand;
        }

        public override Vector3 Position { get; }
        public override Quaternion Rotation { get; }
        public override Vector3 LinearVelocity { get; }
        public override Vector3 AngularVelocity { get; }
        public override DeviceState State { get; }
        public override float Trigger { get; }
        public override float Grip { get; }
        public override bool IndexPointing { get; }
        public override bool IndexResting { get; }
        public override bool ThumbUp { get; }
        public override bool ThumbResting { get; }
        public override Vector2 ThumbAxis { get; }

        public override bool IsPressedDown(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsPressed(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsPressReleased(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsTouchedDown(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsTouched(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsTouchReleased(TouchControllerButton button)
        {
            return false;
        }
    }
}

#endif
