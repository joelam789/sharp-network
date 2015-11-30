using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNetwork.SimpleWebSocket
{
    public interface IMessageChecker
    {
        bool Check(object data);
    }

    public class FieldNameChecker : IMessageChecker
    {
        private List<string> m_Keywords = new List<string>();

        public FieldNameChecker(params string[] keywords)
        {
            m_Keywords.AddRange(keywords);
        }

        public bool Check(object data)
        {
            if (data == null) return false;
            if (data is IDictionary<string, object>)
            {
                var map = data as IDictionary<string, object>;
                foreach (var keyword in m_Keywords)
                {
                    if (!map.ContainsKey(keyword)) return false;
                }
            }
            else
            {
                var properties = data.GetType().GetProperties();
                var names = new List<string>();
                foreach (var prop in properties) names.Add(prop.Name);
                foreach (var keyword in m_Keywords)
                {
                    if (!names.Contains(keyword)) return false;
                }
            }

            return true;
        }
    }

    public class FieldValueChecker : IMessageChecker
    {
        private string m_FieldName = "";
        private object m_FieldValue = null;

        public FieldValueChecker(string fieldName, object fieldValue)
        {
            m_FieldName = fieldName;
            m_FieldValue = fieldValue;

            // seems JSON parser will treat all integer as "long", so we need to "coordinate" with it 
            if (m_FieldValue is Int32) m_FieldValue = Convert.ToInt64(m_FieldValue);
        }

        public bool Check(object data)
        {
            if (data == null) return false;
            if (data is IDictionary<string, object>)
            {
                var map = data as IDictionary<string, object>;
                if (!map.ContainsKey(m_FieldName)) return false;
                else return Object.Equals(m_FieldValue, map[m_FieldName]);
            }
            else
            {
                var prop = data.GetType().GetProperty(m_FieldName);
                if (prop == null) return false;
                else return Object.Equals(m_FieldValue, prop.GetValue(data, null));
            }
        }
    }

    public class PropertyFieldNameChecker : IMessageChecker
    {
        private string m_PropertyName = "";
        private FieldNameChecker m_FileNameChecker = null;

        public PropertyFieldNameChecker(string propertyName, params string[] keywords)
        {
            m_PropertyName = propertyName;
            m_FileNameChecker = new FieldNameChecker(keywords);
        }

        public bool Check(object data)
        {
            if (data == null) return false;
            if (data is IDictionary<string, object>)
            {
                var map = data as IDictionary<string, object>;
                if (!map.ContainsKey(m_PropertyName)) return false;
                else return m_FileNameChecker.Check(map[m_PropertyName]);
            }
            else
            {
                var prop = data.GetType().GetProperty(m_PropertyName);
                if (prop == null) return false;
                else return m_FileNameChecker.Check(prop.GetValue(data, null));
            }
        }
    }

    public class PropertyFieldValueChecker : IMessageChecker
    {
        private string m_PropertyName = "";
        private FieldValueChecker m_FileValueChecker = null;

        public PropertyFieldValueChecker(string propertyName, string fieldName, object fieldValue)
        {
            m_PropertyName = propertyName;
            m_FileValueChecker = new FieldValueChecker(fieldName, fieldValue);
        }

        public bool Check(object data)
        {
            if (data == null) return false;
            if (data is IDictionary<string, object>)
            {
                var map = data as IDictionary<string, object>;
                if (!map.ContainsKey(m_PropertyName)) return false;
                else return m_FileValueChecker.Check(map[m_PropertyName]);
            }
            else
            {
                var prop = data.GetType().GetProperty(m_PropertyName);
                if (prop == null) return false;
                else return m_FileValueChecker.Check(prop.GetValue(data, null));
            }
        }
    }

}
