using MonoMod;
using MonoMod.Cil;
using Mono.Cecil;
using System;
using MonoMod.InlineRT;
using MonoMod.Utils;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Linq;

namespace Celeste {
    public class patch_LightingRenderer : LightingRenderer {

        [MonoModIgnore]
        [MonoModConstructor]
        [MonoModIfFlag("RelinkXNA")]
        [PatchMinMaxBlendFunction]
        public static extern void cctor();

        [MonoModIgnore]
        [PatchLightingRendererRender]
        public extern void BeforeRender();
    }
}

namespace MonoMod {
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLightingRendererBeforeRender))]
    class PatchLightingRendererRenderAttribute : Attribute { }
    partial class MonoModRules {
        public static void PatchLightingRendererBeforeRender(ILContext context, CustomAttribute attrib) {
            // References for Matching IL
            TypeDefinition t_VertexLight = MonoModRule.Modder.FindType("Celeste.VertexLight").Resolve();
            FieldReference f_VertexLight_Index = t_VertexLight.FindField("Index");

            // References for resizing array
            TypeDefinition t_Array = MonoModRule.Modder.FindType("System.Array").Resolve();
            MethodDefinition m_Array_Resize = t_Array.FindMethod("Resize");
            FieldReference f_lights = context.Method.DeclaringType.FindField("lights").Resolve();

            // References for getting the level set
            FieldReference f_Session = context.Method.DeclaringType.FindField("Session").Resolve();
            FieldReference f_Session_Area = f_Session.FieldType.Resolve().FindField("Area").Resolve();
            MethodDefinition m_Area_GetLevelSet = f_Session_Area.FieldType.Resolve().FindMethod("System.String GetLevelSet()");
            

            ILCursor cursor = new ILCursor(context);

            int loc_component = -1;

            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdarg0(),
                instr => instr.MatchLdfld(f_lights),
                instr => instr.MatchLdloc(out loc_component),
                instr => instr.MatchLdfld(f_VertexLight_Index),
                instr => instr.MatchLdnull(),
                instr => instr.MatchStelemRef());
            

            ILLabel end = cursor.DefineLabel();
            // if (Session.Area.GetLevelSet() != "Celeste" && edgeVerts.length >= component.Index) {
            // Session.Area.GetLevelSet() != "Celeste"
            cursor.EmitLdarg0();
            cursor.EmitLdfld(f_Session);
            cursor.EmitLdfld(f_Session_Area);
            cursor.EmitCall(m_Area_GetLevelSet);
            cursor.EmitLdstr("Celeste");
            cursor.EmitBeq(end);

            // lights.length >= component.Index
            cursor.EmitLdarg0();
            cursor.EmitLdfld(f_lights);
            cursor.EmitLdlen();
            cursor.EmitLdloc(loc_component);
            cursor.EmitLdfld(f_VertexLight_Index);
            cursor.EmitBlt(end);

            // Array.Resize(ref lights, component.Index + 1);
            cursor.EmitLdflda(f_lights);
            cursor.EmitLdloc(loc_component);
            cursor.EmitLdfld(f_VertexLight_Index);
            cursor.EmitLdcI4(1);
            cursor.EmitAddOvf();
            cursor.EmitCall(m_Array_Resize);

            // }
            cursor.MarkLabel(end);
        }
    }
}