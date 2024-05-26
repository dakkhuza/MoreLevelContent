using Barotrauma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace MoreLevelContent.Shared.Data
{
    public class DataBase
    {
        public void SaveData(XElement saveFile)
        {
            var saveFields = GetSaveFields();
            foreach (var field in saveFields)
            {
                saveFile.SetAttributeValue(field.Name, field.GetValue(this));
            }
            SaveSpecific(saveFile);
        }

        public void LoadData(XElement saveFile)
        {
            var saveFields = GetSaveFields();
            foreach (var field in saveFields)
            {
                XAttribute attr = saveFile.GetAttribute(field.Name);
                if (attr != null)
                {
                    if (field.FieldType.IsEnum)
                    {
                        field.SetValue(this, Enum.Parse(field.FieldType, attr.Value));
                        continue;
                    }
                    field.SetValue(this, Convert.ChangeType(attr.Value, field.FieldType));
                }
                else
                {
                    field.SetValue(this, ((AttributeSaveData)field.GetCustomAttribute(typeof(AttributeSaveData))).DefaultFieldValue);
                }
            }
            LoadSpecific(saveFile);
        }

        protected virtual void LoadSpecific(XElement saveFile) { }
        protected virtual void SaveSpecific(XElement saveFile) { }

        private IEnumerable<FieldInfo> GetSaveFields() => GetType().GetFields().Where(f => f.IsDefined(typeof(AttributeSaveData), false));
    }

    /// <summary>
    /// Simple data that can be stringified into an xml attribute
    /// </summary>
    public class AttributeSaveData : Attribute
    {
        public AttributeSaveData(object defaultValue) => DefaultFieldValue = defaultValue;

        public object DefaultFieldValue;
    }
}
