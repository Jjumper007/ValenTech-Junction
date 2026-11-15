// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExternalTypes.Library;

public class AyaR7Tests
{
    private static List<GeneratedMessagePackFormatterAttribute> GetGeneratedFormatterAttributes()
    {
        Assembly assembly = typeof(GeneratedFormatterForExternalTypesTests).Assembly;
        var attributes = new List<GeneratedMessagePackFormatterAttribute>();
        attributes.AddRange(assembly.GetCustomAttributes<GeneratedMessagePackFormatterAttribute>());
        foreach (Type type in assembly.GetTypes())
        {
            attributes.AddRange(type.GetCustomAttributes<GeneratedMessagePackFormatterAttribute>(inherit: false));
        }

        return attributes;
    }

    [Fact]
    public void SurrogateTypeIsPreservedOnAppliedAttribute()
    {
        List<GeneratedMessagePackFormatterAttribute> attributes = GetGeneratedFormatterAttributes();
        Assert.NotEmpty(attributes);

        List<GeneratedMessagePackFormatterAttribute> withSurrogate = attributes
            .Where(attribute => attribute.SurrogateType is not null)
            .ToList();
        Assert.NotEmpty(withSurrogate);
    }

    [Fact]
    public void SurrogateTypeCanBeUpdatedAndCleared()
    {
        GeneratedMessagePackFormatterAttribute attribute = GetGeneratedFormatterAttributes()
            .First(item => item.SurrogateType is not null);

        Type originalSurrogate = attribute.SurrogateType!;
        Assert.NotNull(originalSurrogate);

        attribute.SurrogateType = typeof(ExternalPoint);
        Assert.Equal(typeof(ExternalPoint), attribute.SurrogateType);

        attribute.SurrogateType = null;
        Assert.Null(attribute.SurrogateType);

        attribute.SurrogateType = originalSurrogate;
        Assert.Equal(originalSurrogate, attribute.SurrogateType);
    }
}
