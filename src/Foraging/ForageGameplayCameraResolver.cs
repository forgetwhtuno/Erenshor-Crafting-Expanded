using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Shared no-per-frame-allocation gameplay-camera resolver for forage pointer interaction and
    // world nameplates. It prefers the largest active on-screen camera that actually renders the
    // resource layer; broad culling coverage breaks same-size ties in favor of the world/gameplay
    // camera rather than a narrow overlay camera. The reusable array grows only when needed.
    internal static class ForageGameplayCameraResolver
    {
        private static Camera[] _cameraBuffer = new Camera[8];

        internal static Camera Resolve(Camera cached, int visibleLayer)
        {
            Camera best = null;
            float bestArea = -1f;
            int bestCoverage = -1;
            float bestDepth = float.MinValue;

            Consider(cached, visibleLayer, ref best, ref bestArea, ref bestCoverage, ref bestDepth);

            Camera main = null;
            try { main = Camera.main; } catch { main = null; }
            Consider(main, visibleLayer, ref best, ref bestArea, ref bestCoverage, ref bestDepth);

            try
            {
                int count = Camera.allCamerasCount;
                if (count > 0)
                {
                    if (_cameraBuffer == null || _cameraBuffer.Length < count)
                        _cameraBuffer = new Camera[count + 4];
                    int copied = Camera.GetAllCameras(_cameraBuffer);
                    for (int i = 0; i < copied; i++)
                        Consider(_cameraBuffer[i], visibleLayer, ref best, ref bestArea, ref bestCoverage, ref bestDepth);
                }
            }
            catch { }

            return best;
        }

        internal static string Describe(Camera camera)
        {
            if (camera == null) return "camera=(none)";
            try
            {
                return "camera={" + camera.name + "} depth=" + camera.depth.ToString("F1") +
                    " rect=" + camera.pixelWidth.ToString() + "x" + camera.pixelHeight.ToString();
            }
            catch { return "camera=(unavailable)"; }
        }

        internal static void Reset()
        {
            if (_cameraBuffer == null || _cameraBuffer.Length > 32) _cameraBuffer = new Camera[8];
            else
            {
                for (int i = 0; i < _cameraBuffer.Length; i++) _cameraBuffer[i] = null;
            }
        }

        private static void Consider(
            Camera candidate,
            int visibleLayer,
            ref Camera best,
            ref float bestArea,
            ref int bestCoverage,
            ref float bestDepth)
        {
            if (!IsUsable(candidate, visibleLayer)) return;
            Rect pixelRect = candidate.pixelRect;
            float area = pixelRect.width * pixelRect.height;
            int coverage = CountBits(candidate.cullingMask);
            float depth = candidate.depth;

            if (best == null ||
                area > bestArea + 0.5f ||
                (Mathf.Abs(area - bestArea) <= 0.5f && coverage > bestCoverage) ||
                (Mathf.Abs(area - bestArea) <= 0.5f && coverage == bestCoverage && depth > bestDepth))
            {
                best = candidate;
                bestArea = area;
                bestCoverage = coverage;
                bestDepth = depth;
            }
        }

        private static bool IsUsable(Camera camera, int visibleLayer)
        {
            if (camera == null || !camera.isActiveAndEnabled || camera.gameObject == null || !camera.gameObject.activeInHierarchy) return false;
            if (camera.targetTexture != null) return false;
            if (visibleLayer >= 0 && visibleLayer <= 31 && (camera.cullingMask & (1 << visibleLayer)) == 0) return false;
            return camera.pixelWidth > 0 && camera.pixelHeight > 0;
        }

        private static int CountBits(int value)
        {
            unchecked
            {
                uint bits = (uint)value;
                int count = 0;
                while (bits != 0u)
                {
                    bits &= bits - 1u;
                    count++;
                }
                return count;
            }
        }
    }
}
