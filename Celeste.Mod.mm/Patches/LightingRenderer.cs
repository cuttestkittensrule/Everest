using MonoMod;
using MonoMod.Cil;
using Mono.Cecil;
using System;
using MonoMod.InlineRT;
using MonoMod.Utils;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Celeste {
    public class patch_LightingRenderer : LightingRenderer {

        [MonoModIgnore]
        [MonoModConstructor]
        [MonoModIfFlag("RelinkXNA")]
        [PatchMinMaxBlendFunction]
        public static extern void cctor();

        [MonoModIgnore]
        [PatchLightingRendererRender]
        public extern void Render();
    }
}

namespace MonoMod {
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLightingRendererRender))]
    class PatchLightingRendererRenderAttribute : Attribute { }
    partial class MonoModRules {
        public static void PatchLightingRendererRender(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Array = MonoModRule.Modder.FindType("System.Array").Resolve();
            TypeReference t_Edge = MonoModRule.Modder.FindType("Celeste.LightningRenderer.Edge").Resolve();
            TypeReference t_Enumerator = MonoModRule.Modder.FindType("System.Collections.Generic.List.Enumerator").MakeGenericInstanceType(t_Edge).Resolve();

            MethodDefinition m_Array_Resize = t_Array.FindMethod("Resize");

            FieldReference f_edgeVerts = context.Method.DeclaringType.FindField("edgeVerts").Resolve();
            FieldReference f_Session = context.Method.DeclaringType.FindField("Session").Resolve();
            FieldReference f_Session_Area = f_Session.FieldType.Resolve().FindField("Area").Resolve();
            MethodDefinition m_Area_GetLevelSet = f_Session_Area.FieldType.Resolve().FindMethod("System.String GetLevelSet()");
            MethodDefinition get_Enumerator_Current = t_Enumerator.Resolve().FindMethod("get_Current").Resolve();
            

            ILCursor cursor = new ILCursor(context);

            cursor.GotoNext(
                MoveType.Before,
                instr => instr.MatchLdloca(6),
                instr => instr.MatchCall(get_Enumerator_Current),
                instr => instr.MatchStloc(7));

            ILLabel end = cursor.DefineLabel();
            // if (Session.Area.GetLevelSet() != "Celeste" && edgeVerts.length - index <= 16) {
            // Session.Area.GetLevelSet() != "Celeste"
            cursor.EmitLdarg0();
            cursor.Emit(OpCodes.Ldfld, f_Session);
            cursor.Emit(OpCodes.Ldfld, f_Session_Area);
            cursor.EmitCall(m_Area_GetLevelSet);
            cursor.EmitLdstr("Celeste");
            cursor.EmitCeq();
            cursor.EmitNot();

            // edgeVerts.length - index <= 16
            cursor.EmitLdarg0();
            cursor.Emit(OpCodes.Ldfld, f_edgeVerts);
            cursor.EmitLdlen();
            cursor.EmitLdloc3();
            cursor.EmitSub();
            cursor.EmitLdcI4(16);
            cursor.EmitCgt();

            //(... && ...) {
            cursor.EmitAnd();
            cursor.EmitBrfalse(end);

            // Array.Resize(ref edgeVerts, edgeVerts.length + 32);
            cursor.EmitLdloca(3);
            cursor.EmitLdarg0();
            cursor.Emit(OpCodes.Ldfld, f_edgeVerts);
            cursor.EmitLdlen();
            cursor.EmitLdcI4(32);
            cursor.EmitAddOvf();
            cursor.EmitCall(m_Array_Resize);

            // }
            cursor.MarkLabel(end);
        }
    }
}