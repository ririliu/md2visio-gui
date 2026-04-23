using System.Text;

namespace md2visio.mermaid.cmn
{
    internal abstract class ValueAccessor
    {
        abstract public T? GetValue<T>(string keyPath) where T : class;
        abstract public void SetValue(string key, object value);

        virtual public bool GetString(string keyPath, out string s)
        {
            if (GetValue<string>(keyPath) is string str && !string.IsNullOrEmpty(str))
            {
                s = str;
                return true;
            }

            if (GetValue<object>(keyPath) is object raw)
            {
                s = raw.ToString() ?? string.Empty;
                return s != string.Empty;
            }

            s = string.Empty;
            return false;
        }

        virtual public bool GetInt(string keyPath, out int i)
        {
            if (GetValue<object>(keyPath) is object raw)
            {
                switch (raw)
                {
                    case int value:
                        i = value;
                        return true;
                    case long value:
                        i = (int)value;
                        return true;
                    case double value:
                        i = (int)value;
                        return true;
                    case float value:
                        i = (int)value;
                        return true;
                }

                if (int.TryParse(raw.ToString(), out i))
                {
                    return true;
                }
            }

            i = 0;
            return false;
        }

        virtual public bool GetDouble(string keyPath, out double d)
        {
            if (GetValue<object>(keyPath) is object raw)
            {
                switch (raw)
                {
                    case double value:
                        d = value;
                        return true;
                    case float value:
                        d = value;
                        return true;
                    case int value:
                        d = value;
                        return true;
                    case long value:
                        d = value;
                        return true;
                }

                if (double.TryParse(raw.ToString(), out d))
                {
                    return true;
                }
            }

            d = 0;
            return false;
        }

        virtual public bool GetBool(string keyPath, out bool b)
        {
            if (GetValue<object>(keyPath) is object raw)
            {
                switch (raw)
                {
                    case bool value:
                        b = value;
                        return true;
                    case int value:
                        b = value != 0;
                        return true;
                    case long value:
                        b = value != 0;
                        return true;
                    case double value:
                        b = Math.Abs(value) > 0;
                        return true;
                }

                if (bool.TryParse(raw.ToString(), out b))
                {
                    return true;
                }
            }

            b = false;
            return false;
        }

        protected void AppendKey(StringBuilder path, string key)
        {
            if (path.Length > 0) path.Append(".");
            path.Append(key);
        }

        protected void UnappendKey(StringBuilder path)
        {
            int dotIndex = path.Length - 1;
            for (; dotIndex >= 0; --dotIndex)
            {
                if (path[dotIndex] == '.') break;                
            }
            if(dotIndex >= 0) path.Remove(dotIndex, path.Length - dotIndex);
            else path.Clear();
        }
    }
}
