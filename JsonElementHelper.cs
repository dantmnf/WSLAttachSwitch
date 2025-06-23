using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WSLAttachSwitch
{
    internal static class JsonElementHelper
    {

        public static JsonElement GetPropertyCaseInsensitive(this JsonElement jsonElement, string propertyName)
        {
            foreach (JsonProperty property in jsonElement.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return property.Value;
                }
            }

            throw new KeyNotFoundException($"Property '{propertyName}' not found in JSON element.");
        }
    }
}
