// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ExternalTypes.Library;

public class AyaA10Attack
{
    [Fact]
    public void SurrogateTypeDefaultsToNull()
    {
        var attribute = new GeneratedMessagePackFormatterAttribute();

        Assert.Null(attribute.SurrogateType);
    }

    [Fact]
    public void SurrogateTypeRoundTripsAssignedType()
    {
        var attribute = new GeneratedMessagePackFormatterAttribute
        {
            SurrogateType = typeof(ExternalPerson),
        };

        Assert.Same(typeof(ExternalPerson), attribute.SurrogateType);
    }

    [Fact]
    public void SurrogateTypeCanBeReassignedAndClearedToNull()
    {
        var attribute = new GeneratedMessagePackFormatterAttribute
        {
            SurrogateType = typeof(int),
        };
        Assert.Same(typeof(int), attribute.SurrogateType);

        attribute.SurrogateType = typeof(string);
        Assert.Same(typeof(string), attribute.SurrogateType);

        attribute.SurrogateType = null;
        Assert.Null(attribute.SurrogateType);
    }

    [Fact]
    public void AttributeOnFormatterExposesConfiguredSurrogateType()
    {
        var surrogateAttr = Assert.IsType<GeneratedMessagePackFormatterAttribute>(
            System.Attribute.GetCustomAttribute(typeof(ExternalWidgetFormatter), typeof(GeneratedMessagePackFormatterAttribute)));
        Assert.NotNull(surrogateAttr.SurrogateType);

        var contractlessAttr = Assert.IsType<GeneratedMessagePackFormatterAttribute>(
            System.Attribute.GetCustomAttribute(typeof(ExternalPersonFormatter), typeof(GeneratedMessagePackFormatterAttribute)));
        Assert.Null(contractlessAttr.SurrogateType);
    }

    [Fact]
    public void AttributeUsageRestrictsToSingleNonInheritedClassAttribute()
    {
        var usage = Assert.IsType<System.AttributeUsageAttribute>(
            System.Attribute.GetCustomAttribute(typeof(GeneratedMessagePackFormatterAttribute), typeof(System.AttributeUsageAttribute)));

        Assert.Equal(System.AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
