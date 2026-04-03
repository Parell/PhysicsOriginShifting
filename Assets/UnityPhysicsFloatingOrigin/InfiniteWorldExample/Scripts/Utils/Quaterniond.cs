// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine.Internal;

namespace UnityEngine
{
    [StructLayout(LayoutKind.Sequential), Serializable]
    public partial struct Quaterniond
    {
        public double LengthSquared
        {
            get
            {
                return x * x + y * y + z * z + w * w;
            }
        }

        public Vector3d xyz
        {
            set
            {
                x = value.x;
                y = value.y;
                z = value.z;
            }
            get
            {
                return new Vector3d(x, y, z);
            }
        }
        // X component of the Quaterniond. Don't modify this directly unless you know quaternions inside out.
        public double x;
        // Y component of the Quaterniond. Don't modify this directly unless you know quaternions inside out.
        public double y;
        // Z component of the Quaterniond. Don't modify this directly unless you know quaternions inside out.
        public double z;
        // W component of the Quaterniond. Don't modify this directly unless you know quaternions inside out.
        public double w;

        // Access the x, y, z, w components using [0], [1], [2], [3] respectively.
        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    case 3: return w;
                    default:
                        throw new IndexOutOfRangeException("Invalid Quaterniond index!");
                }
            }

            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default:
                        throw new IndexOutOfRangeException("Invalid Quaterniond index!");
                }
            }
        }

        // Constructs new Quaterniond with given x,y,z,w components.
        public Quaterniond(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public Quaterniond(Vector3d v, double w)
        {
            x = v.x;
            y = v.y;
            z = v.z;
            this.w = w;
        }

        // Set x, y, z and w components of an existing Quaterniond.
        public void Set(double newX, double newY, double newZ, double newW)
        {
            x = newX;
            y = newY;
            z = newZ;
            w = newW;
        }

        static readonly Quaterniond identityQuaternion = new Quaterniond(0F, 0F, 0F, 1F);

        // The identity rotation (RO). This quaternion corresponds to "no rotation": the object
        public static Quaterniond identity
        {
            get
            {
                return identityQuaternion;
            }
        }

        // Combines rotations /lhs/ and /rhs/.
        public static Quaterniond operator *(Quaterniond lhs, Quaterniond rhs)
        {
            return new Quaterniond(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
        }

        // Rotates the point /point/ with /rotation/.
        public static Vector3d operator *(Quaterniond rotation, Vector3d point)
        {
            double x = rotation.x * 2d;
            double y = rotation.y * 2d;
            double z = rotation.z * 2d;
            double xx = rotation.x * x;
            double yy = rotation.y * y;
            double zz = rotation.z * z;
            double xy = rotation.x * y;
            double xz = rotation.x * z;
            double yz = rotation.y * z;
            double wx = rotation.w * x;
            double wy = rotation.w * y;
            double wz = rotation.w * z;

            Vector3d res;
            res.x = (1d - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z;
            res.y = (xy + wz) * point.x + (1d - (xx + zz)) * point.y + (yz - wx) * point.z;
            res.z = (xz - wy) * point.x + (yz + wx) * point.y + (1d - (xx + yy)) * point.z;
            return res;
        }

        public const double kEpsilon = 0.000001d;

        // Is the dot product of two quaternions within tolerance for them to be considered equal?
        private static bool IsEqualUsingDot(double dot)
        {
            // Returns false in the presence of NaN values.
            return dot > 1.0d - kEpsilon;
        }

        // Are two quaternions equal to each other?
        public static bool operator ==(Quaterniond lhs, Quaterniond rhs)
        {
            return IsEqualUsingDot(Dot(lhs, rhs));
        }

        // Are two quaternions different from each other?
        public static bool operator !=(Quaterniond lhs, Quaterniond rhs)
        {
            // Returns true in the presence of NaN values.
            return !(lhs == rhs);
        }

        // The dot product between two rotations.
        public static double Dot(Quaterniond a, Quaterniond b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        }

        public static Quaterniond LookRotation(Vector3d forward, [DefaultValue("Vector3d.up")] Vector3d upwards)
        {
            return INTERNAL_CALL_LookRotation(ref forward, ref upwards);
        }

        public static Quaterniond LookRotation(Vector3d forward)
        {
            Vector3d up = Vector3d.up;
            return INTERNAL_CALL_LookRotation(ref forward, ref up);
        }

        // from http://answers.unity3d.com/questions/467614/what-is-the-source-code-of-quaternionlookrotation.html
        private static Quaterniond INTERNAL_CALL_LookRotation(ref Vector3d forward, ref Vector3d up)
        {
            forward = Vector3d.Normalize(forward);
            Vector3d right = Vector3d.Normalize(Vector3d.Cross(up, forward));
            up = Vector3d.Cross(forward, right);
            var m00 = right.x;
            var m01 = right.y;
            var m02 = right.z;
            var m10 = up.x;
            var m11 = up.y;
            var m12 = up.z;
            var m20 = forward.x;
            var m21 = forward.y;
            var m22 = forward.z;

            double num8 = m00 + m11 + m22;
            var quaternion = new Quaterniond();
            if (num8 > 0d)
            {
                var num = (double)Math.Sqrt(num8 + 1d);
                quaternion.w = num * 0.5d;
                num = 0.5d / num;
                quaternion.x = (m12 - m21) * num;
                quaternion.y = (m20 - m02) * num;
                quaternion.z = (m01 - m10) * num;
                return quaternion;
            }
            if ((m00 >= m11) && (m00 >= m22))
            {
                var num7 = (double)Math.Sqrt(1d + m00 - m11 - m22);
                var num4 = 0.5d / num7;
                quaternion.x = 0.5d * num7;
                quaternion.y = (m01 + m10) * num4;
                quaternion.z = (m02 + m20) * num4;
                quaternion.w = (m12 - m21) * num4;
                return quaternion;
            }
            if (m11 > m22)
            {
                var num6 = (double)Math.Sqrt(1d + m11 - m00 - m22);
                var num3 = 0.5d / num6;
                quaternion.x = (m10 + m01) * num3;
                quaternion.y = 0.5d * num6;
                quaternion.z = (m21 + m12) * num3;
                quaternion.w = (m20 - m02) * num3;
                return quaternion;
            }
            var num5 = (double)Math.Sqrt(1d + m22 - m00 - m11);
            var num2 = 0.5d / num5;
            quaternion.x = (m20 + m02) * num2;
            quaternion.y = (m21 + m12) * num2;
            quaternion.z = 0.5d * num5;
            quaternion.w = (m01 - m10) * num2;
            return quaternion;
        }

        public void SetLookRotation(Vector3d view)
        {
            Vector3d up = Vector3d.up;
            SetLookRotation(view, up);
        }

        // Creates a rotation with the specified /forward/ and /upwards/ directions.
        public void SetLookRotation(Vector3d view, [DefaultValue("Vector3d.up")] Vector3d up)
        {
            this = LookRotation(view, up);
        }

        // Returns the angle in degrees between two rotations /a/ and /b/.
        public static double Angle(Quaterniond a, Quaterniond b)
        {
            double dot = Mathd.Min(Mathd.Abs(Dot(a, b)), 1.0d);
            return IsEqualUsingDot(dot) ? 0.0d : Mathd.Acos(dot) * 2.0d * Mathd.Rad2Deg;
        }

        // Makes euler angles positive 0/360 with 0.0001 hacked to support old behaviour of QuaternionToEuler
        private static Vector3d Internal_MakePositive(Vector3d euler)
        {
            double negativeFlip = -0.0001d * Mathd.Rad2Deg;
            double positiveFlip = 360.0d + negativeFlip;

            if (euler.x < negativeFlip)
                euler.x += 360.0d;
            else if (euler.x > positiveFlip)
                euler.x -= 360.0d;

            if (euler.y < negativeFlip)
                euler.y += 360.0d;
            else if (euler.y > positiveFlip)
                euler.y -= 360.0d;

            if (euler.z < negativeFlip)
                euler.z += 360.0d;
            else if (euler.z > positiveFlip)
                euler.z -= 360.0d;

            return euler;
        }

        public Vector3d eulerAngles
        {
            get { return Internal_MakePositive(Internal_ToEulerRad(this) * Mathd.Rad2Deg); }

            set { this = Internal_FromEulerRad(value * Mathd.Deg2Rad); }
        }

        public static Quaterniond Euler(double x, double y, double z) { return Internal_FromEulerRad(new Vector3d(x, y, z) * Mathd.Deg2Rad); }

        public static Quaterniond Euler(Vector3d euler) { return Internal_FromEulerRad(euler * Mathd.Deg2Rad); }

        public void ToAngleAxis(out double angle, out Vector3d axis) { Internal_ToAxisAngleRad(this, out axis, out angle); angle *= Mathd.Rad2Deg; }

        public static Quaterniond FromToRotation(Vector3d fromDirection, Vector3d toDirection)
        {
            return RotateTowards(LookRotation(fromDirection), LookRotation(toDirection), double.MaxValue);
        }

        public void SetFromToRotation(Vector3d fromDirection, Vector3d toDirection) { this = FromToRotation(fromDirection, toDirection); }

        // from http://stackoverflow.com/questions/11492299/quaternion-to-euler-angles-algorithm-how-to-convert-to-y-up-and-between-ha
        private static Quaterniond Internal_FromEulerRad(Vector3d euler)
        {
            var yaw = euler.x;
            var pitch = euler.y;
            var roll = euler.z;
            double rollOver2 = roll * 0.5d;
            double sinRollOver2 = (double)Math.Sin((double)rollOver2);
            double cosRollOver2 = (double)Math.Cos((double)rollOver2);
            double pitchOver2 = pitch * 0.5d;
            double sinPitchOver2 = (double)Math.Sin((double)pitchOver2);
            double cosPitchOver2 = (double)Math.Cos((double)pitchOver2);
            double yawOver2 = yaw * 0.5d;
            double sinYawOver2 = (double)Math.Sin((double)yawOver2);
            double cosYawOver2 = (double)Math.Cos((double)yawOver2);
            Quaterniond result;
            result.x = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;
            result.z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.w = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            return result;
        }

        private static void Internal_ToAxisAngleRad(Quaterniond q, out Vector3d axis, out double angle)
        {
            if (Math.Abs(q.w) > 1.0d)
                q.Normalize();


            angle = 2.0d * (double)Math.Acos(q.w); // angle
            double den = (double)Math.Sqrt(1.0d - q.w * q.w);
            if (den > 0.0001d)
            {
                axis = q.xyz / den;
            }
            else
            {
                // This occurs when the angle is zero. 
                // Not a problem: just set an arbitrary normalized axis.
                axis = new Vector3d(1, 0, 0);
            }
        }

        private static Vector3d Internal_ToEulerRad(Quaterniond rotation)
        {
            double sqw = rotation.w * rotation.w;
            double sqx = rotation.x * rotation.x;
            double sqy = rotation.y * rotation.y;
            double sqz = rotation.z * rotation.z;
            double unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            double test = rotation.x * rotation.w - rotation.y * rotation.z;
            Vector3d v;

            if (test > 0.4995d * unit)
            { // singularity at north pole
                v.y = 2d * Mathd.Atan2(rotation.y, rotation.x);
                v.x = Mathd.PI / 2;
                v.z = 0;
                return NormalizeAngles(v * Mathd.Rad2Deg);
            }
            if (test < -0.4995d * unit)
            { // singularity at south pole
                v.y = -2d * Mathd.Atan2(rotation.y, rotation.x);
                v.x = -Mathd.PI / 2;
                v.z = 0;
                return NormalizeAngles(v * Mathd.Rad2Deg);
            }
            Quaterniond q = new Quaterniond(rotation.w, rotation.z, rotation.x, rotation.y);
            v.y = Math.Atan2(2d * q.x * q.w + 2d * q.y * q.z, 1 - 2d * (q.z * q.z + q.w * q.w));     // Yaw
            v.x = Math.Asin(2d * (q.x * q.z - q.w * q.y));                             // Pitch
            v.z = Math.Atan2(2d * q.x * q.y + 2d * q.z * q.w, 1 - 2d * (q.y * q.y + q.z * q.z));      // Roll
            return NormalizeAngles(v * Mathd.Rad2Deg);
        }

        public static Quaterniond RotateTowards(Quaterniond from, Quaterniond to, double maxDegreesDelta)
        {
            double angle = Angle(from, to);
            if (angle == 0.0d) return to;
            return SlerpUnclamped(from, to, Mathd.Min(1.0d, maxDegreesDelta / angle));
        }

        public static Quaterniond SlerpUnclamped(Quaterniond a, Quaterniond b, double t)
        {
            return INTERNAL_CALL_SlerpUnclamped(ref a, ref b, t);
        }

        private static Quaterniond INTERNAL_CALL_SlerpUnclamped(ref Quaterniond a, ref Quaterniond b, double t)
        {
            // if either input is zero, return the other.
            if (a.LengthSquared == 0.0d)
            {
                if (b.LengthSquared == 0.0d)
                {
                    return identity;
                }
                return b;
            }
            else if (b.LengthSquared == 0.0d)
            {
                return a;
            }

            double cosHalfAngle = a.w * b.w + Vector3d.Dot(a.xyz, b.xyz);

            if (cosHalfAngle >= 1.0d || cosHalfAngle <= -1.0d)
            {
                // angle = 0.0d, so just return one input.
                return a;
            }
            else if (cosHalfAngle < 0.0d)
            {
                b.xyz = -b.xyz;
                b.w = -b.w;
                cosHalfAngle = -cosHalfAngle;
            }

            double blendA;
            double blendB;
            if (cosHalfAngle < 0.99d)
            {
                // do proper slerp for big angles
                double halfAngle = (double)Math.Acos(cosHalfAngle);
                double sinHalfAngle = (double)Math.Sin(halfAngle);
                double oneOverSinHalfAngle = 1.0d / sinHalfAngle;
                blendA = (double)Math.Sin(halfAngle * (1.0d - t)) * oneOverSinHalfAngle;
                blendB = (double)Math.Sin(halfAngle * t) * oneOverSinHalfAngle;
            }
            else
            {
                // do lerp if angle is really small.
                blendA = 1.0d - t;
                blendB = t;
            }

            Quaterniond result = new Quaterniond(blendA * a.xyz + blendB * b.xyz, blendA * a.w + blendB * b.w);
            if (result.LengthSquared > 0.0d)
                return Normalize(result);
            else
                return identity;
        }

        private static Vector3d NormalizeAngles(Vector3d angles)
        {
            angles.x = NormalizeAngle(angles.x);
            angles.y = NormalizeAngle(angles.y);
            angles.z = NormalizeAngle(angles.z);
            return angles;
        }

        private static double NormalizeAngle(double angle)
        {
            double modAngle = angle % 360.0d;

            if (modAngle < 0.0d)
                return modAngle + 360.0d;
            else
                return modAngle;
        }

        public static Quaterniond Normalize(Quaterniond q)
        {
            double mag = Mathd.Sqrt(Dot(q, q));

            if (mag < Mathd.Epsilon)
                return identity;

            return new Quaterniond(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        public void Normalize()
        {
            this = Normalize(this);
        }

        public Quaterniond normalized
        {

            get { return Normalize(this); }
        }

        // used to allow Quaternions to be used as keys in hash tables
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);
        }

        // also required for being able to use Quaternions as keys in hash tables
        public override bool Equals(object other)
        {
            if (!(other is Quaterniond)) return false;

            return Equals((Quaterniond)other);
        }

        public bool Equals(Quaterniond other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z) && w.Equals(other.w);
        }

        public override string ToString()
        {
            return ToString(null, null);
        }

        public string ToString(string format)
        {
            return ToString(format, null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
                format = "F5";
            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("({0}, {1}, {2}, {3})", x.ToString(format, formatProvider), y.ToString(format, formatProvider), z.ToString(format, formatProvider), w.ToString(format, formatProvider));
        }

        public static explicit operator Quaternion(Quaterniond quaternion)
        {
            return new Quaternion((float)quaternion.x, (float)quaternion.y, (float)quaternion.z, (float)quaternion.w);
        }

        public static explicit operator Quaterniond(Quaternion quaternion)
        {
            return new Quaterniond(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
        }
    }
}