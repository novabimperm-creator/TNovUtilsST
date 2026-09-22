using Autodesk.Revit.DB;

namespace TNovUtilsST
{
    internal static class RevitApiCompat
    {
        public static int ElementIdIntValue(ElementId elementId)
        {
#if R2022
            return elementId.IntegerValue;
#else
            return checked((int)elementId.Value);
#endif
        }

        public static ElementId CreateElementId(int value)
        {
#if R2022
            return new ElementId(value);
#else
            return new ElementId((long)value);
#endif
        }

#if R2022
        public static BuiltInParameterGroup GetParameterGroup(InternalDefinition definition)
        {
            return definition.ParameterGroup;
        }

        public static bool InsertParameterBinding(Document doc, Definition definition, Binding binding, BuiltInParameterGroup group)
        {
            return doc.ParameterBindings.Insert(definition, binding, group);
        }

        public static bool ReInsertParameterBinding(Document doc, Definition definition, Binding binding, BuiltInParameterGroup group)
        {
            return doc.ParameterBindings.ReInsert(definition, binding, group);
        }
#else
        public static ForgeTypeId GetParameterGroup(InternalDefinition definition)
        {
            return definition.GetGroupTypeId();
        }

        public static bool InsertParameterBinding(Document doc, Definition definition, Binding binding, ForgeTypeId group)
        {
            return doc.ParameterBindings.Insert(definition, binding, group);
        }

        public static bool ReInsertParameterBinding(Document doc, Definition definition, Binding binding, ForgeTypeId group)
        {
            return doc.ParameterBindings.ReInsert(definition, binding, group);
        }
#endif

        public static ExternalDefinitionCreationOptions CreateExternalDefinitionOptions(string name, Definition sourceDefinition)
        {
#if R2022
#pragma warning disable CS0618
            return new ExternalDefinitionCreationOptions(name, sourceDefinition.ParameterType);
#pragma warning restore CS0618
#else
            return new ExternalDefinitionCreationOptions(name, sourceDefinition.GetDataType());
#endif
        }
    }
}
