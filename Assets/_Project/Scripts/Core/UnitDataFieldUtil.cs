using System;
using System.Linq;
using System.Reflection;

namespace GridEmpire.Core
{
    public static class UnitDataFieldUtil
    {
        private static readonly string[] ExcludedFields = { "index" };

        public static FieldInfo[] GetEditableFields(UnitData data)
        {
            return typeof(UnitData).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => (f.FieldType == typeof(int) || f.FieldType == typeof(float))
                            && !ExcludedFields.Contains(f.Name))
                .ToArray();
        }

        public static float GetValue(UnitData data, FieldInfo field) =>
            Convert.ToSingle(field.GetValue(data));

        public static void SetValue(UnitData data, FieldInfo field, float value)
        {
            if (field.FieldType == typeof(int))
                field.SetValue(data, (int)value);
            else
                field.SetValue(data, value);
        }
    }
}