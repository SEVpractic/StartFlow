using System.Text.Json;
using System.Text.Json.Serialization;

namespace StartFlow.Services;

public sealed class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    where T : class
{
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<T>();

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    continue;
                }

                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                if (item is not null)
                {
                    result.Add(item);
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = JsonSerializer.Deserialize<T>(ref reader, options);
            if (item is not null)
            {
                result.Add(item);
            }
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            // Старый формат: свойство отсутствовало (null) — оставляем пустой список.
            reader.Skip();
            return result;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
