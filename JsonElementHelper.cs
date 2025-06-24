using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WSLAttachSwitch
{
    internal static class JsonElementHelper
    {

        public static JsonElement GetPropertyCaseInsensitive(this JsonElement jsonElement, string propertyName)
        {
            if (jsonElement.TryGetProperty(propertyName, out JsonElement jsonProperty))
            {
                return jsonProperty;
            }
            else
            {
                //On some machines (not sure depending on what factor exactly), the property names within the Hyper-V API response come entirely upper cased (opposed to the provided schema), therefore the fallback here:
                foreach (JsonProperty property in jsonElement.EnumerateObject())
                {
                    if (property.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return property.Value;
                    }
                }
            }


            throw new KeyNotFoundException($"Property '{propertyName}' not found in JSON element.");
        }
    }
}
