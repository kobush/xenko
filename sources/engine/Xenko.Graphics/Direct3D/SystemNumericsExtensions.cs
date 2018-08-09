// Copyright (c) Xenko contributors (https://xenko.com) 
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if XENKO_PLATFORM_UWP

using Xenko.Core.Mathematics;

namespace Xenko.Graphics
{
    public static class SystemNumericsExtensions
    {
        public static Vector3 ToXenkoVector3(this System.Numerics.Vector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Quaternion ToXenkoQuaternion(this System.Numerics.Quaternion v)
        {
            return new Quaternion(v.X, v.Y, v.Z, v.W);
        }

        public static Matrix ToXenkoMatrix(this System.Numerics.Matrix4x4 v)
        {
            return new Matrix
            {
                M11 = v.M11, M12 = v.M12, M13 = v.M13, M14 = v.M14,
                M21 = v.M21, M22 = v.M22, M23 = v.M23, M24 = v.M24,
                M31 = v.M31, M32 = v.M32, M33 = v.M33, M34 = v.M34,
                M41 = v.M41, M42 = v.M42, M43 = v.M43, M44 = v.M44
            };
        }
    }
}

#endif
