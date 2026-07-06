using System.Text.Json;

namespace MassTransit.PostgresOutbox.Tests;

public class SerializationTests
{
    public enum TestEnum
    {
        None = 0,
        FirstValue = 1,
        SecondValue = 2
    }

    [Fact]
    public void SerializeAndDeserializeUsingType_ShouldMatchOriginal()
    {
        // Arrange
        var original = new ComplexObject
        {
            Integer = 42,
            String = "Hello, World!",
            Double = 3.14159,
            Boolean = true,
            DateTime = DateTime.UtcNow,
            Nested = new NestedObject
            {
                NestedInt = 7,
                NestedString = "Nested",
                Children = new List<NestedObject>
                {
                    new()
                    {
                        NestedInt = 1,
                        NestedString = "Child1"
                    },
                    new()
                    {
                        NestedInt = 2,
                        NestedString = null
                    }
                }
            },
            StringList = new List<string>
            {
                "Item1",
                "Item2",
                "Item3"
            },
            StringIntDictionary = new Dictionary<string, int>
            {
                {
                    "Key1", 100
                },
                {
                    "Key2", 200
                }
            },
            EnumValue = TestEnum.SecondValue,
            ArbitraryObject = new
            {
                Key = "Value",
                Number = 123
            },
            Guid = Guid.NewGuid()
        };

        // Act

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(original, options);

        // Use Type directly to mimic dynamic deserialization
        var objectType = original.GetType();
        var deserialized = JsonSerializer.Deserialize(json, objectType, options);

        // Assert
        Assert.NotNull(deserialized);

        // Compare all fields using reflection
        Assert.True(DeepCompare(original, deserialized!));
    }

    private static bool DeepCompare(object original, object deserialized, string? path = null)
    {
        var originalType = original.GetType();
        var deserializedType = deserialized.GetType();

        if (originalType != deserializedType)
        {
            Console.WriteLine($"Type mismatch at {path}: {originalType} vs {deserializedType}");
            return false;
        }

        foreach (var property in originalType.GetProperties())
        {
            if (property.GetIndexParameters()
                    .Length > 0)
            {
                continue;
            }

            var propertyPath = path == null ? property.Name : $"{path}.{property.Name}";
            var originalValue = property.GetValue(original);
            var deserializedValue = property.GetValue(deserialized);

            if (originalValue is null && deserializedValue is null)
            {
                continue;
            }

            if (originalValue is null || deserializedValue is null)
            {
                Console.WriteLine($"Null mismatch at {propertyPath}");
                return false;
            }

            if (property.PropertyType == typeof(object) || deserializedValue is JsonElement)
            {
                var serializedOriginal = JsonSerializer.Serialize(originalValue);
                var serializedDeserialized = JsonSerializer.Serialize(deserializedValue);
                if (serializedOriginal != serializedDeserialized)
                {
                    Console.WriteLine($"JsonElement mismatch at {propertyPath}");
                    return false;
                }

                continue;
            }

            // Recursive comparison for complex types
            if (property.PropertyType.IsClass && !property.PropertyType.IsPrimitive &&
                property.PropertyType != typeof(string))
            {
                if (!DeepCompare(originalValue, deserializedValue, propertyPath))
                {
                    return false;
                }
            }
            else if (!Equals(originalValue, deserializedValue))
            {
                Console.WriteLine($"Value mismatch at {propertyPath}: {originalValue} vs {deserializedValue}");
                return false;
            }
        }

        return true;
    }

    public class ComplexObject
    {
        public int Integer { get; set; }
        public string? String { get; set; }
        public double Double { get; set; }
        public bool Boolean { get; set; }
        public DateTime DateTime { get; set; }
        public NestedObject? Nested { get; set; }
        public List<string>? StringList { get; set; }
        public Dictionary<string, int>? StringIntDictionary { get; set; }
        public TestEnum EnumValue { get; set; }
        public object? ArbitraryObject { get; set; }
        public Guid Guid { get; set; }
    }

    public class NestedObject
    {
        public int NestedInt { get; set; }
        public string? NestedString { get; set; }
        public List<NestedObject>? Children { get; set; }
    }
}
