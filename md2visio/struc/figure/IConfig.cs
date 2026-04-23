namespace md2visio.struc.figure
{
    internal interface IConfig
    {
        bool GetBool(string keyPath, out bool b);
        bool GetDouble(string keyPath, out double d);
        bool GetInt(string keyPath, out int i);
        bool GetString(string keyPath, out string val);
    }
}
