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
        }

        public void LoadData(XElement saveFile)
        {
            var saveFields = GetSaveFields();
            foreach (var field in saveFields)
            {
                XAttribute attr = saveFile.GetAttribute(field.Name);
                if (attr != null) field.SetValue(this, Convert.ChangeType(attr.Value, field.FieldType));
                else field.SetValue(this, ((SaveData)field.GetCustomAttribute(typeof(SaveData))).DefaultFieldValue);
            }
        }

        private IEnumerable<FieldInfo> GetSaveFields() => GetType().GetFields().Where(f => f.IsDefined(typeof(SaveData), false));
    }

    public class SaveData : Attribute
    {
        public SaveData(object defaultValue) => DefaultFieldValue = defaultValue;

        public object DefaultFieldValue;
    }
}
