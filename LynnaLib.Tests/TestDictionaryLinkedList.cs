using System.Text.Json;
using Util;

namespace LynnaLib.Tests;

public class TestDictionaryLinkedList
{
    [Fact]
    public void TestSerialize()
    {
        DictionaryLinkedList<int> list = new();
        TrySerialize<int>(list, "[]");
        list.AddLast(3);
        TrySerialize<int>(list, "[3]");
        list.AddLast(4);
        TrySerialize<int>(list, "[3,4]");
        list.AddAfter(3, 5);
        TrySerialize<int>(list, "[3,5,4]");
        list.Remove(5);
        TrySerialize<int>(list, "[3,4]");
    }

    [Fact]
    public void TestDeserialize()
    {
        TryDeserialize<int>("[]", new int[] {});
        TryDeserialize<int>("[2]", new int[] {2});
        TryDeserialize<int>("[5,7]", new int[] {5,7});
        TryDeserialize<int>("[10,8,5000]", new int[] {10,8,5000});
    }

    void TryDeserialize<T>(string data, IEnumerable<T> expected)
    {
        var dict = JsonSerializer.Deserialize<DictionaryLinkedList<T>>(data);
        Assert.True(dict?.SequenceEqual(expected),
                    $"Deserialization error!\nExpected: {expected.ToArray()}\nGot: {dict?.ToArray()}");
    }

    void TrySerialize<T>(DictionaryLinkedList<T> data, string expected)
    {
        string s = JsonSerializer.Serialize<DictionaryLinkedList<T>>(data);
        Assert.True(s == expected,
                    $"Serialization error!\nExpected: {expected}\nGot: {s}");
    }
}
