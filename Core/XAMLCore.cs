using System.Globalization;
using Windows.UI.Xaml.Markup;

namespace XAMLComposition.Core;

public static class XAMLCore
{
    public static bool IsTraceEnabled { get; set; }
#if DEBUG 
        = true;
#endif

    public static void Trace(string message)
    {
        if (IsTraceEnabled is false)
            return;

        Debug.WriteLine($"[XC] {message}");
    }

    /// <summary>
    /// Attempts to parse a XAML property string into a specific type.
    /// </summary>
    public static bool TryParse<T>(string s, out T value)
    {
        static bool FloatParse(string input, out float output)
        {
            return float.TryParse(input,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out output);
        }

        if (typeof(T) == typeof(bool))
        {
            if (s is "TRUE" or "True" or "true" or "1")
            {
                value = (T)Convert.ChangeType(true, typeof(T));
                return true;
            }
            else if (s is "FALSE" or "False" or "false" or "0")
            {
                value = (T)Convert.ChangeType(false, typeof(T));
                return true;
            }
        }
        else if (typeof(T) == typeof(float))
        {
            if (FloatParse(s, out float result))
            {
                value = (T)Convert.ChangeType(result, typeof(T));
                return true;
            }
        }
        else if (typeof(T) == typeof(Vector3))
        {
            char c = s.Contains(",") ? ',' : ' ';
            string[] parts = s.Split(c, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2
                && FloatParse(parts[0], out float x)
                && FloatParse(parts[1], out float y))
            {
                if (parts.Length == 2)
                {
                    value = (T)Convert.ChangeType(
                        new Vector3(x, y, 0f), typeof(Vector3));
                    return true;
                }
                else if (parts.Length == 3 && FloatParse(parts[2], out float z))
                {
                    value = (T)Convert.ChangeType(
                        new Vector3(x, y, z), typeof(Vector3));
                    return true;
                }
            }
        }
        else if (typeof(T) == typeof(Vector4))
        {
            char c = s.Contains(",") ? ',' : ' ';
            string[] parts = s.Split(c, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2
                && FloatParse(parts[0], out float x)
                && FloatParse(parts[1], out float y))
            {
                if (parts.Length == 2)
                {
                    value = (T)Convert.ChangeType(
                        new Vector4(x, y, 0f, 1f), typeof(Vector4));
                    return true;
                }
                else if (parts.Length == 3 && FloatParse(parts[2], out float z))
                {
                    value = (T)Convert.ChangeType(
                        new Vector4(x, y, z, 1f), typeof(Vector4));
                    return true;
                }
                else if (parts.Length == 4
                    && FloatParse(parts[2], out z)
                    && FloatParse(parts[3], out float w))
                {
                    value = (T)Convert.ChangeType(
                        new Vector4(x, y, z, w), typeof(Vector4));
                    return true;
                }
            }
        }
        else
        {
            try
            {
                value = (T)XamlBindingHelper.ConvertValue(typeof(T), s);
                return true;
            }
            catch { }
        }

        value = default;
        return false;
    }

}
