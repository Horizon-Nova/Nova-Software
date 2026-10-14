using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vaeron.Kernel.Identity;

/// <summary>序列化為純值(字串化 Guid),不是 { "Value": "..." } 物件包裝——消費端拿到的是識別碼本身,不需要知道強型別包裝的存在。</summary>
/// <remarks>
/// 註冊位置依宿主而異,不由本組件決定：
///   Web 宿主 — services.Configure&lt;JsonOptions&gt;(o =&gt; o.SerializerOptions.Converters.Add(new StronglyTypedIdJsonConverterFactory()));
///   其他宿主 — 加入自建的 JsonSerializerOptions.Converters
/// 兩者用同一顆型別、同一個轉換器,只有註冊位置不同。
/// </remarks>
public sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(StronglyTypedId<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var entityType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(Converter<>).MakeGenericType(entityType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class Converter<TEntity> : JsonConverter<StronglyTypedId<TEntity>>
    {
        public override StronglyTypedId<TEntity> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            => new(reader.GetGuid());

        public override void Write(
            Utf8JsonWriter writer,
            StronglyTypedId<TEntity> value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
