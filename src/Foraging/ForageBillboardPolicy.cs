using System;

namespace ErenshorCraftingExpanded
{
    // Pure math for the current world-space resource nameplate convention. Unity's world-space
    // Canvas/TMP readable side is treated as local -Z by the proven presentation. Therefore a
    // screen-aligned nameplate keeps Transform +Z parallel to the gameplay camera's +forward.
    // The label no longer points at the camera's world position; it follows the view plane itself.
    public static class ForageBillboardPolicy
    {
        private const float MinimumMagnitudeSquared = 0.000001f;

        public static bool TryNormalizeViewForward(
            float cameraForwardX,
            float cameraForwardY,
            float cameraForwardZ,
            out float forwardX,
            out float forwardY,
            out float forwardZ)
        {
            forwardX = cameraForwardX;
            forwardY = cameraForwardY;
            forwardZ = cameraForwardZ;
            float lengthSquared = forwardX * forwardX + forwardY * forwardY + forwardZ * forwardZ;
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared <= MinimumMagnitudeSquared)
            {
                forwardX = 0f;
                forwardY = 0f;
                forwardZ = 0f;
                return false;
            }

            float inverseLength = (float)(1.0 / Math.Sqrt(lengthSquared));
            forwardX *= inverseLength;
            forwardY *= inverseLength;
            forwardZ *= inverseLength;
            return true;
        }

        public static bool IsReadableFaceOpposedToView(
            float canvasForwardX,
            float canvasForwardY,
            float canvasForwardZ,
            float cameraForwardX,
            float cameraForwardY,
            float cameraForwardZ)
        {
            float cx; float cy; float cz;
            if (!TryNormalizeViewForward(cameraForwardX, cameraForwardY, cameraForwardZ, out cx, out cy, out cz))
                return false;
            float fx; float fy; float fz;
            if (!TryNormalizeViewForward(canvasForwardX, canvasForwardY, canvasForwardZ, out fx, out fy, out fz))
                return false;

            // Canvas readable normal is -forward and the camera looks along +forward. For the
            // readable face to point back toward the viewer, canvas +forward must match camera
            // +forward. This remains true through every yaw/pitch direction.
            return fx * cx + fy * cy + fz * cz > 0.999f;
        }

        internal static string RunSelfTests()
        {
            float[,] viewDirections = new float[,]
            {
                { 0f, 0f, 1f },
                { 0f, 0f, -1f },
                { 1f, 0f, 0f },
                { -1f, 0f, 0f },
                { 0f, 1f, 0.01f },
                { 0f, -1f, -0.01f },
                { 0.7f, -0.2f, 0.7f },
                { -0.6f, 0.5f, -0.6f }
            };

            for (int i = 0; i < viewDirections.GetLength(0); i++)
            {
                float fx; float fy; float fz;
                if (!TryNormalizeViewForward(
                    viewDirections[i, 0], viewDirections[i, 1], viewDirections[i, 2],
                    out fx, out fy, out fz))
                    return "FAIL billboard view normalization " + i;

                float lengthSquared = fx * fx + fy * fy + fz * fz;
                if (Math.Abs(lengthSquared - 1f) > 0.0001f)
                    return "FAIL billboard normalized magnitude " + i;

                if (!IsReadableFaceOpposedToView(
                    fx, fy, fz,
                    viewDirections[i, 0], viewDirections[i, 1], viewDirections[i, 2]))
                    return "FAIL billboard readable face " + i;
            }

            if (IsReadableFaceOpposedToView(0f, 0f, -1f, 0f, 0f, 1f))
                return "FAIL billboard reversed face admitted";

            float ignoredX; float ignoredY; float ignoredZ;
            if (TryNormalizeViewForward(0f, 0f, 0f, out ignoredX, out ignoredY, out ignoredZ))
                return "FAIL billboard zero forward admitted";
            if (TryNormalizeViewForward(float.NaN, 0f, 1f, out ignoredX, out ignoredY, out ignoredZ))
                return "FAIL billboard NaN forward admitted";

            return "PASS forage billboard policy";
        }
    }
}
