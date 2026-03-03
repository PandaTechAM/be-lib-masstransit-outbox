using System.Text.Json;

namespace MassTransit.PostgresOutbox.Demo.Shared.Events;

public class ComplexObjectEvent
{
   public int Integer { get; init; }
   public string? String { get; init; }
   public double Double { get; init; }
   public bool Boolean { get; init; }
   public DateTime DateTime { get; init; }
   public NestedObject? Nested { get; init; }
   public List<string>? StringList { get; init; }
   public Dictionary<string, int>? StringIntDictionary { get; init; }
   public TestEnum EnumValue { get; init; }
   public object? ArbitraryObject { get; init; }
   public required byte[] Bytes { get; init; }

   public static ComplexObjectEvent Init()
   {
      return new ComplexObjectEvent
      {
         Integer = 42,
         String = "Hello, World!",
         Double = 3.14159,
         Boolean = true,
         DateTime = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
         Nested = new NestedObject
         {
            NestedInt = 7,
            NestedString = "Nested",
            Children =
            [
               new NestedObject
               {
                  NestedInt = 1,
                  NestedString = "Child1"
               },

               new NestedObject
               {
                  NestedInt = 2,
                  NestedString = null
               }
            ]
         },
         StringList =
         [
            "Item1",

            "Item2",

            "Item3"
         ],
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
         Bytes = [1, 2, 3, 4, 5]
      };
   }

   public bool Equals(ComplexObjectEvent other)
   {
      if (ReferenceEquals(this, other))
      {
         return true;
      }

      var isMatch = true;

      if (Integer != other.Integer)
      {
         Console.WriteLine($"Mismatch in Integer: {Integer} != {other.Integer}");
         isMatch = false;
      }

      if (String != other.String)
      {
         Console.WriteLine($"Mismatch in String: \"{String}\" != \"{other.String}\"");
         isMatch = false;
      }

      if (!Double.Equals(other.Double))
      {
         Console.WriteLine($"Mismatch in Double: {Double} != {other.Double}");
         isMatch = false;
      }

      if (Boolean != other.Boolean)
      {
         Console.WriteLine($"Mismatch in Boolean: {Boolean} != {other.Boolean}");
         isMatch = false;
      }

      if (!DateTime.Equals(other.DateTime))
      {
         Console.WriteLine($"Mismatch in DateTime: {DateTime} != {other.DateTime}");
         isMatch = false;
      }

      if (!(Nested?.Equals(other.Nested) ?? other.Nested == null))
      {
         Console.WriteLine("Mismatch in Nested object.");
         isMatch = false;
      }

      if (!(StringList?.SequenceEqual(other.StringList!) ?? other.StringList == null))
      {
         Console.WriteLine(
            $"Mismatch in StringList: {string.Join(", ", StringList ?? Enumerable.Empty<string>())} != {string.Join(", ", other.StringList ?? Enumerable.Empty<string>())}");
         isMatch = false;
      }

      if (StringIntDictionary?.Count != other.StringIntDictionary?.Count ||
          !StringIntDictionary!.All(kvp =>
             other.StringIntDictionary!.TryGetValue(kvp.Key, out var value) && value == kvp.Value))
      {
         Console.WriteLine("Mismatch in StringIntDictionary.");
         isMatch = false;
      }

      if (EnumValue != other.EnumValue)
      {
         Console.WriteLine($"Mismatch in EnumValue: {EnumValue} != {other.EnumValue}");
         isMatch = false;
      }

      if (ArbitraryObject is not null && other.ArbitraryObject is not null)
      {
         var serializedOriginal = JsonSerializer.Serialize(ArbitraryObject);
         var serializedOther = JsonSerializer.Serialize(other.ArbitraryObject);
         if (serializedOriginal != serializedOther)
         {
            Console.WriteLine($"Mismatch in ArbitraryObject: {serializedOriginal} != {serializedOther}");
            isMatch = false;
         }
      }
      else if (ArbitraryObject is not null || other.ArbitraryObject is not null)
      {
         Console.WriteLine("Mismatch in ArbitraryObject: one is null");
         isMatch = false;
      }

      if (!Bytes.SequenceEqual(other.Bytes))
      {
         Console.WriteLine($"Mismatch in Bytes: [{string.Join(", ", Bytes)}] != [{string.Join(", ", other.Bytes)}]");
         isMatch = false;
      }

      return isMatch;
   }
}

public class NestedObject
{
   public int NestedInt { get; init; }
   public string? NestedString { get; init; }
   public List<NestedObject>? Children { get; init; }

   public override bool Equals(object? obj)
   {
      if (obj is not NestedObject other)
      {
         return false;
      }

      return NestedInt == other.NestedInt &&
             NestedString == other.NestedString &&
             (Children?.SequenceEqual(other.Children!) ?? other.Children == null);
   }

   public override int GetHashCode()
   {
      return HashCode.Combine(NestedInt, NestedString, Children);
   }
}

public enum TestEnum
{
   None = 0,
   FirstValue = 1,
   SecondValue = 2
}