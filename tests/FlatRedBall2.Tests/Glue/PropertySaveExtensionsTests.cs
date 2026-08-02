using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Glue stores most of an element's meaningful data in a `Properties` name/value bag rather than as
// named JSON fields, so this helper is the only way to read those values. It mirrors FRB1's
// PropertySaveListExtensions.GetValue<T>, but over JsonElement instead of Newtonsoft's boxed object.
public class PropertySaveExtensionsTests
{
    private enum TestSourceType
    {
        File = 0,
        Entity = 1,
        FlatRedBallType = 2,
    }

    private static List<PropertySave> Bag(string name, string rawJsonValue, string? type = null)
    {
        string typeJson = type is null ? "" : $@", ""Type"": ""{type}""";
        string json = $@"[{{ ""Name"": ""{name}"", ""Value"": {rawJsonValue}{typeJson} }}]";
        return JsonSerializer.Deserialize<List<PropertySave>>(json)!;
    }

    [Fact]
    public void GetValue_AbsentName_NullableInt_ReturnsNull()
    {
        var bag = Bag("Present", "1");

        bag.GetValue<int?>("Missing").ShouldBeNull();
    }

    [Fact]
    public void GetValue_AbsentName_ReturnsDefaultAndDoesNotThrow()
    {
        var bag = Bag("Present", "1");

        bag.GetValue<int>("Missing").ShouldBe(0);
        bag.GetValue<bool>("Missing").ShouldBeFalse();
        bag.GetValue<string>("Missing").ShouldBeNull();
    }

    [Fact]
    public void GetValue_Bool_ReturnsBool()
    {
        var bag = Bag("AutoCreateGumScreens", "true", "Boolean");

        bag.GetValue<bool>("AutoCreateGumScreens").ShouldBeTrue();
    }

    [Fact]
    public void GetValue_EnumFromRawInt_ReturnsEnumMember()
    {
        // Glue serializes enums as bare ints with no string converter, so "SourceType": 2 must
        // decode to the member whose value is 2 rather than throwing or returning default.
        var bag = Bag("SourceType", "2", "SourceType");

        bag.GetValue<TestSourceType>("SourceType").ShouldBe(TestSourceType.FlatRedBallType);
    }

    [Fact]
    public void GetValue_Float_ReturnsFloat()
    {
        var bag = Bag("Radius", "16.0", "float");

        bag.GetValue<float>("Radius").ShouldBe(16f);
    }

    [Fact]
    public void GetValue_Int_ReturnsInt()
    {
        var bag = Bag("FileAdditionBehavior", "1", "int");

        bag.GetValue<int>("FileAdditionBehavior").ShouldBe(1);
    }

    [Fact]
    public void GetValue_MissingTypeString_StillDecodesFromRequestedType()
    {
        // Real .gluj entries omit "Type" entirely (e.g. IncludeFormsInComponents in ChickenClicker),
        // so decoding must be driven by the requested T, never by the Type string.
        var bag = Bag("IncludeFormsInComponents", "true");

        bag.GetValue<bool>("IncludeFormsInComponents").ShouldBeTrue();
    }

    [Fact]
    public void GetValue_String_ReturnsString()
    {
        var bag = Bag("CircleInstanceColor", @"""White""", "String");

        bag.GetValue<string>("CircleInstanceColor").ShouldBe("White");
    }

    [Fact]
    public void GetValue_TypeStringDisagreesWithValue_RequestedTypeWins()
    {
        // The Type string is unreliable metadata: it is a mix of C# keywords, CLR names, and Glue
        // enum names, and it can disagree with the actual JSON value. T is authoritative.
        var bag = Bag("Odd", "3", "Boolean");

        bag.GetValue<int>("Odd").ShouldBe(3);
    }
}
