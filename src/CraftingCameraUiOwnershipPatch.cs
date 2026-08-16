using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Crafting-owned camera containment. Installation is manual and fail-closed: the current
    // CameraController IL shape is re-proved before Harmony is allowed to touch UsingUI().
    // The postfix itself is monotonic and can only promote false -> true for an active
    // Crafting-owned drag/resize gesture.
    internal static class CraftingCameraUiOwnershipPatch
    {
        internal static bool TryInstall(Harmony harmony, out string diagnostic)
        {
            diagnostic = "not-checked";
            if (harmony == null)
            {
                diagnostic = "Harmony unavailable";
                return false;
            }

            MethodInfo usingUi;
            string proof;
            if (!CraftingCameraUiCompatibility.TryVerify(out usingUi, out proof))
            {
                diagnostic = proof;
                return false;
            }

            try
            {
                MethodInfo postfix = typeof(CraftingCameraUiOwnershipPatch).GetMethod(
                    "Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfix == null)
                {
                    diagnostic = "postfix method unavailable";
                    return false;
                }
                harmony.Patch(usingUi, null, new HarmonyMethod(postfix));
                diagnostic = proof;
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = "patch failed: " + ex.GetType().Name;
                return false;
            }
        }

        private static void Postfix(ref bool __result)
        {
            __result = CraftingCameraUiPolicy.PromoteUsingUi(__result, CraftingUiPointerOwnership.HasOwners);
        }
    }

    internal static class CraftingCameraUiCompatibility
    {
        private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpCodeTable();

        internal static bool TryVerify(out MethodInfo usingUi, out string diagnostic)
        {
            usingUi = null;
            diagnostic = "camera containment compatibility not verified";
            try
            {
                Type cameraType = typeof(CameraController);
                BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                usingUi = cameraType.GetMethod("UsingUI", instanceFlags, null, Type.EmptyTypes, null);
                if (!ExactMethod(usingUi, cameraType, typeof(bool))) return Fail(out usingUi, out diagnostic, "UsingUI shape mismatch");

                FieldInfo uiWindows = cameraType.GetField("UIWindows", instanceFlags);
                if (uiWindows == null || uiWindows.DeclaringType != cameraType || uiWindows.FieldType != typeof(List<GameObject>))
                    return Fail(out usingUi, out diagnostic, "UIWindows shape mismatch");

                MethodInfo activeSelf = typeof(GameObject).GetProperty("activeSelf", BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
                if (activeSelf == null || !References(usingUi, uiWindows) || !References(usingUi, activeSelf))
                    return Fail(out usingUi, out diagnostic, "UsingUI no longer scans UIWindows.activeSelf");

                MethodInfo update = cameraType.GetMethod("Update", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo modern = cameraType.GetMethod("ModernControls", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo controls = cameraType.GetMethod("Controls", instanceFlags, null, Type.EmptyTypes, null);
                if (!ExactMethod(update, cameraType, typeof(void)) ||
                    !ExactMethod(modern, cameraType, typeof(void)) ||
                    !ExactMethod(controls, cameraType, typeof(void)))
                    return Fail(out usingUi, out diagnostic, "camera control method shape mismatch");

                if (!References(update, modern))
                    return Fail(out usingUi, out diagnostic, "Update no longer references ModernControls");
                if (!References(modern, usingUi))
                    return Fail(out usingUi, out diagnostic, "ModernControls no longer references UsingUI");

                FieldInfo releaseMouse = cameraType.GetField("releaseMouse", instanceFlags);
                if (releaseMouse == null || releaseMouse.DeclaringType != cameraType || releaseMouse.FieldType != typeof(bool) || !References(modern, releaseMouse))
                    return Fail(out usingUi, out diagnostic, "ModernControls releaseMouse boundary mismatch");

                MethodInfo getAxis = typeof(Input).GetMethod("GetAxis", staticFlags, null, new Type[] { typeof(string) }, null);
                if (getAxis == null || !References(modern, getAxis))
                    return Fail(out usingUi, out diagnostic, "ModernControls mouse-axis boundary mismatch");

                FieldInfo dragging = typeof(GameData).GetField("DraggingUIElement", staticFlags);
                if (dragging == null || dragging.FieldType != typeof(bool) || !References(controls, dragging))
                    return Fail(out usingUi, out diagnostic, "standard Controls drag boundary mismatch");

                diagnostic = "verified CameraController.UsingUI/UIWindows + modern/standard input boundaries";
                return true;
            }
            catch (Exception ex)
            {
                return Fail(out usingUi, out diagnostic, "verification exception: " + ex.GetType().Name);
            }
        }

        private static bool ExactMethod(MethodInfo method, Type declaringType, Type returnType)
        {
            return method != null && method.DeclaringType == declaringType && method.ReturnType == returnType && method.GetParameters().Length == 0;
        }

        private static bool Fail(out MethodInfo usingUi, out string diagnostic, string reason)
        {
            usingUi = null;
            diagnostic = reason;
            return false;
        }

        private static bool References(MethodBase source, MemberInfo target)
        {
            if (source == null || target == null) return false;
            MethodBody body = source.GetMethodBody();
            if (body == null) return false;
            byte[] il = body.GetILAsByteArray();
            if (il == null || il.Length == 0) return false;
            Type[] typeArgs = source.DeclaringType != null && source.DeclaringType.IsGenericType
                ? source.DeclaringType.GetGenericArguments()
                : Type.EmptyTypes;
            Type[] methodArgs = source.IsGenericMethod ? source.GetGenericArguments() : Type.EmptyTypes;

            int offset = 0;
            while (offset < il.Length)
            {
                OpCode op;
                byte first = il[offset++];
                short key;
                if (first == 0xFE)
                {
                    if (offset >= il.Length) return false;
                    key = unchecked((short)(0xFE00 | il[offset++]));
                }
                else key = first;
                if (!OpCodesByValue.TryGetValue(key, out op)) return false;

                int tokenOffset = -1;
                int operandSize;
                if (!TryGetOperandSize(op.OperandType, il, offset, out operandSize)) return false;
                if (op.OperandType == OperandType.InlineField || op.OperandType == OperandType.InlineMethod ||
                    op.OperandType == OperandType.InlineTok || op.OperandType == OperandType.InlineType)
                    tokenOffset = offset;

                if (tokenOffset >= 0)
                {
                    try
                    {
                        int token = BitConverter.ToInt32(il, tokenOffset);
                        MemberInfo referenced = source.Module.ResolveMember(token, typeArgs, methodArgs);
                        if (SameMember(referenced, target)) return true;
                    }
                    catch { }
                }
                offset += operandSize;
            }
            return false;
        }

        private static bool SameMember(MemberInfo left, MemberInfo right)
        {
            if (left == null || right == null) return false;
            try { return left.Module == right.Module && left.MetadataToken == right.MetadataToken; }
            catch { return left == right; }
        }

        private static bool TryGetOperandSize(OperandType type, byte[] il, int offset, out int size)
        {
            size = 0;
            switch (type)
            {
                case OperandType.InlineNone: size = 0; return true;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: size = 1; return true;
                case OperandType.InlineVar: size = 2; return true;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: size = 4; return true;
                case OperandType.InlineI8:
                case OperandType.InlineR: size = 8; return true;
                case OperandType.InlineSwitch:
                    if (offset + 4 > il.Length) return false;
                    int count = BitConverter.ToInt32(il, offset);
                    if (count < 0 || count > (il.Length - offset - 4) / 4) return false;
                    size = 4 + count * 4;
                    return true;
                default: return false;
            }
        }

        private static Dictionary<short, OpCode> BuildOpCodeTable()
        {
            Dictionary<short, OpCode> result = new Dictionary<short, OpCode>();
            FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(OpCode)) continue;
                OpCode op = (OpCode)fields[i].GetValue(null);
                result[op.Value] = op;
            }
            return result;
        }
    }
}
