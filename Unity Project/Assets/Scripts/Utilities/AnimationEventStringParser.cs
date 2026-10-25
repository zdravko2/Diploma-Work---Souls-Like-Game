using System;
using System.Collections.Generic;

public class AnimationEventStringParser
{
    public Dictionary<string, string> KeyValuePairs { get; private set; }

    public AnimationEventStringParser(string input, char pairSeparator = ',', char keyValueSeparator = ':')
    {
        KeyValuePairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(input))
            return;

        var pairs = input.Split(pairSeparator);
        foreach (var pair in pairs)
        {
            var kv = pair.Split(new char[] { keyValueSeparator }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim();
                var value = kv[1].Trim();

                if (!string.IsNullOrEmpty(key))
                {
                    KeyValuePairs[key] = value;
                }
            }
            else
            {
                // Optionally handle malformed pairs here if needed
            }
        }
    }

    public bool TryGetValue(string key, out string value)
    {
        return KeyValuePairs.TryGetValue(key, out value);
    }
}
