using FluentAssertions;
using RecipeHub.Application.Dtos;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Tests;

public class ParsingTests
{
    [Theory]
    [InlineData("pcs", Unit.Pcs)]
    [InlineData("g", Unit.G)]
    [InlineData("ml", Unit.Ml)]
    [InlineData("pack", Unit.Pack)]
    public void UnitParsing_round_trips(string api, Unit unit)
    {
        UnitParsing.Parse(api).Should().Be(unit);
        UnitParsing.ToApi(unit).Should().Be(api);
    }

    [Fact]
    public void UnitParsing_rejects_unknown()
    {
        var act = () => UnitParsing.Parse("cups");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("breakfast", MealSlot.Breakfast)]
    [InlineData("dinner", MealSlot.Dinner)]
    public void MealSlotParsing_works(string api, MealSlot slot)
    {
        MealSlotParsing.Parse(api).Should().Be(slot);
        MealSlotParsing.ToApi(slot).Should().Be(api);
    }
}
